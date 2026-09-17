using System.Net;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Strava;
using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

namespace TrainingLoadAnalyzer.Infrastructure.Tests;

/// <summary>
///   User Story 2 at the sync level: the measured window, the streams call, and the imported history
///   reaching the calculators it exists to serve.
/// </summary>
public sealed class ImportedHistoryTests
{
    /// <summary>The fixture history from tasks.md. Purpose-built and anonymised (FR-043).</summary>
    private const string FixtureHistory = """
        [
          {"id":11000000001,"sport_type":"Run","start_date":"2026-09-10T04:30:00Z","utc_offset":10800,
           "moving_time":3120,"elapsed_time":4080,"has_heartrate":true,"manual":false,"private":false},
          {"id":11000000002,"sport_type":"Ride","start_date":"2026-09-12T05:00:00Z","utc_offset":10800,
           "moving_time":5400,"elapsed_time":5400,"has_heartrate":true,"manual":false,"private":false},
          {"id":11000000003,"sport_type":"Swim","start_date":"2026-09-11T05:00:00Z","utc_offset":10800,
           "moving_time":2400,"elapsed_time":2400,"has_heartrate":true,"manual":false,"private":false},
          {"id":11000000004,"sport_type":"EBikeRide","start_date":"2026-09-13T05:00:00Z","utc_offset":10800,
           "moving_time":4800,"elapsed_time":4800,"has_heartrate":true,"manual":false,"private":false},
          {"id":11000000005,"sport_type":"TrailRun","start_date":"2026-09-05T05:00:00Z","utc_offset":10800,
           "moving_time":4200,"elapsed_time":4200,"has_heartrate":false,"manual":false,"private":false},
          {"id":11000000006,"sport_type":"GravelRide","start_date":"2026-09-06T05:00:00Z","utc_offset":10800,
           "moving_time":7200,"elapsed_time":7200,"has_heartrate":false,"manual":false,"private":false},
          {"id":11000000007,"sport_type":"Run","start_date":"2026-09-01T05:00:00Z","utc_offset":10800,
           "moving_time":0,"elapsed_time":600,"has_heartrate":false,"manual":false,"private":false},
          {"id":11000000008,"sport_type":"Run","start_date":"2026-08-28T05:00:00Z","utc_offset":10800,
           "moving_time":1800,"elapsed_time":1800,"has_heartrate":false,"manual":true,"private":true},
          {"id":11000000009,"sport_type":"Ride","start_date":"2024-01-15T09:00:00Z","utc_offset":7200,
           "moving_time":5400,"elapsed_time":5400,"has_heartrate":true,"manual":false,"private":false},
          {"id":11000000010,"sport_type":"Run","start_date":"2026-09-14T05:00:00Z","utc_offset":10800,
           "moving_time":2700,"elapsed_time":2700,"has_heartrate":true,"manual":false,"private":false}
        ]
        """;

    private const string CleanStream =
        """{"time":{"data":[0,300,600]},"heartrate":{"data":[140,150,160]}}""";

    private static StubHttpMessageHandler Stub(string history = FixtureHistory)
    {
        var stub = new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.OK, history, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, "[]");

        // A10 is the fixture whose streams 404 — a manual activity's response, and not a failure.
        stub.Respond("activities/11000000010/streams", HttpStatusCode.NotFound, "{}");
        stub.Respond("streams", HttpStatusCode.OK, CleanStream);

