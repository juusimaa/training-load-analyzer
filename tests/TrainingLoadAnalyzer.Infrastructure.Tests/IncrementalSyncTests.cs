using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

namespace TrainingLoadAnalyzer.Infrastructure.Tests;

/// <summary>
///   User Story 3 — keeping the copy current without re-reading four years. Several assertions here
///   are about <em>what was requested</em> rather than what was stored; that is the requirement.
/// </summary>
public sealed class IncrementalSyncTests
{
    private static string AfterOf(StubHttpMessageHandler http)
    {
        var url = http.Requests
            .Last(r => r.RequestUri!.ToString().Contains("athlete/activities", StringComparison.Ordinal))
            .RequestUri!
            .ToString();

        return url.Split("after=")[1].Split('&')[0];
    }

    // T091: scenario US3.1, FR-027 — the second sync does not ask for the whole history.
    [Fact]
    public async Task ALaterSyncAsksOnlyForWhatHasHappenedSince()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000001", "2026-09-10T04:30:00Z"),
                SyncHarness.Activity("11000000002", "2026-09-12T05:00:00Z", "Ride"))),
            token);

        var (result, http) = await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000001", "2026-09-10T04:30:00Z"),
                SyncHarness.Activity("11000000002", "2026-09-12T05:00:00Z", "Ride"),
                SyncHarness.Activity("11000000003", "2026-09-15T05:00:00Z", "Ride"))),
            token);

        Assert.Equal(1, result.Imported);
        Assert.Equal(3L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
        Assert.NotEqual("0", AfterOf(http));
    }

    // T093: scenario US3.3, FR-028 — an activity uploaded days after it happened is still found,
    // because the resume point looks back a week beyond the last thing seen.
    [Fact]
    public async Task AnActivityUploadedLateIsStillPickedUp()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000002", "2026-09-12T05:00:00Z", "Ride"))),
            token);

        // Dated three days before the last import, but uploaded only now.
        var (result, _) = await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000009", "2026-09-09T05:00:00Z"),
                SyncHarness.Activity("11000000002", "2026-09-12T05:00:00Z", "Ride"))),
            token);

        Assert.Equal(1, result.Imported);
        Assert.Equal(
            1L,
            harness.Scalar("SELECT COUNT(*) FROM Activities WHERE ExternalId = '11000000009'"));
    }

    // T093 continued: the look-back is seven days, not open-ended. An activity dated well before it
    // is outside what a routine sync asks for.
    [Fact]
    public async Task TheLookBackWindowIsSevenDaysAndNotOpenEnded()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000002", "2026-09-12T05:00:00Z", "Ride"))),
            token);

        var (_, http) = await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000002", "2026-09-12T05:00:00Z", "Ride"))),
            token);

        var after = DateTimeOffset.FromUnixTimeSeconds(long.Parse(AfterOf(http)));

        Assert.Equal(
            new DateTimeOffset(2026, 9, 12, 5, 0, 0, TimeSpan.Zero).AddDays(-7),
            after);
    }

    // T100: scenario US3.2, FR-033, C60 — nothing new is a success, not a failure.
    [Fact]
    public async Task ASyncThatFindsNothingNewSucceedsWithZeroCounts()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var history = SyncHarness.History(SyncHarness.Activity("11000000001", "2026-09-10T04:30:00Z"));
        await harness.SyncAsync(SyncHarness.Serving(history), token);

        var (result, _) = await harness.SyncAsync(SyncHarness.Serving(history), token);

        Assert.Equal(SyncOutcome.Completed, result.Outcome);
        Assert.Equal(0, result.Imported);
        Assert.Empty(result.Skipped);
        Assert.Empty(result.Removed);
    }

    // T101: scenario US3.4 — re-reading an activity already held leaves one session.
    [Fact]
    public async Task ReReadingAnActivityLeavesExactlyOneSession()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;
        var history = SyncHarness.History(SyncHarness.Activity("11000000001", "2026-09-10T04:30:00Z"));

        await harness.SyncAsync(SyncHarness.Serving(history), token);
        var (result, _) = await harness.SyncAsync(SyncHarness.Serving(history), token);

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T096: FR-017e, FR-029 — an activity whose series is still owed holds the resume point behind
    // itself, so the next sync re-reads it and retries. That is why no separate queue of
    // outstanding work exists (research R18).
    [Fact]
    public async Task AnOutstandingSeriesHoldsTheResumePointBehindItself()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        // The older activity owes a series its streams request could not deliver.
        var stub = SyncHarness.Serving(SyncHarness.History(
            SyncHarness.Activity("11000000010", "2026-09-05T05:00:00Z", hasHeartrate: true),
            SyncHarness.Activity("11000000002", "2026-09-12T05:00:00Z", "Ride")));
        stub.RespondFirst("activities/11000000010/streams", System.Net.HttpStatusCode.NotFound, "{}");

        var (result, _) = await harness.SyncAsync(stub, token);

        Assert.Equal(1, result.SeriesOutstanding);

        await using var db = harness.NewContext();
        var state = db.SyncStates.Single();
        var resume = new DateTimeOffset(state.ResumePointUtcTicks, TimeSpan.Zero);

        // Behind the owing activity, not behind the latest stored one.
        Assert.Equal(new DateTimeOffset(2026, 9, 5, 5, 0, 0, TimeSpan.Zero).AddDays(-7), resume);
    }

    // T098: FR-017e's self-limiting property. Once the owing activity ages past the measured
    // window it no longer qualifies under FR-017, so it must stop holding the resume point back —
    // a series that never arrives degrades to estimated load rather than wedging the sync forever.
    [Fact]
    public async Task AnOwingActivityStopsHoldingTheResumePointOnceItAgesOut()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var stub = SyncHarness.Serving(SyncHarness.History(
            SyncHarness.Activity("11000000010", "2026-09-05T05:00:00Z", hasHeartrate: true),
            SyncHarness.Activity("11000000002", "2026-09-12T05:00:00Z", "Ride")));
        stub.RespondFirst("activities/11000000010/streams", System.Net.HttpStatusCode.NotFound, "{}");
        await harness.SyncAsync(stub, token);

        // A year on, the owing activity is far outside the 180-day measured window. Both activities
        // still exist on Strava, so the history still carries them.
        harness.Clock.Advance(TimeSpan.FromDays(365));
        var later = SyncHarness.Serving(SyncHarness.History(
            SyncHarness.Activity("11000000010", "2026-09-05T05:00:00Z", hasHeartrate: true),
            SyncHarness.Activity("11000000002", "2026-09-12T05:00:00Z", "Ride")));
        later.RespondFirst("activities/11000000010/streams", System.Net.HttpStatusCode.NotFound, "{}");
        await harness.SyncAsync(later, token);

        await using var db = harness.NewContext();
        var resume = new DateTimeOffset(db.SyncStates.Single().ResumePointUtcTicks, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 9, 12, 5, 0, 0, TimeSpan.Zero).AddDays(-7), resume);
    }

    // T099: FR-029 — an activity read but not stored does not move the resume point past itself.
    // A7 is skipped as unusable, so the resume point stays anchored on what was actually stored.
    [Fact]
    public async Task ASkippedActivityDoesNotAdvanceTheResumePoint()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            SyncHarness.Serving(SyncHarness.History(
                SyncHarness.Activity("11000000002", "2026-09-12T05:00:00Z", "Ride"),
                SyncHarness.Activity("11000000007", "2026-09-16T05:00:00Z", movingTime: 0))),
            token);

        await using var db = harness.NewContext();
        var resume = new DateTimeOffset(db.SyncStates.Single().ResumePointUtcTicks, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 9, 12, 5, 0, 0, TimeSpan.Zero).AddDays(-7), resume);
    }
}
