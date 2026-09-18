using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

namespace TrainingLoadAnalyzer.Infrastructure.Tests;

/// <summary>
///   Real Strava responses occasionally diverge from the naive shape of a field. Each test here
///   pins one such divergence, found against a real account, so it cannot regress silently.
/// </summary>
public sealed class StravaJsonQuirksTests
{
    // A real 10+ year history surfaced an activity whose utc_offset carried a trailing ".0"
    // (e.g. 10800.0) instead of a bare integer. The default int converter rejects that outright,
    // even though the value is whole, and previously crashed the whole sync.
    [Fact]
    public async Task AFractionalUtcOffsetIsAcceptedAndAppliedAsWholeSeconds()
    {
        using var harness = new SyncHarness();

        var history = SyncHarness.History(
            """
            {"id":11000000001,"sport_type":"Run","start_date":"2026-09-10T04:30:00Z","utc_offset":10800.0,
             "moving_time":3120,"elapsed_time":4080,"has_heartrate":false,"manual":false,"private":false}
            """);

        var (result, _) = await harness.SyncAsync(
            SyncHarness.Serving(history), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Imported);

        await using var db = harness.NewContext();
        var stored = db.Activities.Single(a => a.ExternalId == "11000000001");

        // 10800 seconds == 180 minutes == UTC+3, confirming the fractional offset survived intact
        // rather than being truncated to zero or some other silently-wrong value.
        Assert.Equal((short)180, stored.StartedAtOffsetMinutes);
    }
}
