using Microsoft.EntityFrameworkCore;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Strava;

namespace TrainingLoadAnalyzer.Infrastructure.Sync;

/// <summary>
///   Walks Strava's activity list and keeps the local copy in step (FR-027 - FR-040).
/// </summary>
/// <remarks>
///   Exactly one entry point per mode (C59). There is no per-activity import method, no
///   streams-only method, and no reconciliation entry point of its own.
/// </remarks>
public sealed class StravaActivitySync(
    StravaApiClient client,
    ActivityStore store,
    ImportDbContext db,
    TimeProvider clock)
{
    /// <summary>
    ///   How far back a heart-rate series is worth the request it costs (FR-017a). Bounded because
    ///   retrieving one costs a request per activity against an allowance of 1,000 a day, while the
    ///   metrics that depend most on load are governed by recent training (research R24).
    /// </summary>
    private static readonly TimeSpan MeasuredWindow = TimeSpan.FromDays(180);

    /// <summary>
    ///   How far back of already-stored ground each sync re-reads (FR-028). It exists so an activity
    ///   uploaded days after it happened is still found — Strava's list is filtered by when an
    ///   activity <em>happened</em>, not by when it arrived. Re-reading a week is harmless because
    ///   every write is an upsert (FR-023), and it is the price of not missing a late upload.
    /// </summary>
    private static readonly TimeSpan LookBackWindow = TimeSpan.FromDays(7);

    /// <summary>Reads from the stored resume point onward (FR-027).</summary>
    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        var connection = await db.Connections.FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No Strava account is connected. Authorize one before synchronising.");

        var state = await db.SyncStates.FindAsync([connection.AthleteId], cancellationToken);
        var resumePoint = state is null
            ? DateTimeOffset.UnixEpoch
            : new DateTimeOffset(state.ResumePointUtcTicks, TimeSpan.Zero);

        return await WalkAsync(connection, resumePoint, cancellationToken);
    }

    /// <summary>
    ///   Reads the whole history regardless of the resume point (FR-032), reconciling over all of
    ///   it — but only if it reads to completion, on the same terms as any other sync (FR-031c).
    /// </summary>
    public async Task<SyncResult> FullResyncAsync(CancellationToken cancellationToken)
    {
        var connection = await db.Connections.FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No Strava account is connected. Authorize one before synchronising.");

        return await WalkAsync(connection, DateTimeOffset.UnixEpoch, cancellationToken);
    }

    private async Task<SyncResult> WalkAsync(
        StravaConnection connection,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        var imported = 0;
        var updated = 0;
        var outstanding = 0;
        var skipped = new List<SkippedActivity>();
        var discarded = new List<DiscardedSamples>();
        var measuredFrom = clock.GetUtcNow() - MeasuredWindow;

        var summaries = await ReadAllPagesAsync(connection.AccessToken, from, cancellationToken);

        // The span was read to completion: every page arrived. Set in exactly one place, because a
        // second assignment is how the guarantee in FR-031c erodes.
        var spanReadToCompletion = true;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // The order Strava returns is undocumented, so it is not depended on; the walk sorts and
        // deduplicates by id instead (research R15).
        foreach (var summary in summaries.DistinctBy(s => s.Id).OrderBy(s => s.StartDate))
        {
            var (series, seriesMissing, dropped) = await SeriesForAsync(
                connection.AccessToken,
                summary,
                measuredFrom,
                cancellationToken);

            if (dropped > 0)
            {
                discarded.Add(new DiscardedSamples(summary.Id, dropped));
            }

            switch (StravaActivityMapper.Map(summary, series))
            {
                case SkippedActivity skip:
                    skipped.Add(skip);

                    break;

                case MappedActivity mapped:
                    seen.Add(summary.Id);

                    var existed = await db.Activities.AnyAsync(
                        a => a.Provider == ActivityStore.Strava && a.ExternalId == summary.Id,
                        cancellationToken);

                    await store.UpsertAsync(mapped.Activity, seriesMissing, cancellationToken);

                    if (existed)
                    {
                        updated++;
                    }
                    else
                    {
                        imported++;
                    }

                    if (seriesMissing)
                    {
                        outstanding++;
                    }

                    break;
            }
        }

        var removed = await ReconcileAsync(spanReadToCompletion, from, seen, cancellationToken);

        await RecordStateAsync(connection.AthleteId, SyncOutcome.Completed, cancellationToken);

        return new SyncResult
        {
            Imported = imported,
            Updated = updated,
            Removed = removed,
            Skipped = skipped,
            SeriesOutstanding = outstanding,
            Discarded = discarded,
            Outcome = SyncOutcome.Completed,
        };
    }

    private async Task<List<StravaActivitySummary>> ReadAllPagesAsync(
        string accessToken,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        var summaries = new List<StravaActivitySummary>();
        var page = 1;

        while (true)
        {
            var batch = await client.GetActivitiesAsync(accessToken, from, page, cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            summaries.AddRange(batch);
            page++;
        }

        return summaries;
    }

    /// <summary>
    ///   Decides whether an activity's heart-rate series is worth fetching, and fetches it if so
    ///   (FR-017). All three conditions must hold: Strava reports it has heart-rate data, it falls
    ///   inside the measured window, and no series is already held.
    /// </summary>
    private async Task<(HeartRateSeries? Series, bool Missing, int Dropped)> SeriesForAsync(
        string accessToken,
        StravaActivitySummary summary,
        DateTimeOffset measuredFrom,
        CancellationToken cancellationToken)
    {
        if (!summary.HasHeartrate || summary.StartDate < measuredFrom)
        {
            return (null, false, 0);
        }

        var held = await db.Activities.AnyAsync(
            a => a.Provider == ActivityStore.Strava
                && a.ExternalId == summary.Id
                && a.HeartRateJson != null,
            cancellationToken);

        if (held)
        {
            // FR-017b, C56: a stored series is never refetched or replaced.
            return (null, false, 0);
        }

        var streams = await client.GetStreamsAsync(accessToken, summary.Id, cancellationToken);

        if (streams is null)
        {
            // A 404 means "no streams", not a failure (C67). Better a session with estimated load
            // than no session at all (FR-017d).
            return (null, true, 0);
        }

        var series = StravaActivityMapper.ToSeries(streams, out var dropped);

        return (series, series is null && dropped == 0, dropped);
    }

    /// <summary>
    ///   Recomputes and persists the resume point (FR-025, FR-028, FR-029). Two terms: the latest
    ///   activity durably stored, and the earliest whose series is still owed. The second is FR-017e
    ///   — rather than keeping a queue of outstanding work, such an activity simply holds the resume
    ///   point behind itself so the next sync re-reads it and retries. That is self-limiting: once it
    ///   ages past the measured window it no longer qualifies under FR-017 and stops holding
    ///   anything back, so a series that never arrives degrades to estimated load instead of wedging
    ///   the sync.
    /// </summary>
    private async Task RecordStateAsync(
        long athleteId,
        SyncOutcome outcome,
        CancellationToken cancellationToken)
    {
        var latestStored = await db.Activities
            .OrderByDescending(a => a.StartedAtUtcTicks)
            .Select(a => (long?)a.StartedAtUtcTicks)
            .FirstOrDefaultAsync(cancellationToken);

        // Only an activity still inside the measured window can hold the resume point back. Once it
        // ages out it no longer qualifies under FR-017, so a series that never arrives degrades to
        // estimated load rather than wedging the sync forever (FR-017e).
        var measuredFromTicks = (clock.GetUtcNow() - MeasuredWindow).UtcTicks;

        var earliestOwed = await db.Activities
            .Where(a => a.HeartRateOutstanding && a.StartedAtUtcTicks >= measuredFromTicks)
            .OrderBy(a => a.StartedAtUtcTicks)
            .Select(a => (long?)a.StartedAtUtcTicks)
            .FirstOrDefaultAsync(cancellationToken);

        var anchor = latestStored is null
            ? DateTimeOffset.UnixEpoch.UtcTicks
            : Math.Min(latestStored.Value, earliestOwed ?? latestStored.Value);

        var state = await db.SyncStates.FindAsync([athleteId], cancellationToken);

        if (state is null)
        {
            state = new SyncState { AthleteId = athleteId };
            db.SyncStates.Add(state);
        }

        state.ResumePointUtcTicks = Math.Max(
            DateTimeOffset.UnixEpoch.UtcTicks,
            anchor - LookBackWindow.Ticks);
        state.LastSyncStartedAtUtcTicks = clock.GetUtcNow().UtcTicks;
        state.LastOutcome = outcome.ToString();

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///   Removes stored sessions that are absent from a span read to completion (FR-031).
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The completeness guard is the whole safety story (FR-031c, C61). The set of activities
    ///     seen is only meaningful if every page arrived; a sync stopped by a rate limit or a
    ///     dropped connection has a <em>partial</em> set, and deleting against it would remove
    ///     training the athlete actually did.
    ///   </para>
    ///   <para>
    ///     FR-031e needs no rule of its own: an activity whose sport type changed to one outside the
    ///     table is filtered out during mapping, so its id never enters the seen set, and it is
    ///     removed by the same path as a deletion.
    ///   </para>
    /// </remarks>
    private async Task<IReadOnlyList<RemovedActivity>> ReconcileAsync(
        bool spanReadToCompletion,
        DateTimeOffset from,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        if (!spanReadToCompletion)
        {
            return [];
        }

        var candidates = await db.Activities
            .Where(a => a.Provider == ActivityStore.Strava && a.StartedAtUtcTicks >= from.UtcTicks)
            .ToListAsync(cancellationToken);

        var gone = candidates.Where(a => !seen.Contains(a.ExternalId)).ToList();

        if (gone.Count == 0)
        {
            return [];
        }

        db.Activities.RemoveRange(gone);
        await db.SaveChangesAsync(cancellationToken);

        return [.. gone.Select(a => new RemovedActivity(a.ExternalId, RemovalReason.DeletedAtSource))];
    }
}
