using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;
using TrainingLoadAnalyzer.Web.Components.Dashboard;
using TrainingLoadAnalyzer.Web.Components.Pages;
using TrainingLoadAnalyzer.Web.Features.Dashboard;
using TrainingLoadAnalyzer.Web.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   What only rendering can assert: an order, a badge, a rounding, an empty state. Everything with
///   a right answer that is <em>not</em> about markup lives in the read model and is tested without
///   a renderer (research R11).
/// </summary>
/// <remarks>
///   Derives from <c>BunitContext</c>, not <c>TestContext</c>: under xUnit v3 the latter is
///   ambiguous with <c>Xunit.TestContext</c> (CS0104), which every bUnit 1.x tutorial still shows
///   (research R10).
/// </remarks>
public class DashboardComponentTests : BunitContext
{
    private readonly SqliteFixture<ImportDbContext> fixture = new(options => new ImportDbContext(options));

    private readonly FixedLocalClock clock = new();

    /// <summary>
    ///   Registers the page's one dependency over a real SQLite database. The page fetches; the
    ///   five panels below it take a <c>DashboardView</c> and reach for nothing (C102).
    /// </summary>
    private void SeedAndRegister(params TrainingActivity[] activities)
    {
        using (var db = fixture.NewContext())
        {
            var store = new ActivityStore(db);

            foreach (var activity in activities)
            {
                store.UpsertAsync(activity, false, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        Services.AddSingleton<TimeProvider>(clock);
        Services.AddSingleton(new AthleteSettings(Fixtures.MaximumHeartRate));
        Services.AddSingleton<IDbContextFactory<ImportDbContext>>(new FixtureContextFactory(fixture));
        Services.AddSingleton(sp => new DashboardReader(
            sp.GetRequiredService<IDbContextFactory<ImportDbContext>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<AthleteSettings>(),
            NullLogger<DashboardReader>.Instance));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fixture.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>FR-001 - FR-003: a tile shows what it is and what it reads.</summary>
    [Fact]
    public void A_metric_tile_shows_its_label_and_its_figure()
    {
        var tile = Render<MetricTile>(p => p
            .Add(c => c.Label, "Fitness")
            .Add(c => c.Value, 2.8233976017308082));

        Assert.Contains("Fitness", tile.Markup, StringComparison.Ordinal);
        Assert.Contains("2.8", tile.Markup, StringComparison.Ordinal);
    }

    /// <summary>C100, US1 scenario 2: nothing to show is a dash, not a confident zero.</summary>
    [Fact]
    public void A_metric_tile_with_no_figure_shows_a_dash()
    {
        var tile = Render<MetricTile>(p => p.Add(c => c.Label, "Fitness"));

        Assert.Contains(Display.Missing, tile.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0", tile.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   003 FR-014 reaches the page: a figure with fewer than 42 days behind it still carries the
    ///   zero it started from, and must not be presented as though it had settled.
    /// </summary>
    [Fact]
    public void A_figure_that_has_not_warmed_up_says_so()
    {
        var warming = Render<MetricTile>(p => p
            .Add(c => c.Label, "Fitness").Add(c => c.Value, 2.8).Add(c => c.IsReliable, false));

        var settled = Render<MetricTile>(p => p
            .Add(c => c.Label, "Fitness").Add(c => c.Value, 45.3).Add(c => c.IsReliable, true));

        Assert.Contains("settling", warming.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("settling", settled.Markup, StringComparison.Ordinal);
    }

    /// <summary>US1 scenario 1: the three figures fixture H1 produces, on the page.</summary>
    [Fact]
    public void The_dashboard_shows_fitness_fatigue_and_form()
    {
        SeedAndRegister([.. Fixtures.H1]);

        var page = Render<Dashboard>();

        Assert.Contains("Fitness", page.Markup, StringComparison.Ordinal);
        Assert.Contains("2.8", page.Markup, StringComparison.Ordinal);
        Assert.Contains("16.0", page.Markup, StringComparison.Ordinal);
        Assert.Contains("-13.2", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   FR-011 and FR-017: a fresh installation is the ordinary first experience. The guidance is
    ///   a route, not a dead end — the athlete is sent somewhere they can act.
    /// </summary>
    [Fact]
    public void With_no_activities_and_no_connection_the_page_offers_a_way_to_connect()
    {
        SeedAndRegister();

        var page = Render<Dashboard>();

        Assert.Contains("No activities recorded", page.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/connect\"", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   FR-011 with a connection in place: the athlete has already connected, so telling them to
    ///   connect again would be wrong. The next step is a sync.
    /// </summary>
    [Fact]
    public void With_a_connection_but_no_activities_the_page_offers_a_sync()
    {
        using (var db = fixture.NewContext())
        {
            db.Connections.Add(new Infrastructure.Persistence.StravaConnection
            {
                AthleteId = 900001,
                AccessToken = "a",
                RefreshToken = "r",
                GrantedScopes = "activity:read_all",
            });
            db.SaveChanges();
        }

        SeedAndRegister();

        var page = Render<Dashboard>();

        Assert.Contains("No activities recorded", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Sync", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   The "metric calculations fail" edge case, in markup: "Data unavailable", not a stack trace
    ///   and not a page of confident zeroes (C87).
    /// </summary>
    [Fact]
    public void A_history_that_cannot_be_read_shows_data_unavailable_rather_than_tiles()
    {
        SeedAndRegister([.. Fixtures.H1]);
        fixture.Execute("UPDATE Activities SET Type = 'Swimming'");

        var page = Render<Dashboard>();

        Assert.Contains("Data unavailable", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("tile-value", page.Markup, StringComparison.Ordinal);
    }

    private static WeeklyTrainingLoad Week(decimal points) =>
        new(IsoWeek.For(Fixtures.Today), points, 4, LoadBasis.Estimated);

    private static WeeklyLoadTrend TrendOf(bool complete) =>
        new(IsoWeek.For(Fixtures.Today), 480m, 360m, complete, LoadBasis.Estimated);

    /// <summary>
    ///   FR-004, FR-005 and FR-005a together. Mid-week the change is shown and the judgement is
    ///   withheld — which is what the athlete should see on six days out of seven (research R14).
    /// </summary>
    [Fact]
    public void The_weekly_panel_shows_the_change_and_withholds_a_judgement_mid_week()
    {
        var panel = Render<WeeklyLoadPanel>(p => p
            .Add(c => c.Week, Week(480m))
            .Add(c => c.Trend, TrendOf(complete: false)));

        Assert.Contains("480.0", panel.Markup, StringComparison.Ordinal);
        Assert.Contains("+120.0", panel.Markup, StringComparison.Ordinal);
        Assert.Contains("+33%", panel.Markup, StringComparison.Ordinal);
        Assert.Contains("in progress", panel.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Significant", panel.Markup, StringComparison.Ordinal);
    }

    /// <summary>Once the week is complete, feature 004's judgement is shown as it stands.</summary>
    [Fact]
    public void The_weekly_panel_shows_the_judgement_once_the_week_is_complete()
    {
        var panel = Render<WeeklyLoadPanel>(p => p
            .Add(c => c.Week, Week(480m))
            .Add(c => c.Trend, TrendOf(complete: true)));

        Assert.Contains("Significant increase", panel.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("in progress", panel.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   C100, US2 scenario 3: with no previous week the comparison is a dash, and the weekly total
    ///   is still shown — the athlete's own training does not disappear for want of a comparison.
    /// </summary>
    [Fact]
    public void With_no_previous_week_the_comparison_is_a_dash_and_the_total_remains()
    {
        var panel = Render<WeeklyLoadPanel>(p => p.Add(c => c.Week, Week(480m)));

        Assert.Contains("480.0", panel.Markup, StringComparison.Ordinal);
        Assert.Contains(Display.Missing, panel.Markup, StringComparison.Ordinal);
    }

    private static IReadOnlyList<DailyTrainingMetrics> ChartSeries(int days) =>
    [
        .. Enumerable.Range(0, days).Select(i => new DailyTrainingMetrics(
            Fixtures.Today.AddDays(-(days - 1 - i)),
            40 + i,
            20 + (i * 2),
            i >= 42,
            LoadBasis.Estimated,
            LoadBasis.Estimated)),
    ];

    /// <summary>US3 scenario 1: three lines, drawn as markup and nothing else (research R8).</summary>
    [Fact]
    public void The_chart_renders_three_polylines_inside_an_svg()
    {
        var chart = Render<MetricsChartView>(p => p
            .Add(c => c.Metrics, ChartSeries(60))
            .Add(c => c.HasEnoughHistory, true));

        Assert.Contains("<svg", chart.Markup, StringComparison.Ordinal);
        Assert.Equal(3, chart.FindAll("polyline").Count);
    }

    /// <summary>
    ///   US3 scenario 3. The specification offers a legend as an alternative to a tooltip and this
    ///   takes it — three lines are useless if nobody can tell which is which.
    /// </summary>
    [Fact]
    public void The_chart_names_each_line_in_a_legend()
    {
        var chart = Render<MetricsChartView>(p => p
            .Add(c => c.Metrics, ChartSeries(60))
            .Add(c => c.HasEnoughHistory, true));

        Assert.Contains("Fitness", chart.Markup, StringComparison.Ordinal);
        Assert.Contains("Fatigue", chart.Markup, StringComparison.Ordinal);
        Assert.Contains("Form", chart.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   US3 scenario 2: under 30 days the athlete is told why there is no chart, rather than shown
    ///   a line between two points and left to draw the wrong conclusion from it.
    /// </summary>
    [Fact]
    public void Without_enough_history_the_chart_explains_itself_instead_of_drawing()
    {
        var chart = Render<MetricsChartView>(p => p
            .Add(c => c.Metrics, ChartSeries(12))
            .Add(c => c.HasEnoughHistory, false));

        Assert.Contains("Not enough data to show trends (30+ days required)", chart.Markup, StringComparison.Ordinal);
        Assert.Empty(chart.FindAll("polyline"));
    }

    private static RecentActivity Entry(
        int daysAgo,
        LoadProvenance provenance,
        ActivityType type = ActivityType.Running,
        int minutes = 60) =>
        new(
            Fixtures.Today.AddDays(-daysAgo),
            type,
            TimeSpan.FromMinutes(minutes),
            new TrainingLoad(minutes * 2, provenance));

    /// <summary>US4 scenario 1: date, type, moving time and load, newest first.</summary>
    [Fact]
    public void The_recent_list_shows_each_sessions_date_type_duration_and_load()
    {
        var list = Render<RecentActivityList>(p => p.Add(c => c.Activities,
        [
            Entry(0, LoadProvenance.Measured, ActivityType.Cycling, 45),
            Entry(2, LoadProvenance.Estimated, ActivityType.Running, 90),
        ]));

        Assert.Contains("2026-09-18", list.Markup, StringComparison.Ordinal);
        Assert.Contains("Cycling", list.Markup, StringComparison.Ordinal);
        Assert.Contains("45m", list.Markup, StringComparison.Ordinal);
        Assert.Contains("90.0", list.Markup, StringComparison.Ordinal);
        Assert.Contains("1h 30m", list.Markup, StringComparison.Ordinal);

        // Newest first, so the first row's date appears before the second's in the markup.
        Assert.True(
            list.Markup.IndexOf("2026-09-18", StringComparison.Ordinal)
                < list.Markup.IndexOf("2026-09-16", StringComparison.Ordinal),
            "The newest session must appear first (SC-004).");
    }

    /// <summary>
    ///   Discriminating check for C101, US4 scenarios 2 and 3. A measured load and an estimated one
    ///   differ in the rendered <em>content</em>, not only in a CSS class. FR-008 says the dashboard
    ///   must indicate which; a difference only a stylesheet expresses is invisible to a screen
    ///   reader, and invisible to anyone reading the page in a colour they cannot distinguish.
    /// </summary>
    [Fact]
    public void Measured_and_estimated_loads_differ_in_the_markup_not_only_in_styling()
    {
        var measured = Render<RecentActivityList>(p => p.Add(c => c.Activities, [Entry(0, LoadProvenance.Measured)]));
        var estimated = Render<RecentActivityList>(p => p.Add(c => c.Activities, [Entry(0, LoadProvenance.Estimated)]));

        static string WithoutClasses(string markup) =>
            System.Text.RegularExpressions.Regex.Replace(markup, "class=\"[^\"]*\"", string.Empty);

        Assert.NotEqual(WithoutClasses(measured.Markup), WithoutClasses(estimated.Markup));
        Assert.Contains("measured", measured.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("estimated", estimated.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>US4 scenario 4: an empty list says so rather than rendering an empty table.</summary>
    [Fact]
    public void An_empty_recent_list_says_so()
    {
        var list = Render<RecentActivityList>(p => p.Add(c => c.Activities, []));

        Assert.Empty(list.FindAll("li"));
    }
}
