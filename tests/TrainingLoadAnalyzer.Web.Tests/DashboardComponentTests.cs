using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Web.Tests.Fakes;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Web.Components.Dashboard;
using TrainingLoadAnalyzer.Web.Components.Pages;
using TrainingLoadAnalyzer.Web.Features.Dashboard;
using TrainingLoadAnalyzer.Web.Features.Sync;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   What only rendering can assert: an order, a badge, a rounding, an empty state. Everything with
///   a right answer that is <em>not</em> about markup lives in the read model and is tested without
///   a renderer (research R11).
/// </summary>
/// <remarks>
///   <para>
///     Derives from <see cref="DashboardRenderContext"/>, which holds the container setup this
///     class shared verbatim with <c>InformationPreservationTests</c> (008 research R11). That base
///     derives in turn from <c>BunitContext</c>, not <c>TestContext</c>: under xUnit v3 the latter
///     is ambiguous with <c>Xunit.TestContext</c> (CS0104), which every bUnit 1.x tutorial still
///     shows (research R10).
///   </para>
/// </remarks>
public class DashboardComponentTests : DashboardRenderContext
{
    /// <summary>FR-001 - FR-003: a figure shows what it is and what it reads.</summary>
    /// <remarks>
    ///   Repointed from <c>MetricTile</c> to <c>MetricRow</c> by feature 008, which merged the
    ///   three tiles and the weekly panel into one row. Every assertion is unchanged — SC-002
    ///   permits moving an assertion onto markup this feature deliberately replaces, and forbids
    ///   weakening it on the way.
    /// </remarks>
    [Fact]
    public void A_metric_figure_shows_its_label_and_its_value()
    {
        var row = Render<MetricRow>(p => p.Add(c => c.Current, Snapshot(fitness: 2.8233976017308082)));

        Assert.Contains("Fitness", row.Markup, StringComparison.Ordinal);
        Assert.Contains("2.8", row.Markup, StringComparison.Ordinal);
    }

