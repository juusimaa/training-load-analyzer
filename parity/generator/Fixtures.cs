using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Persistence;

namespace ParityGenerator;

/// <summary>A history fixture (parity.md §2), turned into the reference's own domain values.</summary>
internal sealed record HistoryFixture(
    string Name,
    DateOnly Today,
    TimeSpan Offset,
    int MaximumHeartRate,
    bool IsStravaConnected,
    IReadOnlyList<TrainingActivity> Activities)
{
    /// <summary>
    ///   Built through the reference's public constructors, so a fixture the reference would refuse
    ///   fails here, loudly, rather than producing a golden for something that cannot exist.
    /// </summary>
    public static HistoryFixture Load(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        return new HistoryFixture(
            (string)root["name"]!,
            DateOnly.ParseExact((string)root["today"]!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Fixtures.ParseOffset((string)root["offset"]!),
            (int)root["maximumHeartRate"]!,
            (bool)root["isStravaConnected"]!,
            [.. root["activities"]!.AsArray().Select(a => Fixtures.Activity(a!.AsObject()))]);
    }
}

/// <summary>A sync scenario (parity.md §4): clock, initial store, and the recorded exchange.</summary>
internal sealed record SyncScenario(
    string Name,
    string Directory,
    DateTimeOffset UtcNow,
    TimeSpan LocalOffset,
    JsonObject? Connection,
    IReadOnlyList<(TrainingActivity Activity, bool Outstanding)> Activities,
    JsonObject? SyncState,
    JsonArray Exchanges)
{
    public static SyncScenario Load(string directory)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "scenario.json")))!.AsObject();
        var clock = root["clock"]!.AsObject();

        return new SyncScenario(
            (string)root["name"]!,
            directory,
            DateTimeOffset.Parse((string)clock["utc"]!, CultureInfo.InvariantCulture),
            Fixtures.ParseOffset((string)clock["localOffset"]!),
            root["connection"] as JsonObject,
            [.. root["activities"]!.AsArray().Select(a => (
                Fixtures.Activity(a!.AsObject()),
                (bool?)a["heartRateOutstanding"] ?? false))],
            root["syncState"] as JsonObject,
            root["exchanges"]!.AsArray());
    }

    /// <summary>
    ///   A real in-memory SQLite database migrated with the reference's own migrations and seeded
    ///   with the scenario's connection, activities and sync state — the shape of the reference's
    ///   SqliteFixture and SyncHarness (copied, not referenced).
    /// </summary>
    public MemoryDatabase Seed()
    {
        var database = new MemoryDatabase();

        using var db = database.NewContext();

        if (Connection is { } c)
        {
            db.Connections.Add(new StravaConnection
            {
                AthleteId = (long)c["athleteId"]!,
                AccessToken = (string)c["accessToken"]!,
                RefreshToken = (string)c["refreshToken"]!,
                ExpiresAtUtcTicks = Fixtures.Instant((string)c["expiresAt"]!).UtcTicks,
                GrantedScopes = (string)c["grantedScopes"]!,
                ConnectedAtUtcTicks = Fixtures.Instant((string)c["connectedAt"]!).UtcTicks,
            });
        }

        db.SaveChanges();

        var store = new ActivityStore(db);

        foreach (var (activity, outstanding) in Activities)
        {
            store.UpsertAsync(activity, outstanding, CancellationToken.None).GetAwaiter().GetResult();
        }

        if (SyncState is { } s && Connection is { } owner)
        {
            db.SyncStates.Add(new SyncState
            {
                AthleteId = (long)owner["athleteId"]!,
                ResumePointUtcTicks = Fixtures.Instant((string)s["resumePoint"]!).UtcTicks,
                LastSyncStartedAtUtcTicks = s["lastSyncStartedAt"] is { } started
                    ? Fixtures.Instant((string)started!).UtcTicks
                    : null,
                LastOutcome = (string)s["lastOutcome"]!,
            });
            db.SaveChanges();
        }

        return database;
    }
}

