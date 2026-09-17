using System.Net;
using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

namespace TrainingLoadAnalyzer.Infrastructure.Tests;

/// <summary>
///   User Story 3 — reconciling edits and deletions, and never doing so on the strength of a read
///   that did not finish.
/// </summary>
public sealed class ReconciliationTests
{
    private const string Recent = "2026-09-12T05:00:00Z";

    // T102: scenario US3.6, FR-031, FR-031a — an activity deleted on Strava, dated inside the span
    // a routine sync reads, has its session removed and the removal reported by identifier.
    [Fact]
    public async Task AnActivityDeletedOnStravaHasItsSessionRemoved()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000001", "2026-09-10T04:30:00Z"),
                SyncHarness.Activity("11000000002", Recent, "Ride"))),
            token);

        // 11000000001 is gone from Strava, and the span read covers its date.
        var (result, _) = await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000002", Recent, "Ride"))),
            token);

        Assert.Single(result.Removed);
        Assert.Equal("11000000001", result.Removed[0].ExternalId);
        Assert.Equal(RemovalReason.DeletedAtSource, result.Removed[0].Reason);
        Assert.Equal(1L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T104: THE test the whole design rests on — scenario US3.8, FR-031c, C61. A sync that did not
    // read its span to completion removes NOTHING. An activity missing from a truncated read is
    // missing from the read, not from Strava; deleting an athlete's training on the strength of a
    // dropped connection is the one failure this feature must never produce.
    [Fact]
    public async Task ASyncThatDidNotFinishRemovesNothing()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000001", "2026-09-10T04:30:00Z"),
                SyncHarness.Activity("11000000002", Recent, "Ride"))),
            token);

        // The first page arrives without 11000000001; the walk then drops before it can finish.
        var truncated = new StubHttpMessageHandler()
            .Respond(
                "athlete/activities",
                HttpStatusCode.OK,
                SyncHarness.History(SyncHarness.Activity("11000000002", Recent, "Ride")),
                once: true)
            .Drop("athlete/activities");

        var (result, _) = await harness.SyncAsync(truncated, token);

        // Phase 6 turned failures into outcomes rather than exceptions, so the guarantee is now
        // carried by the completeness flag rather than by the throw aborting the walk.
        Assert.Equal(SyncOutcome.Interrupted, result.Outcome);
        Assert.Empty(result.Removed);
        Assert.Equal(2L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T106: scenario US3.7, FR-031b — an activity deleted long ago survives a routine sync,
    // because a routine sync does not read that far back. A stated limit, not a defect: Strava's
    // listing is filtered by when an activity happened, not by when it changed.
    [Fact]
    public async Task AnActivityDeletedOutsideTheReadSpanSurvivesARoutineSync()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000001", "2026-06-01T04:30:00Z"),
                SyncHarness.Activity("11000000002", Recent, "Ride"))),
            token);

        var (result, _) = await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000002", Recent, "Ride"))),
            token);

        Assert.Empty(result.Removed);
        Assert.Equal(2L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T107: scenario US3.9, FR-031e — an activity corrected to a sport outside the table is
    // removed on the same terms as a deletion. Expected to fall out of the reconciliation rule with
    // no code of its own: the corrected activity is filtered by the sport-type table, so its id
    // never enters the seen set.
    [Fact]
    public async Task AnActivityCorrectedToAnOutOfScopeSportIsRemoved()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000004", "2026-09-13T05:00:00Z", "Ride"))),
            token);

        Assert.Equal(1L, harness.Scalar("SELECT COUNT(*) FROM Activities"));

        var (result, _) = await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000004", "2026-09-13T05:00:00Z", "EBikeRide"))),
            token);

        Assert.Single(result.Removed);
        Assert.Equal(0L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T108: scenario US3.5, FR-032 — a full resynchronization reads everything regardless of the
    // resume point, and duplicates nothing.
    [Fact]
    public async Task AFullResyncReadsTheWholeHistoryAndDuplicatesNothing()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;
        var history = SyncHarness.History(
            SyncHarness.Activity("11000000001", "2026-01-10T04:30:00Z"),
            SyncHarness.Activity("11000000002", Recent, "Ride"));

        await harness.SyncAsync(SyncHarness.Serving(history), token);

        await using var db = harness.NewContext();
        var stub = SyncHarness.Serving(history);
        var result = await new StravaActivitySync(
            new TrainingLoadAnalyzer.Infrastructure.Strava.StravaApiClient(new HttpClient(stub)),
            new TrainingLoadAnalyzer.Infrastructure.Persistence.ActivityStore(db),
            db,
            harness.Clock).FullResyncAsync(token);

        Assert.Equal(2, result.Updated);
        Assert.Equal(2L, harness.Scalar("SELECT COUNT(*) FROM Activities"));

        var after = stub.Requests
            .First(r => r.RequestUri!.ToString().Contains("athlete/activities", StringComparison.Ordinal))
            .RequestUri!.ToString();

        Assert.Contains("after=0", after, StringComparison.Ordinal);
    }

    // T110: FR-017b against a full resync — the path most likely to strip a series by accident.
    [Fact]
    public async Task AFullResyncKeepsAStoredSeries()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;
        var history = SyncHarness.History(
            SyncHarness.Activity("11000000001", "2026-09-10T04:30:00Z", hasHeartrate: true));

        await harness.SyncAsync(SyncHarness.Serving(history), token);
        Assert.Equal(1L, harness.Scalar("SELECT COUNT(*) FROM Activities WHERE HeartRateJson IS NOT NULL"));

        await using var db = harness.NewContext();
        await new StravaActivitySync(
            new TrainingLoadAnalyzer.Infrastructure.Strava.StravaApiClient(
                new HttpClient(SyncHarness.Serving(history))),
            new TrainingLoadAnalyzer.Infrastructure.Persistence.ActivityStore(db),
            db,
            harness.Clock).FullResyncAsync(token);

        Assert.Equal(1L, harness.Scalar("SELECT COUNT(*) FROM Activities WHERE HeartRateJson IS NOT NULL"));
    }
}