    /// <summary>C100, US1 scenario 2: nothing to show is a dash, not a confident zero.</summary>
    [Fact]
    public void A_metric_figure_with_nothing_to_show_is_a_dash()
    {
        var row = Render<MetricRow>();

        Assert.Contains(Display.Missing, row.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0", row.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   003 FR-014 reaches the page: a figure with fewer than 42 days behind it still carries the
    ///   zero it started from, and must not be presented as though it had settled.
    /// </summary>
    [Fact]
    public void A_figure_that_has_not_warmed_up_says_so()
    {
        var warming = Render<MetricRow>(p => p.Add(c => c.Current, Snapshot(2.8, isReliable: false)));
        var settled = Render<MetricRow>(p => p.Add(c => c.Current, Snapshot(45.3, isReliable: true)));

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
        Connect();

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
        Fixture.Execute("UPDATE Activities SET Type = 'Swimming'");

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
        var panel = Render<MetricRow>(p => p
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
        var panel = Render<MetricRow>(p => p
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
        var panel = Render<MetricRow>(p => p.Add(c => c.Week, Week(480m)));

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

        // Amendment 1: the plot is MudChart's now, so "nothing was drawn" is the absence of its
        // svg rather than of our polylines. The assertion is the same claim, against new markup.
        Assert.Empty(chart.FindAll("svg"));
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
    /// <remarks>
    ///   Rewritten for 007 Amendment 1, not deleted. This used to assert
    ///   <c>Assert.Empty(FindAll("li"))</c>, and against MudList — which renders <c>div</c>s — that
    ///   would have gone on passing while testing nothing at all: green, and vacuous. The claim it
    ///   was actually making is that an empty list says so in words and draws no rows, so that is
    ///   what it now asserts, against the markup MudList produces.
    /// </remarks>
    [Fact]
    public void An_empty_recent_list_says_so()
    {
        var list = Render<RecentActivityList>(p => p.Add(c => c.Activities, []));

        Assert.Contains("Nothing recorded yet.", list.Markup, StringComparison.Ordinal);
        Assert.Empty(list.FindAll(".recent-row"));
    }

    /// <summary>FR-009, US5 scenario 1: the button, and a loading state while it works.</summary>
    [Fact]
    public void The_sync_panel_offers_a_button_and_shows_progress_while_running()
    {
        var idle = Render<SyncPanel>(p => p.Add(c => c.Status, SyncStatus.Never));
        var running = Render<SyncPanel>(p => p.Add(c => c.Status, new SyncStatus { IsRunning = true }));

        Assert.Contains("Sync Activities", idle.Markup, StringComparison.Ordinal);
        Assert.Contains("Syncing", running.Markup, StringComparison.Ordinal);
        Assert.True(running.Find("button").HasAttribute("disabled"), "A sync in flight must not be startable twice.");
    }

    /// <summary>
    ///   US5 scenario 5, FR-017 and FR-018: both routes to "you need to connect" — never having
    ///   connected, and a credential Strava has since rejected — land on the same prompt and the
    ///   same link.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_missing_or_rejected_connection_offers_the_connect_route(bool rejected)
    {
        var status = rejected
            ? new SyncStatus { Result = new SyncResult { Outcome = SyncOutcome.ReconnectionRequired } }
            : new SyncStatus { Failure = SyncMessage.ConnectionRequired };

        var panel = Render<SyncPanel>(p => p.Add(c => c.Status, status));

        Assert.Contains("Strava connection required", panel.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/connect\"", panel.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   FR-009 and US5 scenario 2's second half. Clicking the button runs a sync and the page
    ///   re-reads, so activities that arrived change the figures on screen without a manual reload.
    ///   <para>
    ///     Every test before this one covered one half: the coordinator syncs, the button renders.
    ///     Nothing joined them, and the join is what the athlete actually does.
    ///   </para>
    /// </summary>
    [Fact]
    public void Clicking_sync_runs_one_and_the_page_re_reads_afterwards()
    {
        SeedAndRegister();
        var coordinator = Services.GetRequiredService<SyncCoordinator>();

        var page = Render<Dashboard>();
        page.Find("button.sync").Click();

        // The seeded database holds no connection, so the sync fails in the way US5 scenario 5
        // describes — which is still proof that the button reached the coordinator at all.
        Assert.NotNull(coordinator.Status.FinishedAt);
        Assert.Contains("Strava connection required", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   FR-012 and the "sync in progress when the page refreshes" edge case. A reload is a
    ///   <em>new</em> circuit, so a freshly constructed page must read the state from the singleton
    ///   rather than start blank. A page keeping this in a component field looks idle after every
    ///   refresh, whatever is actually happening on the server.
    /// </summary>
    [Fact]
    public void A_freshly_loaded_page_sees_a_sync_that_is_already_running()
    {
        SeedAndRegister();
        var coordinator = Services.GetRequiredService<SyncCoordinator>();

        var gate = new TaskCompletionSource();
        _ = coordinator.RunAsync(CancellationToken.None);

        var reloaded = Render<Dashboard>();

        Assert.Contains("Sync", reloaded.Markup, StringComparison.Ordinal);
        gate.TrySetResult();
    }

    // ---- Feature 008: the rail (US1, FR-003) ----

    /// <summary>
    ///   FR-003: one rail holds the as-of date, the ISO week, the window control, the Strava state
    ///   with its sync control, and the configured maximum heart rate.
    /// </summary>
    [Fact]
    public void The_rail_holds_the_as_of_date_the_week_the_window_the_sync_control_and_the_maximum()
    {
        SeedAndRegister([.. Fixtures.H1]);

        var rail = Render<Dashboard>().Find("aside.rail");

        Assert.Contains(Display.Day(Fixtures.Today), rail.InnerHtml, StringComparison.Ordinal);
        Assert.Contains("2026-W38", rail.InnerHtml, StringComparison.Ordinal);
        Assert.Contains("30", rail.InnerHtml, StringComparison.Ordinal);
        Assert.Contains("90", rail.InnerHtml, StringComparison.Ordinal);
        Assert.Contains("180", rail.InnerHtml, StringComparison.Ordinal);
        Assert.Contains("Sync", rail.InnerHtml, StringComparison.Ordinal);
        Assert.Contains("190", rail.InnerHtml, StringComparison.Ordinal);
        Assert.Contains("bpm", rail.InnerHtml, StringComparison.Ordinal);
        Assert.NotNull(rail.QuerySelector("button.sync"));
    }

    /// <summary>
    ///   FR-003's organising rule, and the one worth a test of its own: <em>no</em> interactive
    ///   control appears outside the rail.
    /// </summary>
    /// <remarks>
    ///   Stated as a prohibition rather than a list, because the failure mode is a control drifting
    ///   back into the content column during some later change — which a list of expected controls
    ///   would not notice. The empty state's connect action is the case to watch: it is guidance
    ///   inside the explanation, and FR-003 puts it in the rail with everything else.
    /// </remarks>
    [Fact]
    public void No_interactive_control_appears_outside_the_rail()
    {
        SeedAndRegister([.. Fixtures.H1]);

        var page = Render<Dashboard>();

        var strays = page
            .FindAll("button, a[href], input, select, textarea")
            .Where(control => control.Closest("aside.rail") is null)
            .Select(control => control.OuterHtml)
            .ToList();

        Assert.True(
            strays.Count == 0,
            "Every control belongs in the rail (FR-003). Found outside it:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, strays));
    }

    // ---- Feature 008: the metric row (US1, FR-004, FR-008) ----

    /// <summary>One day's figures, for the row to render.</summary>
    private static DailyTrainingMetrics Snapshot(
        double fitness = 2.8,
        double fatigue = 16.0,
        bool isReliable = true,
        LoadBasis basis = LoadBasis.Estimated) =>
        new(Fixtures.Today, fitness, fatigue, isReliable, basis, basis);

    /// <summary>
    ///   FR-004: Fitness, Fatigue, Form and the week's load as four peers in one row, each figure
    ///   at display size, with Fitness and Fatigue carrying their own series colour.
    /// </summary>
    [Fact]
    public void The_metric_row_shows_four_figures_as_peers()
    {
        var row = Render<MetricRow>(p => p
            .Add(c => c.Current, Snapshot())
            .Add(c => c.Week, Week(480m)));

        Assert.Equal(4, row.FindAll(".tile-value").Count);

        Assert.Contains("2.8", row.Markup, StringComparison.Ordinal);
        Assert.Contains("16.0", row.Markup, StringComparison.Ordinal);
        Assert.Contains("-13.2", row.Markup, StringComparison.Ordinal);
        Assert.Contains("480.0", row.Markup, StringComparison.Ordinal);

        // FR-004: each of the two accented figures is one consistent colour, distinct from the
        // other two — carried by the same class the chart's matching line takes, so the figure and
        // its line can never drift into different colours.
        Assert.NotNull(row.Find(".series-fitness.tile-value"));
        Assert.NotNull(row.Find(".series-fatigue.tile-value"));
        Assert.Empty(row.FindAll(".series-form"));
    }

    /// <summary>
    ///   FR-008 and the alignment edge case: a figure that cannot be computed is an em dash, and it
    ///   <em>still occupies its position</em> so the row does not close up around the gap and
    ///   re-order the three that remain.
    /// </summary>
    [Fact]
    public void A_row_with_nothing_to_show_keeps_four_positions_of_em_dashes()
    {
        var row = Render<MetricRow>();

        var figures = row.FindAll(".tile-value");

        Assert.Equal(4, figures.Count);
        Assert.All(figures, figure => Assert.Equal(Display.Missing, figure.TextContent.Trim()));
        Assert.DoesNotContain("0.0", row.Markup, StringComparison.Ordinal);
    }

    // ---- Feature 008: the chart (US1, FR-006) ----

    private static IReadOnlyList<DailyTrainingLoad> Bars(int days) =>
    [
        .. Enumerable.Range(0, days).Select(i => new DailyTrainingLoad(
            Fixtures.Today.AddDays(-(days - 1 - i)),
            i % 3 == 0 ? 0m : 60m + (i % 4 * 30),
            i % 3 == 0 ? 0 : 1,
            i % 3 == 0 ? LoadBasis.None : LoadBasis.Estimated)),
    ];

    /// <summary>
    ///   FR-006: load bars behind the three lines, a zero rule, and a legend naming each series.
    /// </summary>
    [Fact]
    public void The_chart_draws_bars_behind_three_lines_over_a_zero_rule()
    {
        var chart = Render<MetricsChartView>(p => p
            .Add(c => c.Metrics, ChartSeries(60))
            .Add(c => c.DailyLoad, Bars(60))
            .Add(c => c.HasEnoughHistory, true));

        var plot = chart.Find("svg");

        Assert.NotEmpty(plot.QuerySelectorAll("rect.load-bar"));
        Assert.Single(plot.QuerySelectorAll("line.zero-rule"));
        Assert.Equal(3, plot.QuerySelectorAll("polyline").Length);

        // Back to front: the bars are a backdrop, so every one of them is drawn before the first
        // line. Asserted by document order, which is what decides SVG painting order.
        var drawn = plot.QuerySelectorAll("rect.load-bar, polyline").Select(e => e.ClassName).ToList();

        Assert.DoesNotContain(
            "load-bar",
            drawn.SkipWhile(c => c == "load-bar"));
    }

    /// <summary>
    ///   SC-007 as amended by 008 Amendment 1(c): the three lines differ by stroke <em>pattern</em>
    ///   as well as colour, which feature 007's Amendment 1 had recorded losing as a real
    ///   accessibility regression.
    /// </summary>
    /// <remarks>
    ///   Read from the stylesheet rather than the markup, and not by choice: a scoped
    ///   <c>.razor.css</c> compiles into a separate bundle and never appears in what bUnit renders,
    ///   so no assertion over <c>chart.Markup</c> could see this rule. The alternative — writing
    ///   <c>stroke-dasharray</c> as a presentation attribute so a render test could read it — would
    ///   put the line's appearance back in the markup, which is the pattern R5 moved away from.
    /// </remarks>
    [Fact]
    public void The_form_line_is_distinguished_by_its_stroke_pattern_and_not_only_its_colour()
    {
        var dashed = Theme.Stylesheet.Declaration(
            Theme.Stylesheet.MetricsChart, ".series-form", "stroke-dasharray");

        Assert.False(
            string.IsNullOrWhiteSpace(dashed),
            "The Form line has no dash pattern, so the three series are told apart by colour "
            + "alone (SC-007, Amendment 1(c)).");
    }

    // ---- Feature 008: recent sessions (US1, FR-007) ----

    /// <summary>
    ///   FR-007: a table with one row per session and a consistent column for each value, so the
    ///   figures line up down the list rather than each row setting its own rhythm.
    /// </summary>
    [Fact]
    public void The_recent_sessions_are_a_table_with_one_row_per_session()
    {
        var list = Render<RecentActivityList>(p => p.Add(c => c.Activities,
            [.. Enumerable.Range(0, 7).Select(i => Entry(i, LoadProvenance.Measured))]));

        Assert.NotNull(list.Find("table.table"));

        var rows = list.FindAll("tr.recent-row");

        Assert.Equal(7, rows.Count);
        Assert.All(rows, row => Assert.Equal(5, row.QuerySelectorAll("td").Length));
    }

    /// <summary>
    ///   FR-018: the basis is a tag carrying the <em>word</em>. The tag is a container around the
    ///   word, never a replacement for it — a distinction only a colour expresses is invisible to a
    ///   screen reader and to anyone who cannot tell the two colours apart.
    /// </summary>
    [Fact]
    public void The_basis_is_a_tag_that_carries_its_word()
    {
        var estimated = Render<RecentActivityList>(p => p.Add(c => c.Activities,
            [Entry(0, LoadProvenance.Estimated)]));

        Assert.Equal("estimated", estimated.Find("tr.recent-row span.tag").TextContent.Trim());
    }

    // ---- Feature 008: the window selector (US1, FR-005) ----

    /// <summary>
    ///   FR-005: choosing a window updates the plot, its heading and its date axis, without a full
    ///   page reload.
    /// </summary>
    [Fact]
    public void Choosing_a_window_narrows_the_chart_its_heading_and_its_axis()
    {
        SeedAndRegister([.. Fixtures.ConsecutiveDays(180)]);

        var page = Render<Dashboard>();

        Assert.Contains("last 180 days", page.Markup, StringComparison.Ordinal);
        Assert.Contains(Display.Day(Fixtures.Today.AddDays(-179)), page.Markup, StringComparison.Ordinal);

        page.Find("input[name=\"window\"][value=\"90\"]").Change("90");

        Assert.Contains("last 90 days", page.Markup, StringComparison.Ordinal);
        Assert.Contains(Display.Day(Fixtures.Today.AddDays(-89)), page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(Display.Day(Fixtures.Today.AddDays(-179)), page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   R6: <c>HasEnoughHistoryForChart</c> describes the stored history, not the chosen window.
    ///   Selecting 30 days on 200 days of training is not an insufficient-history state, and a page
    ///   that confused the two would replace the chart with an explanation the moment the athlete
    ///   asked for a closer look.
    /// </summary>
    [Fact]
    public void A_narrow_window_over_a_long_history_is_not_an_insufficient_history_state()
    {
        SeedAndRegister([.. Fixtures.ConsecutiveDays(200)]);

        var page = Render<Dashboard>();

        page.Find("input[name=\"window\"][value=\"30\"]").Change("30");

        Assert.NotEmpty(page.FindAll("svg"));
        Assert.DoesNotContain("Not enough data to show trends", page.Markup, StringComparison.Ordinal);
    }

    // ---- Feature 008: every state in the same visual language (US3, FR-009) ----

    /// <summary>
    ///   FR-011 and FR-017 on the redesigned page: a fresh installation is told what to do and
    ///   given somewhere to do it, and shows no figures it does not have.
    /// </summary>
    [Fact]
    public void The_unconnected_empty_state_explains_itself_and_offers_the_connect_route()
    {
        SeedAndRegister();

        var page = Render<Dashboard>();
        var content = page.Find("section.content");

        Assert.Contains("No activities recorded", content.InnerHtml, StringComparison.Ordinal);
        Assert.Contains("Connect your Strava account to bring your training in.", content.InnerHtml, StringComparison.Ordinal);
        Assert.NotNull(content.QuerySelector("a[href=\"/connect\"]"));

        // No figure row, so no em dashes standing in for figures nobody has yet.
        Assert.Empty(page.FindAll(".tile-value"));
    }

    /// <summary>
    ///   The same page with a connection already in place: a different explanation, and
    ///   deliberately no second connect action — the rail already offers the sync that is the
    ///   actual next step (US3 scenario 2).
    /// </summary>
    [Fact]
    public void The_connected_empty_state_offers_a_sync_and_no_redundant_connect_action()
    {
        Connect();
        SeedAndRegister();

        var page = Render<Dashboard>();
        var content = page.Find("section.content");

        Assert.Contains("Your Strava account is connected, but nothing has been imported yet.", content.InnerHtml, StringComparison.Ordinal);
        Assert.Null(content.QuerySelector("a[href=\"/connect\"]"));

        // The rail still offers the manual sync, which is what this state is waiting for.
        Assert.NotNull(page.Find("aside.rail").QuerySelector("button.sync"));
        Assert.Empty(page.FindAll(".tile-value"));
    }

    /// <summary>
    ///   C87: an unreadable store is a notice, visibly marked as different from the ordinary empty
    ///   states — which are expected and unalarming — rather than a page of confident zeroes.
    /// </summary>
    [Fact]
    public void An_unreadable_history_renders_a_distinct_notice_in_place_of_the_figures()
    {
        SeedAndRegister([.. Fixtures.H1]);
        Fixture.Execute("UPDATE Activities SET Type = 'Swimming'");

        var page = Render<Dashboard>();

        Assert.NotNull(page.Find("section.content").QuerySelector(".state-unavailable"));
        Assert.Contains("Data unavailable", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".tile-value"));
        Assert.Empty(page.FindAll("svg"));
        Assert.Empty(page.FindAll("table"));
    }

    /// <summary>
    ///   The first paint, while the history is still being read. The athlete is told something is
    ///   happening rather than shown an empty column that looks like a finished page with no data.
    /// </summary>
    [Fact]
    public void While_the_history_is_being_read_the_column_says_so()
    {
        var pending = new PendingContextFactory(Fixture);

        SeedAndRegister([.. Fixtures.H1]);
        Services.AddSingleton<IDbContextFactory<ImportDbContext>>(pending);

        var page = Render<Dashboard>();

        Assert.Contains("Reading your training history…", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".tile-value"));

        // The rail is not waiting on the history, so it renders through the loading state.
        Assert.NotNull(page.Find("aside.rail").QuerySelector("button.sync"));

        pending.Release();
    }

    /// <summary>
    ///   US5 scenario 5 in the rail: a credential Strava has since rejected offers the way back,
    ///   in SyncMessage's existing words.
    /// </summary>
    [Fact]
    public void A_reconnection_required_sync_offers_the_route_back_in_its_existing_words()
    {
        var panel = Render<SyncPanel>(p => p.Add(c => c.Status, new SyncStatus
        {
            Result = new SyncResult { Outcome = SyncOutcome.ReconnectionRequired },
        }));

        Assert.Contains(SyncMessage.ConnectionRequired, panel.Markup, StringComparison.Ordinal);
        Assert.NotNull(panel.Find("a[href=\"/connect\"]"));
    }

    /// <summary>
    ///   005 FR-035: a rate-limited sync states when the athlete can come back, in the format it
    ///   already used — Strava's own next window boundary, in the athlete's local time.
    /// </summary>
    [Fact]
    public void A_rate_limited_sync_states_its_retry_time_in_its_existing_format()
    {
        var retryAfter = new DateTimeOffset(
            Fixtures.Today.ToDateTime(new TimeOnly(7, 15)),
            FixedLocalClock.FixtureOffset);

        var panel = Render<SyncPanel>(p => p.Add(c => c.Status, new SyncStatus
        {
            Result = new SyncResult { Outcome = SyncOutcome.RateLimited, RetryAfter = retryAfter },
            RetryAfterLocal = retryAfter,
        }));

        Assert.Contains("Rate limited by Strava. Available again at 07:15.", panel.Markup, StringComparison.Ordinal);
    }

    // ---- Feature 008: accessible to everyone who used it before (US5) ----

    /// <summary>
    ///   FR-019: one top-level heading, and a heading naming the rail and each content region, in
    ///   the order they are read.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The <c>h1</c> is the rail's nameplate, and that is a deliberate departure from
    ///     contract §3, which specifies a styled <c>div</c> there. The rail comes first in the
    ///     document, so with the nameplate as a <c>div</c> the page's first four headings — As of,
    ///     Window, Strava, Max heart rate — would all precede the <c>h1</c>, which is not a
    ///     heading structure at all. The contract's stated reason for the rule is that the page
    ///     must carry exactly one <c>h1</c> and that <c>FocusOnNavigate Selector="h1"</c> must find
    ///     it; both hold, and better, with the nameplate as the heading it already is.
    ///   </para>
    ///   <para>
    ///     The four display figures carry no heading of their own. They are the lede, directly
    ///     under the page's title, and their labels are data labels rather than region names —
    ///     promoting them would name four headings where there is one region.
    ///   </para>
    /// </remarks>
    [Fact]
    public void The_headings_name_the_rail_and_each_content_region_in_reading_order()
    {
        SeedAndRegister([.. Fixtures.H1]);

        var page = Render<Dashboard>();

        Assert.Single(page.FindAll("h1"));

        // The h1 comes first, or the outline below it is hanging from nothing.
        var headings = page.FindAll("h1, h2").ToList();

        Assert.Equal("h1", headings[0].TagName.ToLowerInvariant());
        Assert.Equal("Training Load", headings[0].TextContent.Trim());

        Assert.Equal(
            ["As of", "Window", "Strava", "Max heart rate"],
            page.Find("aside.rail").QuerySelectorAll("h2").Select(h => h.TextContent.Trim()));

        var content = page.Find("section.content").QuerySelectorAll("h2")
            .Select(h => h.TextContent.Trim())
            .ToList();

        Assert.Contains(content, h => h.StartsWith("Daily load and metrics", StringComparison.Ordinal));
        Assert.Contains(content, h => h == "Recent activities");
    }

    /// <summary>
    ///   FR-018 and SC-007: outside the plot, no distinction the interface draws is carried by
    ///   colour alone.
    /// </summary>
    /// <remarks>
    ///   Asserted by deleting every class attribute and checking the meaning survives — which is
    ///   what a greyscale screen, and a screen reader, actually do to the page. Three distinctions
    ///   are at stake and each has its own failure: a basis tag that became a coloured dot, a week
    ///   trend that became a red or green figure, and a connection state that became an indicator
    ///   light.
    /// </remarks>
    [Fact]
    public void Every_distinction_outside_the_plot_survives_without_colour()
    {
        SeedAndRegister([.. Fixtures.H3f]);

        var page = Render<Dashboard>();
        var colourless = System.Text.RegularExpressions.Regex.Replace(
            page.Markup, "(class|style)=\"[^\"]*\"", string.Empty);

        // Provenance, in the recent-sessions table.
        Assert.Contains("measured", colourless, StringComparison.Ordinal);
        Assert.Contains("estimated", colourless, StringComparison.Ordinal);

        // The week's trend, as a signed figure and a percentage rather than a direction of colour.
        Assert.Matches(@"[+-]\d+\.\d\s*\(\s*[+-]?\d+%\s*\)", colourless);

        // The Strava state in the rail, in SyncMessage's own words.
        page.Find("button.sync").Click();

        Assert.Contains(
            SyncMessage.ConnectionRequired,
            System.Text.RegularExpressions.Regex.Replace(page.Markup, "(class|style)=\"[^\"]*\"", string.Empty),
            StringComparison.Ordinal);
    }
}