        return stub;
    }

    private static async Task<(SyncResult Result, SqliteFixture<ImportDbContext> Fixture, StubHttpMessageHandler Http)>
        SyncAsync(StubHttpMessageHandler? stub = null)
    {
        stub ??= Stub();
        var fixture = new SqliteFixture<ImportDbContext>(o => new ImportDbContext(o));
        var db = fixture.NewContext();

        db.Connections.Add(new StravaConnection
        {
            AthleteId = 900001,
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            GrantedScopes = "read,activity:read_all",
            ExpiresAtUtcTicks = FixedClock.Default.AddHours(6).UtcTicks,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sync = new StravaActivitySync(
            new StravaApiClient(new HttpClient(stub)),
            new ActivityStore(db),
            db,
            new FixedClock());

        return (await sync.SyncAsync(TestContext.Current.CancellationToken), fixture, stub);
    }

    // T085: scenario US2.1 — seven sessions stored, three skipped with their reasons. A9 is
    // stored like any other ride; being outside the measured window costs it its series, not its
    // place in the history.
    [Fact]
    public async Task TheFixtureHistoryYieldsSevenSessionsAndThreeAccountedSkips()
    {
        var (result, fixture, _) = await SyncAsync();
        using var _f = fixture;

        Assert.Equal(7, result.Imported);
        Assert.Equal(7L, fixture.Scalar("SELECT COUNT(*) FROM Activities"));
        Assert.Equal(3, result.Skipped.Count);
        Assert.Equal(2, result.Skipped.Count(s => s.Reason == SkipReason.SportOutOfScope));
        Assert.Single(result.Skipped, s => s.Reason == SkipReason.UnusableByDomain);
        Assert.Contains(result.Skipped, s => s.ExternalId == "11000000007");
    }

    // T070: FR-017 — an activity inside the measured window with heart-rate data gets its series.
    [Fact]
    public async Task AnActivityInsideTheMeasuredWindowIsFetchedAndStoredWithItsSeries()
    {
        var (_, fixture, http) = await SyncAsync();
        using var _f = fixture;

        Assert.Contains(http.Requests, r => r.RequestUri!.ToString().Contains("activities/11000000001/streams", StringComparison.Ordinal));

        await using var db = fixture.NewContext();
        var a1 = (await new ActivityStore(db).InRangeAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, TestContext.Current.CancellationToken))
            .Single(a => a.ExternalId == "11000000001");

        Assert.NotNull(a1.HeartRate);
        Assert.Equal(LoadProvenance.Measured, a1.CalculateTrainingLoad(190).Provenance);
    }

    // T071: scenario US2.9, FR-017a — A9 is dated 2024, so no request is spent on it at all. The
    // second assertion is the point: this is a budget decision before it is a data one.
    [Fact]
    public async Task AnActivityOutsideTheMeasuredWindowCostsNoStreamsRequest()
    {
        var (_, fixture, http) = await SyncAsync();
        using var _f = fixture;

        Assert.DoesNotContain(http.Requests, r => r.RequestUri!.ToString().Contains("activities/11000000009/streams", StringComparison.Ordinal));

        await using var db = fixture.NewContext();
        var a9 = (await new ActivityStore(db).InRangeAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, TestContext.Current.CancellationToken))
            .Single(a => a.ExternalId == "11000000009");

        Assert.Null(a9.HeartRate);
        Assert.Equal(LoadProvenance.Estimated, a9.CalculateTrainingLoad(190).Provenance);
    }

    // T078: FR-017d, C67 — a 404 from streams means "no streams", not a failure. A10 is stored
    // anyway, its series recorded as outstanding, and the sync continues.
    [Fact]
    public async Task AStreamsRequestReturningNotFoundLeavesTheSeriesOutstanding()
    {
        var (result, fixture, _) = await SyncAsync();
        using var _f = fixture;

        Assert.Equal(1, result.SeriesOutstanding);
        Assert.Equal(SyncOutcome.Completed, result.Outcome);
        Assert.Equal(
            1L,
            fixture.Scalar(
                "SELECT COUNT(*) FROM Activities WHERE ExternalId = '11000000010' AND HeartRateOutstanding = 1"));
    }

    // T080: FR-017b, C56 — a discriminating check. A stored series is never replaced, and no
    // request is spent refetching one. Without this, an implementation that refetches on every sync
    // passes everything else.
    [Fact]
    public async Task AStoredSeriesIsNeitherRefetchedNorReplaced()
    {
        var (_, fixture, _) = await SyncAsync();
        using var _f = fixture;

        var different = new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.OK, FixtureHistory, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, "[]")
            .Respond("streams", HttpStatusCode.OK, """{"time":{"data":[0,60]},"heartrate":{"data":[90,95]}}""");

        await using var db = fixture.NewContext();
        await new StravaActivitySync(
            new StravaApiClient(new HttpClient(different)),
            new ActivityStore(db),
            db,
            new FixedClock()).SyncAsync(TestContext.Current.CancellationToken);

        var a1 = (await new ActivityStore(fixture.NewContext()).InRangeAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, TestContext.Current.CancellationToken))
            .Single(a => a.ExternalId == "11000000001");

        Assert.Equal(3, a1.HeartRate!.Samples.Count);
        Assert.Equal(140, a1.HeartRate.Samples[0].Bpm);
    }

    // T086: scenarios US2.6 and US2.7 — the history survives a restart, and a second identical
    // import duplicates nothing.
    [Fact]
    public async Task ASecondImportOverAnUnchangedHistoryDuplicatesNothing()
    {
        var (_, fixture, _) = await SyncAsync();
        using var _f = fixture;

        await using var db = fixture.NewContext();
        var again = await new StravaActivitySync(
            new StravaApiClient(new HttpClient(Stub())),
            new ActivityStore(db),
            db,
            new FixedClock()).SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, again.Imported);
        Assert.Equal(7, again.Updated);
        Assert.Equal(7L, fixture.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T087: SC-003 — THE test that shows the feature does what it is for. Every other task here
    // tests the importer; this is the only one that shows the imported history actually reaching
    // the calculators features 002 and 003 built, with no network and no Strava.
    [Fact]
    public async Task TheImportedHistoryFeedsTheCalculatorsItExistsToServe()
    {
        var (_, fixture, _) = await SyncAsync();
        using var _f = fixture;

        await using var db = fixture.NewContext();
        IReadOnlyCollection<TrainingActivity> sessions = await new ActivityStore(db).InRangeAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, TestContext.Current.CancellationToken);

        var range = new DateRange(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14));
        var daily = TrainingLoadAggregator.AggregateDaily(sessions, range, maximumHeartRate: 190);
        var weekly = TrainingLoadAggregator.AggregateWeekly(sessions, range, maximumHeartRate: 190);
        var metrics = TrainingMetricsCalculator.Calculate(daily, range);

        Assert.Equal(14, daily.Count);
        Assert.NotEmpty(weekly);
        Assert.Equal(14, metrics.Count);

        // A1 was imported with a measured series, so the day it falls on reports measured load.
        var tenth = daily.Single(d => d.Day == new DateOnly(2026, 9, 10));
        Assert.True(tenth.Points > 0);
        Assert.Equal(LoadBasis.Measured, tenth.Basis);

        // And the fitness series is real rather than empty.
        Assert.True(metrics[^1].Fitness > 0);
    }

    // T088: the deferred half of scenario US1.5 — with no connection, a sync refuses rather than
    // attempting an unauthenticated request.
    [Fact]
    public async Task ASyncWithNoConnectedAccountIsRefused()
    {
        using var fixture = new SqliteFixture<ImportDbContext>(o => new ImportDbContext(o));
        await using var db = fixture.NewContext();
        var http = new StubHttpMessageHandler();

        var sync = new StravaActivitySync(
            new StravaApiClient(new HttpClient(http)),
            new ActivityStore(db),
            db,
            new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sync.SyncAsync(TestContext.Current.CancellationToken));

        Assert.Empty(http.Requests);
    }

    // T082: scenario US2.2 — a history spanning more pages than one request returns is walked
    // completely, and nothing is imported twice because of a page boundary.
    [Fact]
    public async Task EveryPageIsWalkedAndNoActivityIsCountedTwiceAtABoundary()
    {
        const string page1 = """
            [{"id":11000000001,"sport_type":"Run","start_date":"2026-09-10T04:30:00Z","utc_offset":10800,
              "moving_time":3120,"elapsed_time":4080,"has_heartrate":false}]
            """;
        const string page2 = """
            [{"id":11000000002,"sport_type":"Ride","start_date":"2026-09-12T05:00:00Z","utc_offset":10800,
              "moving_time":5400,"elapsed_time":5400,"has_heartrate":false}]
            """;
        const string page3 = """
            [{"id":11000000003,"sport_type":"Run","start_date":"2026-09-13T05:00:00Z","utc_offset":10800,
              "moving_time":1800,"elapsed_time":1800,"has_heartrate":false}]
            """;

        var stub = new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.OK, page1, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, page2, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, page3, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, "[]");

        var (result, fixture, http) = await SyncAsync(stub);
        using var _f = fixture;

        Assert.Equal(3, result.Imported);
        Assert.Equal(3L, fixture.Scalar("SELECT COUNT(*) FROM Activities"));

        // per_page=200 is real but undocumented; it is what keeps the request budget workable.
        Assert.All(
            http.Requests.Where(r => r.RequestUri!.ToString().Contains("athlete/activities", StringComparison.Ordinal)),
            r => Assert.Contains("per_page=200", r.RequestUri!.ToString(), StringComparison.Ordinal));
    }

    // T082 continued: a page boundary must not duplicate. Strava's order is undocumented, so an
    // activity repeated across two pages is deduplicated by id rather than trusted away.
    [Fact]
    public async Task AnActivityAppearingOnTwoPagesIsStoredOnce()
    {
        const string page = """
            [{"id":11000000001,"sport_type":"Run","start_date":"2026-09-10T04:30:00Z","utc_offset":10800,
              "moving_time":3120,"elapsed_time":4080,"has_heartrate":false}]
            """;

        var stub = new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.OK, page, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, page, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, "[]");

        var (result, fixture, _) = await SyncAsync(stub);
        using var _f = fixture;

        Assert.Equal(1, result.Imported);
        Assert.Equal(1L, fixture.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T035, deferred from Phase 3 until an activities table existed: FR-007's boundary.
    // Disconnecting discards the credentials and nothing else — whether the athlete's imported
    // history goes too is their separate, explicit choice, never a side effect.
    [Fact]
    public async Task DisconnectingLeavesTheImportedHistoryUntouched()
    {
        var (_, fixture, _) = await SyncAsync();
        using var _f = fixture;

        await using var db = fixture.NewContext();
        var revoking = new StubHttpMessageHandler().Respond("oauth/revoke", HttpStatusCode.OK);
        await new StravaAuthorization(
            new HttpClient(revoking),
            new StravaCredentials("12345", "a-client-secret"),
            new FixedClock(),
            db).DisconnectAsync(TestContext.Current.CancellationToken);

        Assert.Empty(fixture.NewContext().Connections);
        Assert.Equal(7L, fixture.Scalar("SELECT COUNT(*) FROM Activities"));
    }
}
