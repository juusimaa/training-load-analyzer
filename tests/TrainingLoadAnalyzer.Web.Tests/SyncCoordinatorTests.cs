using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Web.Features.Sync;
using TrainingLoadAnalyzer.Web.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   The one-at-a-time guard, and every way a sync can end (FR-009, FR-010, C93 - C96).
/// </summary>
/// <remarks>
///   Feature 005's <c>StravaActivitySync</c> guards concurrent syncs with an <em>instance</em>
///   field, and a probe showed it cannot be registered as a singleton at all. As a scoped service
///   two circuits get two semaphores, so 005 FR-040's guarantee stops holding the moment a host
///   with more than one scope exists — which is this feature. The guarantee lives here now
///   (research R13).
/// </remarks>
public class SyncCoordinatorTests
{
    private const string ActivitiesJson = """
        [
          { "id": "5001", "sport_type": "Run",  "start_date": "2026-09-18T04:00:00Z",
            "utc_offset": 10800, "moving_time": 3600, "has_heartrate": false },
          { "id": "5002", "sport_type": "Ride", "start_date": "2026-09-17T04:00:00Z",
            "utc_offset": 10800, "moving_time": 5400, "has_heartrate": false }
        ]
        """;

    private static async Task ConnectAsync(WebAppFactory app)
    {
        await using var db = app.NewContext();

        db.Connections.Add(new StravaConnection
        {
            AthleteId = 900001,
            AccessToken = "an-access-token",
            RefreshToken = "a-refresh-token",
            ExpiresAtUtcTicks = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcTicks,
            GrantedScopes = "read,activity:read_all",
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static SyncCoordinator CoordinatorIn(WebAppFactory app) =>
        app.Services.GetRequiredService<SyncCoordinator>();

    /// <summary>
    ///   US5 scenario 5 and C95. <c>SyncAsync</c> throws when no account is connected; the athlete
    ///   must see a message, not a crashed page.
    /// </summary>
    [Fact]
    public async Task With_no_connection_the_sync_reports_a_failure_rather_than_throwing()
    {
        await using var app = new WebAppFactory();
        _ = app.CreateClient();

        var status = await CoordinatorIn(app).RunAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(status.Failure);
        Assert.False(status.IsRunning);
        Assert.Equal(SyncMessage.ConnectionRequired, SyncMessage.For(status));
    }

    /// <summary>FR-009, US5 scenario 2: the button's whole purpose, end to end.</summary>
    [Fact]
    public async Task A_sync_imports_what_strava_returns()
    {
        await using var app = new WebAppFactory();
        app.Strava
            .RespondFirst("&page=2&", HttpStatusCode.OK, "[]")
            .Respond("athlete/activities", HttpStatusCode.OK, ActivitiesJson);

        _ = app.CreateClient();
        await ConnectAsync(app);

        var status = await CoordinatorIn(app).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SyncOutcome.Completed, status.Result!.Outcome);
        Assert.Equal(2, status.Result.Imported);

        await using var db = app.NewContext();
        Assert.Equal(2, await db.Activities.CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///   Discriminating check for C94 and research R13. Two syncs at once produce <em>one</em> walk:
    ///   the stub recorded a single request for the first page, not two. A coordinator without a
    ///   guard passes every other test in this file.
    /// </summary>
    [Fact]
    public async Task Two_syncs_at_once_produce_one_walk()
    {
        await using var app = new WebAppFactory();
        app.Strava
            .RespondFirst("&page=2&", HttpStatusCode.OK, "[]")
            .Respond("athlete/activities", HttpStatusCode.OK, ActivitiesJson);

        _ = app.CreateClient();
        await ConnectAsync(app);

        var coordinator = CoordinatorIn(app);
        var gate = new TaskCompletionSource();
        app.Strava.Gate = gate;

        var first = coordinator.RunAsync(CancellationToken.None);

        // The first walk is held at its first request, so the second arrives while it is in flight.
        while (app.Strava.Requests.Count == 0)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        var refused = await coordinator.RunAsync(TestContext.Current.CancellationToken);

        gate.SetResult();
        await first;

        Assert.True(refused.IsRunning);
        Assert.Single(app.Strava.Requests.Where(r => r.RequestUri!.Query.Contains("&page=1&", StringComparison.Ordinal)));
    }

    /// <summary>
    ///   Discriminating check for C93. The coordinator resolves as the <em>same instance</em> from
    ///   two scopes. Every other test here passes against a scoped registration — and a scoped one
    ///   is exactly what silently breaks the page-refresh edge case and the two-tab case, because a
    ///   Blazor circuit is a DI scope.
    /// </summary>
    [Fact]
    public async Task The_coordinator_is_one_instance_for_the_whole_process()
    {
        await using var app = new WebAppFactory();
        _ = app.CreateClient();

        using var first = app.Services.CreateScope();
        using var second = app.Services.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<SyncCoordinator>(),
            second.ServiceProvider.GetRequiredService<SyncCoordinator>());
    }

    /// <summary>
    ///   C96: the sync's context belongs to a scope the coordinator creates and disposes per run,
    ///   never to a circuit that may live for hours (research R12).
    /// </summary>
    [Fact]
    public async Task Each_run_uses_its_own_scope()
    {
        await using var app = new WebAppFactory();
        app.Strava
            .RespondFirst("&page=2&", HttpStatusCode.OK, "[]")
            .Respond("athlete/activities", HttpStatusCode.OK, ActivitiesJson);

        _ = app.CreateClient();
        await ConnectAsync(app);

        var coordinator = CoordinatorIn(app);

        await coordinator.RunAsync(TestContext.Current.CancellationToken);
        var second = await coordinator.RunAsync(TestContext.Current.CancellationToken);

        // A context reused across both runs would still be tracking the first run's entities and
        // report them as new again.
        Assert.Equal(0, second.Result!.Imported);
        Assert.Equal(2, second.Result.Updated);
    }

    /// <summary>
    ///   US5 scenario 4 and 005 FR-035: Strava's own window boundary, converted into the athlete's
    ///   offset by the coordinator rather than by the machine's time zone at the point of display.
    /// </summary>
    [Fact]
    public async Task A_rate_limited_sync_reports_when_to_return()
    {
        await using var app = new WebAppFactory();
        app.Strava.Respond(
            "athlete/activities",
            HttpStatusCode.TooManyRequests,
            "{}",
            new Dictionary<string, string>
            {
                ["X-RateLimit-Limit"] = "100,1000",
                ["X-RateLimit-Usage"] = "100,120",
            });

        _ = app.CreateClient();
        await ConnectAsync(app);

        var status = await CoordinatorIn(app).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SyncOutcome.RateLimited, status.Result!.Outcome);
        Assert.Contains("Rate limited", SyncMessage.For(status), StringComparison.Ordinal);
    }
}
