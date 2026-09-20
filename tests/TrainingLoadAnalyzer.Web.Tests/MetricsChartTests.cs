using System.Globalization;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Web.Features.Dashboard;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   The chart's geometry, asserted directly rather than through a rendered component — which is
///   what SC-003 actually says, and what a canvas-based charting library would have put out of
///   reach of any test in this repository (research R8).
/// </summary>
public class MetricsChartTests
{
    private const double Width = 600;
    private const double Height = 200;

    private static IReadOnlyList<DailyTrainingMetrics> Series(int days) =>
    [
        .. Enumerable.Range(0, days).Select(i => new DailyTrainingMetrics(
            Fixtures.Today.AddDays(-(days - 1 - i)),
            40 + i,
            20 + (i * 2),
            i >= 42,
            LoadBasis.Estimated,
            LoadBasis.Estimated)),
    ];

    private static void InFinnish(Action assertion)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fi-FI");

        try
        {
            assertion();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    ///   SC-003, C90, C91: three series, one point per day, in order, none skipped or duplicated.
    /// </summary>
    [Fact]
    public void Three_series_carry_one_point_per_day()
    {
        var plotted = MetricsChart.Plot(Series(5), Width, Height);

        Assert.Equal(3, plotted.Count);
        Assert.Equal(["Fitness", "Fatigue", "Form"], plotted.Select(s => s.Label));

        foreach (var series in plotted)
        {
            Assert.Equal(5, series.Points.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        }
    }

    /// <summary>
    ///   C89 and research R9 — the most important test in this feature.
    ///   <para>
    ///     On a machine set to Finnish, <c>$"{x},{y}"</c> renders the points (0, 45.3) and
    ///     (1.5, 12.25) as <c>points="0,45,3 1,5,12,25"</c>: well-formed markup, silently wrong
    ///     geometry, no exception — and correct-looking on an en-US build agent while broken on the
    ///     developer's own laptop. Every coordinate must carry exactly one comma.
    ///   </para>
    /// </summary>
    [Fact]
    public void Coordinates_are_machine_readable_whatever_the_current_culture_is()
    {
        InFinnish(() =>
        {
            var plotted = MetricsChart.Plot(Series(5), Width, Height);

            foreach (var series in plotted)
            {
                foreach (var point in series.Points.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    Assert.Equal(1, point.Count(c => c == ','));
                    Assert.DoesNotContain("−", point, StringComparison.Ordinal);

                    var parts = point.Split(',');
                    Assert.True(
                        double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                        $"'{parts[0]}' is not an invariant-culture number.");
                    Assert.True(
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                        $"'{parts[1]}' is not an invariant-culture number.");
                }
            }
        });
    }

    /// <summary>
    ///   C92: form is routinely negative (003 FR-003), and a chart that clipped it at zero would
    ///   hide exactly the state an athlete most wants to see.
    /// </summary>
    [Fact]
    public void A_negative_form_is_plotted_rather_than_clipped()
    {
        // 30 days, so Form (20 − i) really does cross zero. The assertion below guards that:
        // a test for negative values whose fixture has none proves nothing.
        var series = Series(30);
        var plotted = MetricsChart.Plot(series, Width, Height);
        var form = plotted.Single(s => s.Label == "Form");

        Assert.Contains(series, m => m.Form < 0);

        foreach (var y in form.Points.Split(' ').Select(p => double.Parse(p.Split(',')[1], CultureInfo.InvariantCulture)))
        {
            Assert.InRange(y, 0, Height);
        }
    }

    /// <summary>C91: nothing to plot yields nothing, never a malformed attribute.</summary>
    [Fact]
    public void An_empty_history_yields_no_series_at_all()
    {
        Assert.Empty(MetricsChart.Plot([], Width, Height));
    }

    // ---- Feature 008: the bars, the zero rule and the date axis ----

    /// <summary>A daily load history, one entry per day, of the given points.</summary>
    private static IReadOnlyList<DailyTrainingLoad> Loads(params decimal[] points) =>
    [
        .. points.Select((p, i) => new DailyTrainingLoad(
            Fixtures.Today.AddDays(-(points.Length - 1 - i)),
            p,
            p == 0 ? 0 : 1,
            p == 0 ? LoadBasis.None : LoadBasis.Estimated)),
    ];

    private static IReadOnlyList<DailyTrainingLoad> VaryingLoads(int days) =>
        Loads([.. Enumerable.Range(0, days).Select(i => (decimal)(60 + (i % 5 * 40)))]);

    /// <summary>
    ///   FR-006: one bar per day that carried training. A rest day draws nothing — a zero-height
    ///   rect is an invisible element with a real cost, and a floor-height one would claim the
    ///   athlete trained.
    /// </summary>
    [Fact]
    public void One_bar_is_drawn_for_each_day_that_carried_load()
    {
        var bars = MetricsChart.LoadBars(Loads(120, 0, 240, 0, 60), Width, Height);

        Assert.Equal(3, bars.Count);
    }

    /// <summary>A history of nothing but rest draws no bars at all, never a flat row of them.</summary>
    [Fact]
    public void A_history_with_no_load_draws_no_bars()
    {
        Assert.Empty(MetricsChart.LoadBars(Loads(0, 0, 0), Width, Height));
        Assert.Empty(MetricsChart.LoadBars([], Width, Height));
    }

    /// <summary>
    ///   The bars are on their own scale, independent of the metric band.
    /// </summary>
    /// <remarks>
    ///   They are a backdrop, not a fourth series: the tallest bar fills the same share of the
    ///   plot whatever the loads happen to be, so a light week does not render as a row of stubs
    ///   and a heavy one does not tower over the lines. Two histories an order of magnitude apart
    ///   must therefore produce the same tallest bar.
    /// </remarks>
    [Fact]
    public void The_bars_are_scaled_on_their_own_axis_not_the_metric_band()
    {
        static double Tallest(IReadOnlyList<LoadBar> bars) =>
            bars.Max(b => double.Parse(b.Height, CultureInfo.InvariantCulture));

        var modest = MetricsChart.LoadBars(Loads(30, 60, 90), Width, Height);
        var heavy = MetricsChart.LoadBars(Loads(300, 600, 900), Width, Height);

        Assert.Equal(Tallest(modest), Tallest(heavy), 6);

        // And it stays inside the plot rather than over-drawing the lines it sits behind.
        Assert.InRange(Tallest(heavy), 0, Height);
    }

    /// <summary>
    ///   The floor that keeps a full window visible. At 180 days the arithmetic width of a bar
    ///   falls below a pixel, and a sub-pixel rect is a rect nobody sees.
    /// </summary>
    [Fact]
    public void Every_bar_keeps_a_minimum_width_across_a_full_window()
    {
        var bars = MetricsChart.LoadBars(VaryingLoads(180), Width, Height);

        Assert.Equal(180, bars.Count);
        Assert.All(bars, bar =>
            Assert.True(
                double.Parse(bar.Width, CultureInfo.InvariantCulture) >= 1.0,
                $"A bar {bar.Width} wide is not visible."));
    }

    /// <summary>
    ///   C89 again, for the geometry this feature adds. A rect's four attributes are as vulnerable
    ///   to a decimal comma as a polyline's points, and just as silent about it.
    /// </summary>
    [Fact]
    public void Bar_geometry_is_machine_readable_whatever_the_current_culture_is()
    {
        InFinnish(() =>
        {
            foreach (var bar in MetricsChart.LoadBars(VaryingLoads(30), Width, Height))
            {
                foreach (var value in new[] { bar.X, bar.Y, bar.Width, bar.Height })
                {
                    Assert.DoesNotContain(",", value, StringComparison.Ordinal);
                    Assert.True(
                        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                        $"'{value}' is not an invariant-culture number.");
                }
            }
        });
    }

    /// <summary>
    ///   FR-006's zero reference line, and the property that makes it worth drawing: it sits on the
    ///   <em>shared metric scale</em>, so the Form line crossing zero crosses the rule.
    /// </summary>
    /// <remarks>
    ///   The fixture is chosen so one day's form is exactly zero — Form is <c>20 − i</c>, so day 20
    ///   is it. The rule's y must be that point's y. A rule computed on any other scale would look
    ///   plausible and be wrong everywhere except by coincidence.
    /// </remarks>
    [Fact]
    public void The_zero_rule_sits_where_the_form_line_crosses_zero()
    {
        var series = Series(30);

        Assert.Equal(0, series[20].Form);

        var form = MetricsChart.Plot(series, Width, Height).Single(s => s.Label == "Form");
        var crossing = form.Points.Split(' ')[20].Split(',')[1];

        Assert.Equal(crossing, MetricsChart.ZeroRule(series, Width, Height));
    }

    /// <summary>
    ///   A band that never reaches zero gets no rule. Drawing one at the edge of the plot would
    ///   assert a reference the chart does not actually contain.
    /// </summary>
    [Fact]
    public void A_band_that_excludes_zero_has_no_rule()
    {
        IReadOnlyList<DailyTrainingMetrics> alwaysPositive =
        [
            .. Enumerable.Range(0, 30).Select(i => new DailyTrainingMetrics(
                Fixtures.Today.AddDays(-(29 - i)), 50, 10, true, LoadBasis.Estimated, LoadBasis.Estimated)),
        ];

        Assert.All(alwaysPositive, m => Assert.True(m.Form > 0));
        Assert.Null(MetricsChart.ZeroRule(alwaysPositive, Width, Height));
        Assert.Null(MetricsChart.ZeroRule([], Width, Height));
    }

    /// <summary>
    ///   FR-005 and FR-006: the date axis, with both ends of the window named so the chart's span
    ///   is readable without counting ticks.
    /// </summary>
    [Fact]
    public void The_axis_labels_both_ends_of_the_window_and_evenly_between()
    {
        var series = Series(180);
        var ticks = MetricsChart.AxisTicks(series, 6);

        Assert.Equal(6, ticks.Count);
        Assert.Equal(Display.Day(series[0].Day), ticks[0]);
        Assert.Equal(Display.Day(series[^1].Day), ticks[^1]);
        Assert.Equal(ticks, ticks.Distinct());
    }

    /// <summary>
    ///   Every label carries the same unambiguous day format the rest of the page uses, on any
    ///   machine. A date is a number too, and <c>ToString("d")</c> would render 18.9.2026 here and
    ///   9/18/2026 on a build agent.
    /// </summary>
    [Fact]
    public void Axis_labels_are_invariantly_formatted_whatever_the_current_culture_is()
    {
        InFinnish(() =>
        {
            foreach (var tick in MetricsChart.AxisTicks(Series(180), 6))
            {
                Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", tick);
            }
        });
    }

    /// <summary>A window too short to divide still labels what it has, rather than throwing.</summary>
    [Fact]
    public void A_window_shorter_than_the_tick_count_labels_every_day_it_has()
    {
        Assert.Equal(3, MetricsChart.AxisTicks(Series(3), 6).Count);
        Assert.Empty(MetricsChart.AxisTicks([], 6));
    }

    /// <summary>
    ///   Each series names the class its colour resolves through, and names no colour itself.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     008 research R5. The reference page writes <c>stroke="#d6006c"</c> straight onto the
    ///     polyline. A presentation attribute cannot respond to <c>prefers-color-scheme</c>, so a
    ///     literal there would pin all three lines to their light-scheme values and leave the chart
    ///     as the one region that ignores a dark device.
    ///   </para>
    ///   <para>
    ///     Worth asserting here rather than trusting the colour scan.
    ///     <c>ColourDisciplineTests</c> reads only <c>*.css</c> and <c>*.razor</c>, so a hex
    ///     written into this <c>.cs</c> module is outside the net that catches it everywhere else.
    ///   </para>
    /// </remarks>
    [Fact]
    public void Each_series_carries_a_class_and_never_a_colour()
    {
        var plotted = MetricsChart.Plot(Series(5), Width, Height);

        Assert.Equal(
            ["series-fitness", "series-fatigue", "series-form"],
            plotted.Select(s => s.CssClass));

        Assert.DoesNotContain(
            "#",
            string.Join(' ', plotted.Select(s => s.CssClass + s.Points)),
            StringComparison.Ordinal);
    }
}
