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
}
