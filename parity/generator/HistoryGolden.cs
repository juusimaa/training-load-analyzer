using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Strava;
using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Web;
using TrainingLoadAnalyzer.Web.Components.Dashboard;
using TrainingLoadAnalyzer.Web.Components.Pages;
using TrainingLoadAnalyzer.Web.Features.Dashboard;
using TrainingLoadAnalyzer.Web.Features.Sync;

namespace ParityGenerator;

/// <summary>
///   Writes <c>golden/histories/&lt;name&gt;.json</c> (parity.md §3): what the reference computes,
///   builds, lays out and renders for one history fixture.
/// </summary>
internal static class HistoryGolden
{
    private const double Width = 1000;
    private const double Height = 300;
    private const int AxisTickCount = 6;
    private static readonly int[] Windows = [180, 90, 30];

    public static async Task<JsonObject> BuildAsync(HistoryFixture fixture)
    {
        var activities = fixture.Activities;
        var today = fixture.Today;
        var maximum = fixture.MaximumHeartRate;

        // The span the aggregates are taken over: the view's 180 days, widened to cover every
        // activity so that nothing in the fixture falls outside what is compared.
        var days = activities.Select(DayOf).ToList();
        var start = days.Count > 0 && days.Min() < today.AddDays(-179) ? days.Min() : today.AddDays(-179);
        var end = days.Count > 0 && days.Max() > today ? days.Max() : today;
        var range = new DateRange(start, end);

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, maximum);
        var weekly = TrainingLoadAggregator.AggregateWeekly(activities, range, maximum);
        var metrics = TrainingMetricsCalculator.Calculate(daily, range);

        // Every week that has a predecessor inside the weekly series.
        var trendStart = weekly[0].Week.Monday.AddDays(7);
        var trends = trendStart <= end
            ? TrainingLoadTrendCalculator.Calculate(weekly, new DateRange(trendStart, end))
            : [];

        var view = DashboardViewBuilder.Build(activities, today, maximum, fixture.IsStravaConnected);

        var golden = new JsonObject
        {
            ["name"] = fixture.Name,
            ["range"] = new JsonObject { ["start"] = GoldenJson.Day(start), ["end"] = GoldenJson.Day(end) },
            ["loads"] = new JsonArray([.. activities.Select(a =>
            {
                var load = a.CalculateTrainingLoad(maximum);

                return (JsonNode)new JsonObject
                {
                    ["id"] = a.ExternalId,
                    ["points"] = GoldenJson.Decimal(load.Points),
                    ["provenance"] = load.Provenance.ToString(),
                };
            })]),
            ["daily"] = new JsonArray([.. daily.Select(d => (JsonNode)new JsonObject
            {
                ["day"] = GoldenJson.Day(d.Day),
                ["points"] = GoldenJson.Decimal(d.Points),
                ["count"] = d.ActivityCount,
                ["basis"] = d.Basis.ToString(),
            })]),
            ["weekly"] = new JsonArray([.. weekly.Select(w => (JsonNode)new JsonObject
            {
                ["year"] = w.Week.Year,
                ["week"] = w.Week.Week,
                ["monday"] = GoldenJson.Day(w.Week.Monday),
                ["points"] = GoldenJson.Decimal(w.Points),
                ["count"] = w.ActivityCount,
                ["basis"] = w.Basis.ToString(),
            })]),
            ["metrics"] = new JsonArray([.. metrics.Select(m => (JsonNode)new JsonObject
            {
                ["day"] = GoldenJson.Day(m.Day),
                ["fitness"] = GoldenJson.Double(m.Fitness),
                ["fatigue"] = GoldenJson.Double(m.Fatigue),
                ["form"] = GoldenJson.Double(m.Form),
                ["isReliable"] = m.IsReliable,
                ["fitnessBasis"] = m.FitnessBasis.ToString(),
                ["fatigueBasis"] = m.FatigueBasis.ToString(),
                ["formBasis"] = m.FormBasis.ToString(),
            })]),
            ["trends"] = new JsonArray([.. trends.Select(Trend)]),
            ["view"] = View(view),
            ["geometry"] = Geometry(view),
            ["renderedText"] = await RenderedTextAsync(fixture, view),
        };