internal static class Fixtures
{
    public static TrainingActivity Activity(JsonObject a)
    {
        HeartRateSeries? series = null;

        if (a["heartRate"] is JsonArray samples)
        {
            series = new HeartRateSeries(
            [
                .. samples.Select(p => new HeartRateSample(
                    TimeSpan.FromSeconds((double)p![0]!),
                    (int)p[1]!)),
            ]);
        }

        return new TrainingActivity(
            (string)a["id"]!,
            DateTimeOffset.Parse((string)a["start"]!, CultureInfo.InvariantCulture),
            TimeSpan.FromSeconds((double)a["movingSeconds"]!),
            Enum.Parse<ActivityType>((string)a["type"]!),
            series);
    }

    public static DateTimeOffset Instant(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    public static TimeSpan ParseOffset(string text) =>
        DateTimeOffset.Parse($"2000-01-01T00:00:00{text}", CultureInfo.InvariantCulture).Offset;
}

/// <summary>
///   The reference's FixedLocalClock, pinned in both respects: the instant and the zone.
/// </summary>
internal sealed class FixtureClock(DateTimeOffset utcNow, TimeSpan offset) : TimeProvider
{
    private readonly TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone(
        "ParityGenerator/Fixture",
        offset,
        "Fixture",
        "Fixture");

    public override DateTimeOffset GetUtcNow() => utcNow;

    public override TimeZoneInfo LocalTimeZone => zone;
}

/// <summary>The reference's SqliteFixture: one open :memory: connection for the database's life.</summary>
internal sealed class MemoryDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public MemoryDatabase()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var context = NewContext();
        context.Database.Migrate();
    }

    public ImportDbContext NewContext() => new(
        new DbContextOptionsBuilder<ImportDbContext>().UseSqlite(connection).Options);

    public void Dispose() => connection.Dispose();
}

/// <summary>
///   Replays a scenario's exchanges strictly in order and records each request's method and URL.
///   A request that does not match the next exchange is a fixture error and fails the run.
/// </summary>
internal sealed class ReplayHandler(SyncScenario scenario) : HttpMessageHandler
{
    private int next;

    public List<(string Method, string Url)> Requests { get; } = [];

    public int Remaining => scenario.Exchanges.Count - next;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var method = request.Method.Method;
        var url = request.RequestUri!.ToString();
        Requests.Add((method, url));

        if (next >= scenario.Exchanges.Count)
        {
            throw new InvalidOperationException(
                $"{scenario.Name}: unexpected request {method} {url} after the last recorded exchange.");
        }

        var exchange = scenario.Exchanges[next++]!.AsObject();

        if ((string)exchange["method"]! != method || (string)exchange["url"]! != url)
        {
            throw new InvalidOperationException(
                $"{scenario.Name}: request {method} {url} does not match recorded exchange "
                    + $"{exchange["method"]} {exchange["url"]}.");
        }

        var recorded = exchange["response"]!.AsObject();

        if ((bool?)recorded["drop"] == true)
        {
            throw new HttpRequestException("Connection dropped (recorded).");
        }

        var response = new HttpResponseMessage((HttpStatusCode)(int)recorded["status"]!)
        {
            Content = new StringContent(
                File.ReadAllText(Path.Combine(scenario.Directory, (string)recorded["body"]!)),
                System.Text.Encoding.UTF8,
                "application/json"),
        };

        foreach (var (name, value) in recorded["headers"]?.AsObject() ?? [])
        {
            response.Headers.TryAddWithoutValidation(name, (string)value!);
        }

        return Task.FromResult(response);
    }
}

/// <summary>How every golden is written: invariant, round-trip, and readable.</summary>
internal static class GoldenJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Decimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Double(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Utc(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);

    public static string WithOffset(DateTimeOffset instant) =>
        instant.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);

    public static void Write(string path, JsonNode node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, node.ToJsonString(Options) + "\n");
    }
}
