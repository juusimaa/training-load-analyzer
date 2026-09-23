using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Web.Components.Dashboard;
using TrainingLoadAnalyzer.Web.Components.Pages;
using TrainingLoadAnalyzer.Web.Features.Dashboard;
using TrainingLoadAnalyzer.Web.Features.Sync;

namespace ParityGenerator;

/// <summary>
///   Writes <c>golden/probes/display.json</c> (every Display helper and SyncMessage row on probe
///   inputs) and <c>golden/probes/surfaces.json</c> (the surfaces that depend on no history).
/// </summary>
internal static partial class ProbeGolden
{
    /// <summary>The athlete's offset in every probe: +03:00, as the reference's FixedLocalClock.</summary>
    private static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    private static readonly DateTimeOffset FinishedAt = new(2026, 9, 18, 7, 7, 0, Offset);

    private static readonly DateTimeOffset RetryAfterLocal = new(2026, 9, 18, 7, 15, 0, Offset);

    public static JsonObject Display(string fixturePath)
    {
        var probes = JsonNode.Parse(File.ReadAllText(fixturePath))!.AsObject();

        return new JsonObject
        {
            ["metric"] = Rows(probes["metric"]!, text =>
                TrainingLoadAnalyzer.Web.Features.Dashboard.Display.Metric(
                    double.Parse(text, CultureInfo.InvariantCulture))),
            ["points"] = Rows(probes["points"]!, text =>
                TrainingLoadAnalyzer.Web.Features.Dashboard.Display.Points(
                    decimal.Parse(text, CultureInfo.InvariantCulture))),
            ["percent"] = Rows(probes["percent"]!, text =>
                TrainingLoadAnalyzer.Web.Features.Dashboard.Display.Percent(
                    decimal.Parse(text, CultureInfo.InvariantCulture))),
            // MetricRow's caption rule: a "+" for a change of zero or more, then the decimal.
            ["weekChange"] = Rows(probes["weekChange"]!, text =>
            {
                var change = decimal.Parse(text, CultureInfo.InvariantCulture);

                return (change >= 0 ? "+" : "")
                    + TrainingLoadAnalyzer.Web.Features.Dashboard.Display.Points(change);
            }),
            ["duration"] = new JsonArray([.. probes["durationSeconds"]!.AsArray().Select(s => (JsonNode)new JsonObject
            {
                ["seconds"] = (int)s!,
                ["output"] = TrainingLoadAnalyzer.Web.Features.Dashboard.Display.Duration(
                    TimeSpan.FromSeconds((int)s!)),
            })]),
            ["day"] = Rows(probes["days"]!, text =>
                TrainingLoadAnalyzer.Web.Features.Dashboard.Display.Day(
                    DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture))),
            ["missing"] = TrainingLoadAnalyzer.Web.Features.Dashboard.Display.Missing,
            ["syncMessage"] = new JsonArray([.. StatusRows().Select(row => (JsonNode)new JsonObject
            {
                ["row"] = row.Name,
                ["status"] = Describe(row.Status),
                ["message"] = SyncMessage.For(row.Status),
            })]),
        };
    }

    public static async Task<JsonObject> SurfacesAsync()
    {
        var plain = Rendering.Plain();

        // The loading state: the page over a read that never answers. Its rail shows the machine's
        // own date, since the view it would read the date from has not arrived, so the date and its
        // ISO week are replaced by placeholders the React test fills in from the browser's clock.
        using var database = new MemoryDatabase();
        var clock = new FixtureClock(new DateTimeOffset(2026, 9, 18, 4, 0, 0, TimeSpan.Zero), Offset);
        var loading = Rendering.RenderPage<Dashboard>(
            services => HistoryGolden.RegisterDashboard(services, database, clock, 190, pending: true),
            settled: false);

        var asOf = DateOnly.FromDateTime(DateTime.Now);
        var rail = Rendering.Text(Rendering.Between(loading, "<aside class=\"rail\"", "</aside>"))
            .Replace(TrainingLoadAnalyzer.Web.Features.Dashboard.Display.Day(asOf), "{asOf}", StringComparison.Ordinal)
            .Replace(IsoWeekDesignation(asOf), "{isoWeek}", StringComparison.Ordinal);

        var panels = new JsonObject();

        foreach (var row in StatusRows())
        {
            panels[row.Name] = Rendering.Text(await Rendering.RenderAsync<SyncPanel>(plain, new Dictionary<string, object?>
            {
                [nameof(SyncPanel.Status)] = row.Status,
            }));
        }

        // Error.razor reads the request id from Activity.Current or the HttpContext, and there is
        // neither here: the SPA never has one either. Its "Development Mode" block is not ported
        // (Amendment 1(c)), so it is stripped before the text is taken.
        System.Diagnostics.Activity.Current = null;
        var error = DevelopmentMode().Replace(await Rendering.RenderAsync<Error>(plain), string.Empty);

        if (error.Contains("Development Mode", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Error.razor's Development Mode block was not stripped.");
        }

        return new JsonObject
        {
            ["loading"] = new JsonObject
            {
                ["rail"] = rail,
                ["content"] = Rendering.Text(Rendering.Between(loading, "<section class=\"content\"", "</main>")),
            },
            ["syncPanel"] = panels,
            ["notFound"] = Rendering.Text(await Rendering.RenderAsync<NotFound>(plain)),
            ["error"] = Rendering.Text(error),
        };
    }

    private static JsonArray Rows(JsonNode inputs, Func<string, string> format) =>
        new([.. inputs.AsArray().Select(i => (JsonNode)new JsonObject
        {
            ["input"] = (string)i!,
            ["output"] = format((string)i!),
        })]);

    /// <summary>One status per row of the message table in http-api.md §3.</summary>
    private static IEnumerable<(string Name, SyncStatus Status)> StatusRows()
    {
        yield return ("running", new SyncStatus { IsRunning = true });
        yield return ("notConnected", new SyncStatus { Failure = SyncMessage.ConnectionRequired, FinishedAt = FinishedAt });
        yield return ("never", SyncStatus.Never);
        yield return ("completedImported", Finished(new SyncResult { Outcome = SyncOutcome.Completed, Imported = 3 }));
        yield return ("completedNothingNew", Finished(new SyncResult { Outcome = SyncOutcome.Completed, Imported = 0, Updated = 2 }));
        yield return ("rateLimitedKnown", Finished(
            new SyncResult { Outcome = SyncOutcome.RateLimited, Imported = 1, RetryAfter = RetryAfterLocal.ToUniversalTime() },
            RetryAfterLocal));
        yield return ("rateLimitedUnknown", Finished(new SyncResult { Outcome = SyncOutcome.RateLimited }));
        yield return ("interrupted", Finished(new SyncResult { Outcome = SyncOutcome.Interrupted }));
        yield return ("reconnectionRequired", Finished(new SyncResult { Outcome = SyncOutcome.ReconnectionRequired }));
        yield return ("refused", Finished(new SyncResult { Outcome = SyncOutcome.Refused }));
        yield return ("unrecognised", Finished(new SyncResult { Outcome = (SyncOutcome)99 }));
    }

    private static SyncStatus Finished(SyncResult result, DateTimeOffset? retryAfterLocal = null) => new()
    {
        Result = result,
        FinishedAt = FinishedAt,
        RetryAfterLocal = retryAfterLocal,
    };

    private static JsonObject Describe(SyncStatus status) => new()
    {
        ["isRunning"] = status.IsRunning,
        ["failure"] = status.Failure,
        ["outcome"] = status.Result is { } r
            ? (Enum.IsDefined(r.Outcome) ? r.Outcome.ToString() : "Unrecognised")
            : null,
        ["imported"] = status.Result?.Imported,
        ["updated"] = status.Result?.Updated,
        ["finishedAt"] = status.FinishedAt is { } f ? GoldenJson.WithOffset(f) : null,
        ["retryAfterLocal"] = status.RetryAfterLocal is { } t ? GoldenJson.WithOffset(t) : null,
    };

    private static string IsoWeekDesignation(DateOnly day)
    {
        var week = TrainingLoadAnalyzer.Domain.IsoWeek.For(day);

        return string.Create(CultureInfo.InvariantCulture, $"{week.Year}-W{week.Week:00}");
    }

    [GeneratedRegex("""<div class="state"[^>]*>\s*<h3[^>]*>Development Mode</h3>.*?</div>""", RegexOptions.Singleline)]
    private static partial Regex DevelopmentMode();
}
