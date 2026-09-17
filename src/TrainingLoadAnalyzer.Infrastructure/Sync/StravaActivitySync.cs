using Microsoft.EntityFrameworkCore;
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

    /// <summary>Reads from the stored resume point onward (FR-027).</summary>
    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        var connection = await db.Connections.FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No Strava account is connected. Authorize one before synchronising.");

        var imported = 0;
        var updated = 0;
        var outstanding = 0;
        var skipped = new List<SkippedActivity>();
        var discarded = new List<DiscardedSamples>();
        var measuredFrom = clock.GetUtcNow() - MeasuredWindow;

        var page = 1;
        var summaries = new List<StravaActivitySummary>();

        while (true)
        {
            var batch = await client.GetActivitiesAsync(
                connection.AccessToken,
                DateTimeOffset.UnixEpoch,
                page,
                cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            summaries.AddRange(batch);
            page++;
        }

        // The order Strava returns is undocumented, so it is not depended on (research R15).
        foreach (var summary in summaries.DistinctBy(s => s.Id).OrderBy(s => s.StartDate))
        {
            var seriesOwed = summary.HasHeartrate && summary.StartDate >= measuredFrom;
            Domain.HeartRateSeries? series = null;
            var seriesMissing = false;

            if (seriesOwed)
            {
                var streams = await client.GetStreamsAsync(
                    connection.AccessToken,
                    summary.Id,
                    cancellationToken);

                if (streams is null)
                {
                    // FR-017d: better a session with estimated load than no session at all.
                    seriesMissing = true;
                }
                else
                {
                    series = StravaActivityMapper.ToSeries(streams, out var dropped);

                    if (dropped > 0)
                    {
                        discarded.Add(new DiscardedSamples(summary.Id, dropped));
                    }

                    seriesMissing = series is null && dropped == 0;
                }
            }

            switch (StravaActivityMapper.Map(summary, series))
            {
                case SkippedActivity skip:
                    skipped.Add(skip);

                    break;

                case MappedActivity mapped:
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

        return new SyncResult
        {
            Imported = imported,
            Updated = updated,
            Skipped = skipped,
            SeriesOutstanding = outstanding,
            Discarded = discarded,
            Outcome = SyncOutcome.Completed,
        };
    }
}
