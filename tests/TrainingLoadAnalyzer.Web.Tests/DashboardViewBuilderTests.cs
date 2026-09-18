using TrainingLoadAnalyzer.Web.Features.Dashboard;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   The read model: pure, no database, no host, no clock (research R11).
/// </summary>
public class DashboardViewBuilderTests
{
    private static DashboardView Build(IReadOnlyList<Domain.TrainingActivity> activities) =>
        DashboardViewBuilder.Build(activities, Fixtures.Today, Fixtures.MaximumHeartRate, true);

    /// <summary>
    ///   FR-001, FR-002, FR-003 against fixture H1. These are the real CTL and ATL for a single
    ///   120-point day — <c>120 × (1 − e^(−1/42))</c> and <c>120 × (1 − e^(−1/7))</c> — not the load
    ///   itself. An implementation that displayed the raw 120 fails here.
    /// </summary>
    [Fact]
    public void The_current_figures_are_fitness_fatigue_and_form()
    {
        var view = Build(Fixtures.H1);

        Assert.NotNull(view.Current);
        Assert.Equal(2.8233976017308082, view.Current.Value.Fitness, 12);
        Assert.Equal(15.974652029978209, view.Current.Value.Fatigue, 12);
        Assert.Equal(view.Current.Value.Fitness - view.Current.Value.Fatigue, view.Current.Value.Form, 12);
    }

    /// <summary>
    ///   C80: <c>Current</c> is the last point of the chart, derived on every read. The tiles and
    ///   the chart's final point are therefore the same value by construction, which is what makes
    ///   SC-002 structural rather than something to be maintained.
    /// </summary>
    [Fact]
    public void The_current_figures_are_the_charts_last_point()
    {
        var view = Build(Fixtures.ConsecutiveDays(40));

        Assert.NotNull(view.Current);
        Assert.Equal(view.Metrics[^1], view.Current.Value);
        Assert.Equal(Fixtures.Today, view.Current.Value.Day);
    }

    /// <summary>
    ///   C79: pure. The same arguments yield the same figures, and the order activities arrive in
    ///   does not affect them — continuing 002 C13.
    /// </summary>
    /// <remarks>
    ///   Asserted value by value rather than with one <c>Assert.Equal</c> on the view. A record
    ///   compares its collection members by reference, so two separate builds are never
    ///   <c>Equals</c> however identical their contents; giving <see cref="DashboardView"/> a
    ///   structural equality nobody needs to satisfy a test would be the wrong way round.
    /// </remarks>
    [Fact]
    public void Building_is_pure_and_order_independent()
    {
        var shuffled = Fixtures.H2.Reverse().ToList();
        var once = Build(Fixtures.H2);
        var again = Build(Fixtures.H2);
        var reordered = Build(shuffled);

        Assert.Equal(once.Metrics, again.Metrics);
        Assert.Equal(once.Current, again.Current);

        Assert.Equal(once.Metrics, reordered.Metrics);
        Assert.Equal(once.Current, reordered.Current);
        Assert.Equal(once.AsOf, reordered.AsOf);
    }

    /// <summary>
    ///   FR-006 and C83. With 400 days of history the chart shows the last 180 — today included,
    ///   so it opens on 2026-03-23. Older training is not discarded: it still feeds fitness and
    ///   fatigue, because those accumulate from the first recorded day. Only the chart stops.
    /// </summary>
    [Fact]
    public void The_chart_covers_at_most_180_days_ending_today()
    {
        var view = Build(Fixtures.ConsecutiveDays(400));

        Assert.Equal(180, view.Metrics.Count);
        Assert.Equal(new DateOnly(2026, 3, 23), view.Metrics[0].Day);
        Assert.Equal(Fixtures.Today, view.Metrics[^1].Day);
    }

    /// <summary>
    ///   C83: gap-free and ascending, one entry per day. It is
    ///   <c>TrainingMetricsCalculator</c>'s output unmodified — feature 003 refuses a discontinuous
    ///   history outright — so this pins that nothing reshapes it on the way out.
    /// </summary>
    [Fact]
    public void The_chart_has_exactly_one_entry_per_day_in_order()
    {
        var view = Build(Fixtures.ConsecutiveDays(200));

        for (var i = 1; i < view.Metrics.Count; i++)
        {
            Assert.Equal(view.Metrics[i - 1].Day.AddDays(1), view.Metrics[i].Day);
        }
    }

    /// <summary>
    ///   A history shorter than the window shows all of it rather than padding to 180.
    /// </summary>
    [Fact]
    public void A_history_shorter_than_the_window_is_shown_in_full()
    {
        var view = Build(Fixtures.ConsecutiveDays(45));

        Assert.Equal(45, view.Metrics.Count);
        Assert.Equal(Fixtures.Today.AddDays(-44), view.Metrics[0].Day);
    }
}
