using System.Net;
using System.Net.Http.Headers;
using TrainingLoadAnalyzer.Infrastructure.Strava;
using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

namespace TrainingLoadAnalyzer.Infrastructure.Tests;

/// <summary>User Story 4 — surviving Strava's limits and a dropped connection.</summary>
public sealed class RateLimitTests
{
    private static HttpResponseHeaders HeadersWith(string limit, string usage)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("X-ReadRateLimit-Limit", limit);
        response.Headers.TryAddWithoutValidation("X-ReadRateLimit-Usage", usage);

        return response.Headers;
    }

    // T112: FR-034 — the read limit is the binding one for an importer: 100 per fifteen minutes and
    // 1,000 a day, against an overall limit of 200 and 2,000 that never binds first.
    [Fact]
    public void TheReadRateLimitHeadersAreParsedIntoBothWindows()
    {
        var status = RateLimitStatus.From(HeadersWith("100,1000", "98,412"));

        Assert.NotNull(status);
        Assert.Equal(100, status.ShortTermLimit);
        Assert.Equal(98, status.ShortTermUsage);
        Assert.Equal(1000, status.DailyLimit);
        Assert.Equal(412, status.DailyUsage);
    }

    // T112 continued: Strava's own documentation is inconsistent about header casing, and HTTP
    // header names are case-insensitive, so matching must be too.
    [Fact]
    public void HeaderNamesAreMatchedWithoutRegardToCase()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("x-readratelimit-limit", "100,1000");
        response.Headers.TryAddWithoutValidation("X-READRATELIMIT-USAGE", "5,50");

        var status = RateLimitStatus.From(response.Headers);

        Assert.NotNull(status);
        Assert.Equal(5, status.ShortTermUsage);
    }

    [Fact]
    public void AResponseWithoutTheHeadersYieldsNoStatus() =>
        Assert.Null(RateLimitStatus.From(new HttpResponseMessage(HttpStatusCode.OK).Headers));

    // T116: C62 — Strava's fifteen-minute windows reset at natural quarter-hours, so the retry time
    // is the next boundary rather than a fixed delay. At 10:07:33 that is 10:15:00, not 10:22:33 —
    // on average this halves the wait.
    [Theory]
    [InlineData("2026-09-17T10:07:33Z", "2026-09-17T10:15:00Z")]
    [InlineData("2026-09-17T10:15:00Z", "2026-09-17T10:30:00Z")]
    [InlineData("2026-09-17T10:46:01Z", "2026-09-17T11:00:00Z")]
    [InlineData("2026-09-17T23:52:00Z", "2026-09-18T00:00:00Z")]
    public void TheShortTermRetryTimeIsTheNextQuarterHourBoundary(string now, string expected)
    {
        var status = RateLimitStatus.From(HeadersWith("100,1000", "100,412"))!;

        Assert.Equal(
            DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            status.RetryAfter(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture)));
    }

    // T116 continued: the daily window resets at midnight UTC, not at a quarter-hour.
    [Fact]
    public void TheDailyRetryTimeIsTheNextMidnightUtc()
    {
        var status = RateLimitStatus.From(HeadersWith("100,1000", "5,1000"))!;

        Assert.Equal(
            new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero),
            status.RetryAfter(FixedClock.Default));
    }

    [Fact]
    public void ABudgetWithRoomLeftIsNotExhausted()
    {
        Assert.False(RateLimitStatus.From(HeadersWith("100,1000", "98,412"))!.IsExhausted);
        Assert.True(RateLimitStatus.From(HeadersWith("100,1000", "100,412"))!.IsExhausted);
        Assert.True(RateLimitStatus.From(HeadersWith("100,1000", "5,1000"))!.IsExhausted);
    }

    private const string TwoActivities = """
        [{"id":11000000001,"sport_type":"Run","start_date":"2026-09-10T04:30:00Z","utc_offset":10800,
          "moving_time":3120,"elapsed_time":3120,"has_heartrate":false,"manual":false,"private":false},
         {"id":11000000002,"sport_type":"Ride","start_date":"2026-09-12T05:00:00Z","utc_offset":10800,
          "moving_time":5400,"elapsed_time":5400,"has_heartrate":false,"manual":false,"private":false}]
        """;

    private static readonly Dictionary<string, string> Exhausted = new()
    {
        ["X-ReadRateLimit-Limit"] = "100,1000",
        ["X-ReadRateLimit-Usage"] = "100,412",
    };

    // T114: scenario US4.1, FR-035, C62 — the limit stops the sync cleanly, everything already
    // stored is kept, and the outcome names both the reason and when to retry.
    [Fact]
    public async Task ReachingTheReadLimitStopsTheSyncAndKeepsWhatWasStored()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var stub = new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
            .Respond("athlete/activities", HttpStatusCode.TooManyRequests, """{"message":"Rate Limit Exceeded"}""", Exhausted);

        var (result, _) = await harness.SyncAsync(stub, token);

        Assert.Equal(SyncOutcome.RateLimited, result.Outcome);
        Assert.Equal(2, result.Imported);
        Assert.Equal(2L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
        Assert.Equal(new DateTimeOffset(2026, 9, 17, 10, 15, 0, TimeSpan.Zero), result.RetryAfter);
    }

    // T118: scenario US4.2, FR-036 — the next sync resumes from the stored resume point rather
    // than starting the history over.
    [Fact]
    public async Task ASyncAfterOneThatStoppedResumesRatherThanRestarting()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            new StubHttpMessageHandler()
                .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
                .Respond("athlete/activities", HttpStatusCode.TooManyRequests, "{}", Exhausted),
            token);

        var (_, http) = await harness.SyncAsync(
            new StubHttpMessageHandler()
                .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
                .Respond("athlete/activities", HttpStatusCode.OK, "[]"),
            token);

        var after = http.Requests
            .First(r => r.RequestUri!.ToString().Contains("athlete/activities", StringComparison.Ordinal))
            .RequestUri!.ToString();

        Assert.DoesNotContain("after=0&", after, StringComparison.Ordinal);
    }

    // T120: scenario US4.3, FR-026, FR-037 — a dropped connection partway leaves the completed
    // pages stored and reports Interrupted, rather than escaping as an exception.
    [Fact]
    public async Task ADroppedConnectionPartwayIsReportedAsInterrupted()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var stub = new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
            .Drop("athlete/activities");

        var (result, _) = await harness.SyncAsync(stub, token);

        Assert.Equal(SyncOutcome.Interrupted, result.Outcome);
        Assert.Equal(2L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T123: C68, a discriminating check. A 429 is a stop signal, never a transient failure.
    // Strava's documentation is explicit that requests violating the short-term limit still count
    // toward the daily one, so retrying into a limit burns a budget that resets only at midnight.
    [Fact]
    public async Task A429IsNeverRetried()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var stub = new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.TooManyRequests, "{}", Exhausted);

        var (result, http) = await harness.SyncAsync(stub, token);

        Assert.Equal(SyncOutcome.RateLimited, result.Outcome);
        Assert.Single(http.Requests, r => r.RequestUri!.ToString().Contains("athlete/activities", StringComparison.Ordinal));
    }

    // T124: FR-037's other exclusions — a 4xx will be malformed again, so it is not retried either.
    [Fact]
    public async Task ABadRequestIsNotRetried()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var stub = new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.BadRequest, "{}");

        var (result, http) = await harness.SyncAsync(stub, token);

        Assert.Equal(SyncOutcome.Interrupted, result.Outcome);
        Assert.Single(http.Requests, r => r.RequestUri!.ToString().Contains("athlete/activities", StringComparison.Ordinal));
    }

    // T122: FR-037 — a 5xx is plausibly transient, so it is retried a bounded number of times.
    [Fact]
    public async Task AServerErrorIsRetriedABoundedNumberOfTimes()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var stub = new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.ServiceUnavailable, "{}", once: true)
            .Respond("athlete/activities", HttpStatusCode.ServiceUnavailable, "{}", once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, "[]");

        var (result, _) = await harness.SyncAsync(stub, token);

        Assert.Equal(SyncOutcome.Completed, result.Outcome);
        Assert.Equal(2, result.Imported);
    }

    // T125: scenario US4.4 — after an interruption, the incomplete page is read again and each
    // activity stored exactly once.
    [Fact]
    public async Task AfterAnInterruptionEachActivityIsStoredExactlyOnce()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            new StubHttpMessageHandler()
                .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
                .Drop("athlete/activities"),
            token);

        await harness.SyncAsync(
            new StubHttpMessageHandler()
                .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
                .Respond("athlete/activities", HttpStatusCode.OK, "[]"),
            token);

        Assert.Equal(2L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T128: FR-006 — a credential rejected mid-sync surfaces as an outcome, not an escaping
    // exception, and what was stored is kept.
    [Fact]
    public async Task ARevokedCredentialMidSyncIsReportedAsNeedingReconnection()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var stub = new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
            .Respond("athlete/activities", HttpStatusCode.Unauthorized, """{"message":"Authorization Error"}""");

        var (result, _) = await harness.SyncAsync(stub, token);

        Assert.Equal(SyncOutcome.ReconnectionRequired, result.Outcome);
        Assert.Equal(2L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T130: scenario US4.5, FR-038, FR-039 — a sync that stopped early is distinguishable from one
    // that completed, in the result and in the stored state.
    [Fact]
    public async Task EverySyncReportsHowItEndedAndTheStateRecordsIt()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var (result, _) = await harness.SyncAsync(
            new StubHttpMessageHandler()
                .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
                .Respond("athlete/activities", HttpStatusCode.TooManyRequests, "{}", Exhausted),
            token);

        Assert.Equal(SyncOutcome.RateLimited, result.Outcome);
        Assert.NotNull(result.RetryAfter);

        await using var db = harness.NewContext();
        Assert.Equal(nameof(SyncOutcome.RateLimited), db.SyncStates.Single().LastOutcome);
    }

    // T131: C66 — no credential reaches a result, in any outcome.
    [Fact]
    public async Task NoOutcomeCarriesACredential()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var (result, _) = await harness.SyncAsync(
            new StubHttpMessageHandler()
                .Respond("athlete/activities", HttpStatusCode.Unauthorized, "{}"),
            token);

        Assert.DoesNotContain("access-1", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-1", result.ToString(), StringComparison.Ordinal);
    }

    // T132: C61 across every outcome — the discriminating check T104 could not make. T104 pins the
    // dropped-connection case; these are the three a later refactor is most likely to miss.
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, SyncOutcome.RateLimited)]
    [InlineData(HttpStatusCode.Unauthorized, SyncOutcome.ReconnectionRequired)]
    [InlineData(HttpStatusCode.BadRequest, SyncOutcome.Interrupted)]
    public async Task NoOutcomeButCompletedEverRemovesASession(HttpStatusCode status, SyncOutcome expected)
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        await harness.SyncAsync(
            new StubHttpMessageHandler()
                .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
                .Respond("athlete/activities", HttpStatusCode.OK, "[]"),
            token);

        // Strava now answers with nothing but a failure. Both stored activities are absent from
        // what was read -- and both must survive, because the read did not finish.
        var (result, _) = await harness.SyncAsync(
            new StubHttpMessageHandler().Respond("athlete/activities", status, "{}", Exhausted),
            token);

        Assert.Equal(expected, result.Outcome);
        Assert.Empty(result.Removed);
        Assert.Equal(2L, harness.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T126: scenario US4.6, FR-040, C63 — a second sync started while one is running is refused
    // rather than blocking or running alongside.
    [Fact]
    public async Task ASecondConcurrentSyncIsRefused()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var gate = new TaskCompletionSource();
        var stub = new StubHttpMessageHandler { Gate = gate }
            .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, "[]");

        await using var db = harness.NewContext();
        var sync = new StravaActivitySync(
            new StravaApiClient(new HttpClient(stub)),
            new TrainingLoadAnalyzer.Infrastructure.Persistence.ActivityStore(db),
            db,
            harness.Clock);

        var first = sync.SyncAsync(token);
        var second = await sync.SyncAsync(token);

        Assert.Equal(SyncOutcome.Refused, second.Outcome);

        gate.SetResult();
        var completed = await first;

        Assert.Equal(SyncOutcome.Completed, completed.Outcome);
    }

    // T126 continued: a refusal does nothing at all -- it must not remove, import, or touch state.
    [Fact]
    public async Task ARefusedSyncChangesNothing()
    {
        using var harness = new SyncHarness();
        var token = TestContext.Current.CancellationToken;

        var gate = new TaskCompletionSource();
        var stub = new StubHttpMessageHandler { Gate = gate }
            .Respond("athlete/activities", HttpStatusCode.OK, TwoActivities, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, "[]");

        await using var db = harness.NewContext();
        var sync = new StravaActivitySync(
            new StravaApiClient(new HttpClient(stub)),
            new TrainingLoadAnalyzer.Infrastructure.Persistence.ActivityStore(db),
            db,
            harness.Clock);

        var first = sync.SyncAsync(token);
        var second = await sync.SyncAsync(token);

        Assert.Empty(second.Removed);
        Assert.Equal(0, second.Imported);
        Assert.Empty(second.Skipped);

        gate.SetResult();
        await first;
    }
}