        return golden;
    }

    private static JsonNode Trend(WeeklyLoadTrend t) => new JsonObject
    {
        ["year"] = t.Week.Year,
        ["week"] = t.Week.Week,
        ["monday"] = GoldenJson.Day(t.Week.Monday),
        ["points"] = GoldenJson.Decimal(t.Points),
        ["previous"] = GoldenJson.Decimal(t.PreviousPoints),
        ["absoluteChange"] = GoldenJson.Decimal(t.AbsoluteChange),
        ["relativeChange"] = t.RelativeChange is { } r ? GoldenJson.Decimal(r) : null,
        ["isComplete"] = t.IsComplete,
        ["basis"] = t.Basis.ToString(),
        ["classification"] = t.Classification.ToString(),
    };

    /// <summary>The view projected to exactly http-api.md §2, every string through Display.</summary>
    public static JsonObject View(DashboardView view)
    {
        var current = view.Current;

        return new JsonObject
        {
            ["asOf"] = Display.Day(view.AsOf),
            ["isoWeek"] = view.IsoWeek,
            ["maximumHeartRate"] = view.MaximumHeartRate.ToString(CultureInfo.InvariantCulture),
            ["isStravaConnected"] = view.IsStravaConnected,
            ["isUnavailable"] = view.IsUnavailable,
            ["hasActivities"] = view.HasActivities,
            ["hasEnoughHistoryForChart"] = view.HasEnoughHistoryForChart,
            ["current"] = current is not { } c ? null : new JsonObject
            {
                ["fitness"] = Figure(c.Fitness, c, c.FitnessBasis),
                ["fatigue"] = Figure(c.Fatigue, c, c.FatigueBasis),
                ["form"] = Figure(c.Form, c, c.FormBasis),
            },
            ["week"] = new JsonObject
            {
                ["points"] = Display.Points(view.CurrentWeek?.Points),
                ["trend"] = view.Trend is not { } t ? null : new JsonObject
                {
                    ["change"] = (t.AbsoluteChange >= 0 ? "+" : "") + Display.Points(t.AbsoluteChange),
                    ["percent"] = Display.Percent(t.RelativeChange),
                    ["judgement"] = t.Classification switch
                    {
                        TrendClassification.SignificantIncrease => "Significant increase",
                        TrendClassification.SignificantDecrease => "Significant decrease",
                        TrendClassification.Steady => "Steady",
                        _ => "Week in progress",
                    },
                },
            },
            ["days"] = new JsonArray([.. view.Metrics.Select((m, i) =>
            {
                var load = view.DailyLoad[i];

                return (JsonNode)new JsonObject
                {
                    ["day"] = Display.Day(m.Day),
                    ["fitness"] = m.Fitness,
                    ["fatigue"] = m.Fatigue,
                    ["form"] = m.Form,
                    ["load"] = load.Points,
                    ["display"] = new JsonObject
                    {
                        ["fitness"] = Display.Metric(m.Fitness),
                        ["fatigue"] = Display.Metric(m.Fatigue),
                        ["form"] = Display.Metric(m.Form),
                        ["load"] = Display.Points(load.Points),
                    },
                };
            })]),
            ["recent"] = new JsonArray([.. view.Recent.Select(r => (JsonNode)new JsonObject
            {
                ["day"] = Display.Day(r.Day),
                ["type"] = r.Type.ToString(),
                ["movingTime"] = Display.Duration(r.MovingTime),
                ["provenance"] = r.Load.Provenance == LoadProvenance.Measured ? "measured" : "estimated",
                ["load"] = Display.Points(r.Load.Points),
            })]),
        };
    }

    /// <summary>A port of MetricRow.Qualifiers: settling first, then the basis.</summary>
    private static JsonObject Figure(double value, DailyTrainingMetrics current, LoadBasis basis)
    {
        var qualifiers = new JsonArray();

        if (!current.IsReliable)
        {
            qualifiers.Add("still settling");
        }

        if (basis is LoadBasis.Estimated or LoadBasis.Mixed)
        {
            qualifiers.Add(basis == LoadBasis.Mixed ? "partly estimated" : "estimated");
        }

        return new JsonObject { ["value"] = Display.Metric(value), ["qualifiers"] = qualifiers };
    }

    private static IReadOnlyList<T> Windowed<T>(IReadOnlyList<T> series, int windowDays) =>
        series.Count <= windowDays ? series : [.. series.Skip(series.Count - windowDays)];

    private static JsonObject Geometry(DashboardView view)
    {
        var geometry = new JsonObject();

        foreach (var window in Windows)
        {
            var metrics = Windowed(view.Metrics, window);
            var loads = Windowed(view.DailyLoad, window);
            var slots = MetricsChart.HoverSlots(metrics, loads);

            geometry[window.ToString(CultureInfo.InvariantCulture)] = new JsonObject
            {
                ["viewBox"] = MetricsChart.ViewBox(Width, Height),
                ["plot"] = new JsonObject(MetricsChart.Plot(metrics, Width, Height)
                    .Select(s => KeyValuePair.Create<string, JsonNode?>(s.Label, s.Points))),
                ["plotClasses"] = new JsonArray([.. MetricsChart.Plot(metrics, Width, Height)
                    .Select(s => (JsonNode)s.CssClass)]),
                ["bars"] = new JsonArray([.. MetricsChart.LoadBars(loads, Width, Height)
                    .Select(b => (JsonNode)new JsonArray(b.X, b.Y, b.Width, b.Height))]),
                ["zeroRule"] = MetricsChart.ZeroRule(metrics, Width, Height),
                ["ticks"] = new JsonArray([.. MetricsChart.AxisTicks(metrics, AxisTickCount)
                    .Select(t => (JsonNode)t)]),
                ["slots"] = new JsonArray([.. slots.Select((s, index) => (JsonNode)new JsonObject
                {
                    ["day"] = s.Day,
                    ["left"] = s.Left,
                    ["width"] = s.Width,
                    ["barTop"] = s.BarTop,
                    ["barHeight"] = s.BarHeight,
                    ["load"] = s.Load,
                    ["opensLeft"] = index > slots.Count * 0.55,
                    ["points"] = new JsonArray([.. s.Points.Select(p =>
                        (JsonNode)new JsonArray(p.Label, p.Value, p.Top))]),
                })]),
            };
        }

        return geometry;
    }

    private static async Task<JsonObject> RenderedTextAsync(HistoryFixture fixture, DashboardView view)
    {
        var plain = Rendering.Plain();
        var text = new JsonObject
        {
            ["metricRow"] = Rendering.Text(await Rendering.RenderAsync<MetricRow>(plain, new Dictionary<string, object?>
            {
                [nameof(MetricRow.Current)] = view.Current,
                [nameof(MetricRow.Week)] = view.CurrentWeek,
                [nameof(MetricRow.Trend)] = view.Trend,
            })),
            ["recent"] = Rendering.Text(await Rendering.RenderAsync<RecentActivityList>(plain, new Dictionary<string, object?>
            {
                [nameof(RecentActivityList.Activities)] = view.Recent,
            })),
        };

        foreach (var window in Windows)
        {
            text[$"chart{window}"] = Rendering.Text(await Rendering.RenderAsync<MetricsChartView>(plain, new Dictionary<string, object?>
            {
                [nameof(MetricsChartView.Metrics)] = Windowed(view.Metrics, window),
                [nameof(MetricsChartView.DailyLoad)] = Windowed(view.DailyLoad, window),
                [nameof(MetricsChartView.HasEnoughHistory)] = view.HasEnoughHistoryForChart,
                [nameof(MetricsChartView.WindowDays)] = window,
            }));
        }

        // The page itself, over a real SQLite store seeded with the fixture and read by the real
        // DashboardReader, as the reference's DashboardRenderContext does.
        using var database = new MemoryDatabase();
        Seed(database, fixture);

        var clock = new FixtureClock(
            new DateTimeOffset(fixture.Today.ToDateTime(new TimeOnly(7, 0)), fixture.Offset),
            fixture.Offset);

        var page = Rendering.RenderPage<Dashboard>(
            services => RegisterDashboard(services, database, clock, fixture.MaximumHeartRate, pending: false),
            settled: true);

        text["rail"] = Rendering.Text(Rendering.Between(page, "<aside class=\"rail\"", "</aside>"));
        text["content"] = Rendering.Text(Rendering.Between(page, "<section class=\"content\"", "</main>"));

        return text;
    }

    public static void Seed(MemoryDatabase database, HistoryFixture fixture)
    {
        using var db = database.NewContext();
        var store = new ActivityStore(db);

        foreach (var activity in fixture.Activities)
        {
            store.UpsertAsync(activity, false, CancellationToken.None).GetAwaiter().GetResult();
        }

        if (fixture.IsStravaConnected)
        {
            db.Connections.Add(new StravaConnection
            {
                AthleteId = 900001,
                AccessToken = "unused",
                RefreshToken = "unused",
                GrantedScopes = "read,activity:read_all",
            });
            db.SaveChanges();
        }
    }

    /// <summary>The page's dependencies, registered as the reference's DashboardRenderContext does.</summary>
    public static void RegisterDashboard(
        IServiceCollection services,
        MemoryDatabase database,
        TimeProvider clock,
        int maximumHeartRate,
        bool pending)
    {
        services.AddLogging();
        services.AddSingleton(clock);
        services.AddSingleton(new AthleteSettings(maximumHeartRate));
        services.AddSingleton<IDbContextFactory<ImportDbContext>>(
            pending ? new PendingContextFactory(database) : new DatabaseContextFactory(database));
        services.AddSingleton(sp => new DashboardReader(
            sp.GetRequiredService<IDbContextFactory<ImportDbContext>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<AthleteSettings>(),
            sp.GetRequiredService<ILogger<DashboardReader>>()));
        services.AddSingleton(new StravaApiClient(new HttpClient()));
        services.AddScoped<ActivityStore>();
        services.AddScoped(_ => database.NewContext());
        services.AddScoped<StravaActivitySync>();
        services.AddSingleton<SyncCoordinator>();
    }

    private static DateOnly DayOf(TrainingActivity activity) => DateOnly.FromDateTime(activity.StartedAt.DateTime);

    private sealed class DatabaseContextFactory(MemoryDatabase database) : IDbContextFactory<ImportDbContext>
    {
        public ImportDbContext CreateDbContext() => database.NewContext();
    }

    /// <summary>The reference's PendingContextFactory: a read that never answers.</summary>
    private sealed class PendingContextFactory(MemoryDatabase database) : IDbContextFactory<ImportDbContext>
    {
        public ImportDbContext CreateDbContext() => database.NewContext();

        public async Task<ImportDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);

            return database.NewContext();
        }
    }
}
