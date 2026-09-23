using System.Text.Json.Nodes;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Strava;
using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Web.Features.Sync;

namespace ParityGenerator;

/// <summary>
///   Writes <c>golden/sync/&lt;name&gt;.json</c> (parity.md §4): the reference's sync run over one
///   recorded Strava exchange, against an in-memory SQLite store seeded from the scenario.
/// </summary>
internal static class SyncGolden
{
    public static async Task<JsonObject> BuildAsync(SyncScenario scenario)
    {
        using var database = scenario.Seed();
        var clock = new FixtureClock(scenario.UtcNow, scenario.LocalOffset);
        var replay = new ReplayHandler(scenario);

        SyncResult result;

        // Wired as Program.cs wires it: the typed StravaApiClient over an HttpClient, and the sync
        // over its own context and store.
        await using (var db = database.NewContext())
        {
            var sync = new StravaActivitySync(
                new StravaApiClient(new HttpClient(replay)),
                new ActivityStore(db),
                db,
                clock);

            result = await sync.SyncAsync(CancellationToken.None);
        }

        if (replay.Remaining != 0)
        {
            throw new InvalidOperationException(
                $"{scenario.Name}: {replay.Remaining} recorded exchange(s) were never requested.");
        }

        // As SyncCoordinator.RunAsync records it.
        var status = new SyncStatus
        {
            Result = result,
            FinishedAt = clock.GetLocalNow(),
            RetryAfterLocal = result.RetryAfter is { } retry
                ? TimeZoneInfo.ConvertTime(retry, clock.LocalTimeZone)
                : null,
        };

        await using var read = database.NewContext();

        var rows = read.Activities
            .AsEnumerable()
            .OrderBy(r => r.StartedAtUtcTicks)
            .ThenBy(r => r.ExternalId, StringComparer.Ordinal)
            .ToList();

        var state = read.SyncStates.SingleOrDefault();

        return new JsonObject
        {
            ["name"] = scenario.Name,
            ["activities"] = new JsonArray([.. rows.Select(row =>
            {
                var activity = row.ToDomain();

                return (JsonNode)new JsonObject
                {
                    ["id"] = activity.ExternalId,
                    ["start"] = GoldenJson.WithOffset(activity.StartedAt),
                    ["movingSeconds"] = activity.MovingTime.TotalSeconds,
                    ["type"] = activity.Type.ToString(),
                    ["heartRate"] = activity.HeartRate is { } series
                        ? new JsonArray([.. series.Samples.Select(s =>
                            (JsonNode)new JsonArray(s.TimeFromStart.TotalSeconds, s.Bpm))])
                        : null,
                    ["heartRateOutstanding"] = row.HeartRateOutstanding,
                };
            })]),
            ["syncState"] = state is null ? null : new JsonObject
            {
                ["resumePoint"] = GoldenJson.Utc(new DateTimeOffset(state.ResumePointUtcTicks, TimeSpan.Zero)),
                ["lastSyncStartedAt"] = state.LastSyncStartedAtUtcTicks is { } started
                    ? GoldenJson.Utc(new DateTimeOffset(started, TimeSpan.Zero))
                    : null,
                ["lastOutcome"] = state.LastOutcome,
            },
            ["result"] = new JsonObject
            {
                ["imported"] = result.Imported,
                ["updated"] = result.Updated,
                ["seriesOutstanding"] = result.SeriesOutstanding,
                ["skipped"] = new JsonArray([.. result.Skipped.Select(s =>
                    (JsonNode)new JsonObject { ["id"] = s.ExternalId, ["reason"] = s.Reason.ToString() })]),
                ["removed"] = new JsonArray([.. result.Removed.Select(r =>
                    (JsonNode)new JsonObject { ["id"] = r.ExternalId, ["reason"] = r.Reason.ToString() })]),
                ["discarded"] = new JsonArray([.. result.Discarded.Select(d =>
                    (JsonNode)new JsonObject { ["id"] = d.ExternalId, ["count"] = d.Count })]),
                ["outcome"] = result.Outcome.ToString(),
                ["retryAfter"] = result.RetryAfter is { } after ? GoldenJson.Utc(after) : null,
            },
            ["message"] = SyncMessage.For(status),
            ["requests"] = new JsonArray([.. replay.Requests.Select(r =>
                (JsonNode)new JsonObject { ["method"] = r.Method, ["url"] = r.Url })]),
        };
    }
}
