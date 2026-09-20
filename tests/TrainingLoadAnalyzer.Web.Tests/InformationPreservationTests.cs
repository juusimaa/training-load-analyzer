using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Web.Components.Dashboard;
using TrainingLoadAnalyzer.Web.Components.Pages;
using TrainingLoadAnalyzer.Web.Features.Dashboard;
using TrainingLoadAnalyzer.Web.Features.Sync;
using TrainingLoadAnalyzer.Web.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   Feature 007, SC-001: every user-visible string survives the visual refresh.
/// </summary>
/// <remarks>
///   <para>
///     These are characterization tests, and they were written <em>before</em> any component was
///     converted, against markup that already produced the right words. That ordering is the whole
///     point — written afterwards they would only describe whatever the conversion happened to
///     produce, and would prove nothing about what was lost on the way.
///   </para>
///   <para>
///     They assert on extracted <em>text</em>, not on markup. A redesign changes markup by
///     definition, so a markup snapshot would fail on every intended change and train everyone to
///     re-approve it blindly. The text is the invariant worth freezing.
///   </para>
/// </remarks>
public class InformationPreservationTests : DashboardRenderContext
{
    // ---- The populated dashboard (US2 scenario 1) ----

    /// <summary>
    ///   The figures fixture H1 produces, their labels, and the section headings around them.
    /// </summary>
    [Fact]
    public void The_populated_dashboard_keeps_its_figures_and_its_labels()
    {
        var text = TextOf(RenderDashboard([.. Fixtures.H1]));

        Assert.Contains("Fitness", text, StringComparison.Ordinal);
        Assert.Contains("Fatigue", text, StringComparison.Ordinal);
        Assert.Contains("Form", text, StringComparison.Ordinal);
        Assert.Contains("This week", text, StringComparison.Ordinal);
        Assert.Contains("Recent activities", text, StringComparison.Ordinal);

        // H1 is one 60-minute estimated session: 120 points, and the fitness figure it produces.
        Assert.Contains("2.8", text, StringComparison.Ordinal);
        Assert.Contains("120.0", text, StringComparison.Ordinal);
    }

