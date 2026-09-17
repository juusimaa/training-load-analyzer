using System.Net;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Strava;
using TrainingLoadAnalyzer.Infrastructure.Sync;

namespace TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

/// <summary>
///   A connected account over an in-memory database, ready to sync against a stubbed Strava.
///   Shared by the incremental, reconciliation and rate-limit tests.
/// </summary>
internal sealed class SyncHarness : IDisposable
{
    private readonly SqliteFixture<ImportDbContext> fixture;

    public SyncHarness()
    {
        fixture = new SqliteFixture<ImportDbContext>(options => new ImportDbContext(options));

        using var db = fixture.NewContext();
        db.Connections.Add(new StravaConnection
        {
            AthleteId = 900001,
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            GrantedScopes = "read,activity:read_all",
            ExpiresAtUtcTicks = FixedClock.Default.AddHours(6).UtcTicks,
        });
        db.SaveChanges();
    }

    public FixedClock Clock { get; } = new();

    public ImportDbContext NewContext() => fixture.NewContext();

    public object? Scalar(string sql) => fixture.Scalar(sql);

    public void Execute(string sql) => fixture.Execute(sql);

    /// <summary>One activity, as Strava's list endpoint renders it.</summary>
    public static string Activity(
        string id,
        string startDate,
        string sportType = "Run",
        int movingTime = 3120,
        bool hasHeartrate = false) =>
        $$"""
        {"id":{{id}},"sport_type":"{{sportType}}","start_date":"{{startDate}}","utc_offset":10800,
         "moving_time":{{movingTime}},"elapsed_time":{{movingTime}},"has_heartrate":{{(hasHeartrate ? "true" : "false")}},
         "manual":false,"private":false}
        """;

    public static string History(params string[] activities) => $"[{string.Join(',', activities)}]";

    /// <summary>A stub serving one page of history, then an empty page to end the walk.</summary>
    public static StubHttpMessageHandler Serving(string history) =>
        new StubHttpMessageHandler()
            .Respond("athlete/activities", HttpStatusCode.OK, history, once: true)
            .Respond("athlete/activities", HttpStatusCode.OK, "[]")
            .Respond("streams", HttpStatusCode.OK, """{"time":{"data":[0,300,600]},"heartrate":{"data":[140,150,160]}}""");

    public async Task<(SyncResult Result, StubHttpMessageHandler Http)> SyncAsync(
        StubHttpMessageHandler stub,
        CancellationToken cancellationToken)
    {
        await using var db = NewContext();
        var sync = new StravaActivitySync(
            new StravaApiClient(new HttpClient(stub)),
            new ActivityStore(db),
            db,
            Clock);

        return (await sync.SyncAsync(cancellationToken), stub);
    }

    public void Dispose() => fixture.Dispose();
}