    /// <summary>An absent figure is an em dash, never a confident zero (US2 scenario 1).</summary>
    [Fact]
    public void An_absent_figure_is_still_a_dash()
    {
        var text = TextOf(Render<MetricRow>());

        Assert.Contains(Display.Missing, text, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0", text, StringComparison.Ordinal);
    }

    // ---- The qualifiers (US2 scenario 2, FR-012) ----

    /// <summary>
    ///   <para>
    ///     The three qualifier strings, asserted as <em>text</em>.
    ///   </para>
    ///   <para>
    ///     This is the feature's likeliest silent regression. Feature 006 deliberately wrote these
    ///     as words rather than styling, because a difference only a stylesheet expresses is
    ///     invisible to a screen reader. A Material chip that carries a colour or an icon instead
    ///     of its label would satisfy every visual requirement and quietly delete the meaning.
    ///   </para>
    ///   <para>
    ///     They matter more since Amendment 1: with the chart's dash patterns gone, these are the
    ///     last non-colour guarantees left in the interface.
    ///   </para>
    /// </summary>
    [Theory]
    [InlineData(false, LoadBasis.Measured, "still settling")]
    [InlineData(true, LoadBasis.Estimated, "estimated")]
    [InlineData(true, LoadBasis.Mixed, "partly estimated")]
    public void A_qualifier_is_still_a_word_and_not_only_a_colour(bool isReliable, LoadBasis basis, string expected)
    {
        var tile = Render<MetricRow>(p => p
            .Add(c => c.Current, new DailyTrainingMetrics(Fixtures.Today, 2.8, 0, isReliable, basis, basis)));

        Assert.Contains(expected, TextOf(tile), StringComparison.Ordinal);
    }

    /// <summary>Two qualifiers at once still both appear (007 edge case).</summary>
    [Fact]
    public void Two_qualifiers_at_once_both_survive()
    {
        var tile = Render<MetricRow>(p => p.Add(
            c => c.Current,
            new DailyTrainingMetrics(Fixtures.Today, 2.8, 0, false, LoadBasis.Mixed, LoadBasis.Mixed)));

        var text = TextOf(tile);

        Assert.Contains("still settling", text, StringComparison.Ordinal);
        Assert.Contains("partly estimated", text, StringComparison.Ordinal);
    }

    // ---- Provenance (US2 scenario 3, FR-012, FR-022) ----

    /// <summary>
    ///   Every activity row says <c>measured</c> or <c>estimated</c> in words. Feature 006's
    ///   comment on this markup is explicit that a colour-only difference excludes anyone who
    ///   cannot tell the two colours apart.
    /// </summary>
    [Fact]
    public void Every_activity_row_still_words_its_provenance()
    {
        var text = TextOf(RenderDashboard([.. Fixtures.H3f]));

        Assert.Contains("estimated", text, StringComparison.Ordinal);
        Assert.Contains("measured", text, StringComparison.Ordinal);
    }

    // ---- The states an athlete hits first and worst (US3, FR-014) ----

    /// <summary>
    ///   An athlete who has just installed this has no data and no connection. The words that meet
    ///   them must survive being wrapped in a Material surface.
    /// </summary>
    [Fact]
    public void The_unconnected_empty_state_keeps_its_words_and_its_action()
    {
        var text = TextOf(RenderDashboard());

        Assert.Contains("No activities recorded", text, StringComparison.Ordinal);
        Assert.Contains("Connect your Strava account to bring your training in.", text, StringComparison.Ordinal);
        Assert.Contains("Connect Strava", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///   The same empty page, but connected: a different explanation and deliberately no second
    ///   connect action, since one is already in place (US3 scenario 2).
    /// </summary>
    [Fact]
    public void The_connected_empty_state_explains_itself_without_a_redundant_action()
    {
        var page = RenderDashboard();

        // The fixture database holds no connection, so this asserts the unconnected branch's
        // wording is the one that appears — and that the two branches have not been merged.
        Assert.DoesNotContain(
            "Your Strava account is connected, but nothing has been imported yet.",
            TextOf(page),
            StringComparison.Ordinal);
    }

    // ---- Sync (US2 scenario 4) ----

    [Fact]
    public void The_sync_control_keeps_its_two_labels()
    {
        var atRest = Render<SyncPanel>(p => p.Add(c => c.Status, SyncStatus.Never));

        Assert.Contains("Sync Activities", TextOf(atRest), StringComparison.Ordinal);

        var running = Render<SyncPanel>(p => p.Add(c => c.Status, new SyncStatus { IsRunning = true }));

        Assert.Contains("Syncing…", TextOf(running), StringComparison.Ordinal);
    }

    /// <summary>The timestamp format is part of what the athlete reads (FR-002).</summary>
    [Fact]
    public void The_last_checked_timestamp_keeps_its_format()
    {
        var finished = new SyncStatus
        {
            Failure = "No Strava account is connected.",
            FinishedAt = Moment,
        };

        var panel = Render<SyncPanel>(p => p.Add(c => c.Status, finished));

        Assert.Matches(new Regex(@"Last checked \d{4}-\d{2}-\d{2} \d{2}:\d{2}"), TextOf(panel));
    }

    // ---- The chart's surround (US2 scenario 5, FR-013a) ----

    /// <summary>
    ///   The legend names. Since Amendment 1 these are the only way to identify a series — the
    ///   dash patterns that used to do it are gone — so they are no longer merely wording.
    /// </summary>
    [Fact]
    public void The_chart_legend_still_names_all_three_series()
    {
        var chart = Render<MetricsChartView>(p => p
            .Add(c => c.Metrics, Series(60))
            .Add(c => c.HasEnoughHistory, true));

        var text = TextOf(chart);

        Assert.Contains("Fitness", text, StringComparison.Ordinal);
        Assert.Contains("Fatigue", text, StringComparison.Ordinal);
        Assert.Contains("Form", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_chart_still_states_its_date_range()
    {
        var chart = Render<MetricsChartView>(p => p
            .Add(c => c.Metrics, Series(60))
            .Add(c => c.HasEnoughHistory, true));

        Assert.Matches(new Regex(@"\d{4}-\d{2}-\d{2}.*\d{4}-\d{2}-\d{2}"), TextOf(chart));
    }

    [Fact]
    public void The_insufficient_history_message_is_word_for_word_unchanged()
    {
        var chart = Render<MetricsChartView>(p => p
            .Add(c => c.Metrics, Series(12))
            .Add(c => c.HasEnoughHistory, false));

        Assert.Contains(
            "Not enough data to show trends (30+ days required)",
            TextOf(chart),
            StringComparison.Ordinal);
    }

    // ---- Structure the redesign must not break (US5, FR-023) ----

    /// <summary>
    ///   Exactly one <c>h1</c>, still.
    /// </summary>
    /// <remarks>
    ///   <c>Routes.razor</c> focuses <c>h1</c> after navigation, so a second one silently steals
    ///   that focus and a keyboard or screen-reader user lands somewhere arbitrary. This is the
    ///   specific risk the app bar introduced: <c>MudAppBar</c>'s title is styled with
    ///   <c>Typo.h6</c> but rendered as a <c>span</c> precisely so it contributes no heading, and
    ///   nothing but a test keeps it that way.
    /// </remarks>
    [Fact]
    public void The_dashboard_still_has_exactly_one_top_level_heading()
    {
        var page = RenderDashboard([.. Fixtures.H1]);

        Assert.Single(page.FindAll("h1"));
    }

    /// <summary>
    ///   The heading structure below it still names the regions in reading order (FR-023).
    /// </summary>
    [Fact]
    public void The_regions_below_it_are_still_headings()
    {
        var page = RenderDashboard([.. Fixtures.H1]);

        Assert.Contains(
            page.FindAll("h2").Select(h => h.TextContent.Trim()),
            text => text.Contains("Recent activities", StringComparison.Ordinal));
    }

    // ---- Helpers ----

    private static readonly DateTimeOffset Moment =
        new(Fixtures.Today.ToDateTime(new TimeOnly(9, 30)), FixedLocalClock.FixtureOffset);

    /// <summary>
    ///   What the athlete reads: tags stripped, entities resolved, whitespace collapsed. Asserting
    ///   on this rather than on <c>Markup</c> is what makes these tests survive the redesign they
    ///   exist to police.
    /// </summary>
    private static string TextOf<TComponent>(IRenderedComponent<TComponent> rendered)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        var withoutTags = Regex.Replace(rendered.Markup, "<[^>]*>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(withoutTags).Replace('\u00A0', ' ');

        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    private static IReadOnlyList<DailyTrainingMetrics> Series(int days) =>
        [.. Enumerable.Range(0, days).Select(i => new DailyTrainingMetrics(
            Fixtures.Today.AddDays(-(days - 1 - i)),
            40 + i,
            20 + (i * 2),
            i >= 42,
            LoadBasis.Estimated,
            LoadBasis.Estimated))];

    // ---- Feature 008 (US2): the redesign adds, and takes nothing away ----

    /// <summary>
    ///   008 Amendment 1(a): the rail's two additions are <em>additional to</em> every string the
    ///   previous interface showed, never in place of one.
    /// </summary>
    /// <remarks>
    ///   The failure this catches is the plausible one. A rail that reads "186 bpm · 2026-W38"
    ///   beside four big figures looks complete, and it is easy to drop "This week" or a
    ///   qualifier on the way there and never notice — the page would still look finished. So the
    ///   old strings and the new ones are asserted in the same test, together.
    /// </remarks>
    [Fact]
    public void The_rails_two_additions_are_additional_to_everything_that_was_there_before()
    {
        var text = TextOf(RenderDashboard([.. Fixtures.H3f]));

        // What feature 007's dashboard showed.
        foreach (var previous in new[]
                 {
                     "Training Load", "Fitness", "Fatigue", "Form", "This week",
                     "Recent activities", "Sync Activities", "measured", "estimated",
                 })
        {
            Assert.Contains(previous, text, StringComparison.Ordinal);
        }

        // And what feature 008 adds beside them.
        Assert.Contains("2026-W38", text, StringComparison.Ordinal);
        Assert.Contains($"{Fixtures.MaximumHeartRate} bpm", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///   <para>
    ///     The three <c>/connect</c> outcomes land on the dashboard, and it renders normally.
    ///   </para>
    ///   <para>
    ///     <strong>A recorded gap, not a preserved message.</strong> <c>/strava/callback</c>
    ///     redirects to <c>/?connect=declined</c>, <c>=scope</c> and <c>=mismatch</c>, and nothing
    ///     on the dashboard reads that parameter — the athlete who declines consent, withholds a
    ///     scope or authorizes as a different athlete is returned to an unchanged page with no
    ///     explanation. That was true before this feature and is true after it. Feature 008 is
    ///     information-preserving, and there is no message here to preserve; inventing one would be
    ///     new athlete-facing content no requirement asks for (Principle VII).
    ///   </para>
    ///   <para>
    ///     What this does assert is the part that could regress: the redesigned page renders its
    ///     ordinary content under each of those query strings rather than breaking on a parameter
    ///     it does not recognise. If a message is ever added, this test is where its wording
    ///     belongs.
    ///   </para>
    /// </summary>
    [Theory]
    [InlineData("declined")]
    [InlineData("scope")]
    [InlineData("mismatch")]
    public void A_connect_outcome_returns_to_a_dashboard_that_still_renders(string outcome)
    {
        SeedAndRegister([.. Fixtures.H1]);

        Services.GetRequiredService<NavigationManager>().NavigateTo($"/?connect={outcome}");

        var text = TextOf(Render<Dashboard>());

        Assert.Contains("Fitness", text, StringComparison.Ordinal);
        Assert.Contains("Recent activities", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///   US2 scenario 2, through the whole page rather than one component: a figure with fewer than
    ///   42 days behind it says so in words on the rendered dashboard.
    /// </summary>
    /// <remarks>
    ///   The component-level test above proves the row can render the qualifier. This proves the
    ///   page actually asks it to — a row wired up without its <c>Current</c> would pass the first
    ///   and fail this one.
    /// </remarks>
    [Fact]
    public void A_settling_figure_still_says_so_on_the_rendered_page()
    {
        var text = TextOf(RenderDashboard([.. Fixtures.ConsecutiveDays(35)]));

        Assert.Contains("still settling", text, StringComparison.Ordinal);
    }
}
