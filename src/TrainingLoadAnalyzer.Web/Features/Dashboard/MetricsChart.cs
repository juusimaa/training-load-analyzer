using System.Globalization;
using TrainingLoadAnalyzer.Domain;

namespace TrainingLoadAnalyzer.Web.Features.Dashboard;

/// <summary>One plotted line, ready to become a <c>&lt;polyline&gt;</c>.</summary>
public sealed record ChartSeries(string Label, string Points, string CssClass);

/// <summary>
///   One day's load, ready to become a <c>&lt;rect&gt;</c> (008 FR-006).
/// </summary>
/// <remarks>
///   Four strings rather than four doubles, deliberately. A <c>double</c> handed to Razor is
///   formatted with the <em>current</em> culture at the point of rendering, which puts
///   <c>width="3,56"</c> into the markup on a Finnish machine — the same silent geometry failure
///   this whole module exists to prevent, arriving through a different door.
/// </remarks>
public sealed record LoadBar(string X, string Y, string Width, string Height);

/// <summary>
///   Turns the metrics series into SVG geometry (FR-006, SC-003).
/// </summary>
/// <remarks>
///   <para>
///     A separate function rather than an expression inside the component, for one reason:
///     <strong>culture</strong>. On a machine set to Finnish the obvious rendering of the points
///     (0, 45.3) and (1.5, 12.25) produces <c>points="0,45,3 1,5,12,25"</c> — valid-looking markup,
///     silently wrong geometry, no exception, and perfectly correct on an en-US machine. Putting
///     every coordinate through one tested place is what stops that coming back (research R9, C89).
///   </para>
///   <para>
///     Hand-written rather than delegated to a charting library. US3 scenario 3 offers a legend as
///     an alternative to a tooltip, so three polylines and a legend satisfy the requirement — and
///     being plain markup, the geometry is assertable, which is what SC-003 asks for (research R8).
///   </para>
/// </remarks>
public static class MetricsChart
{
    /// <summary>How much of the vertical range is left as breathing room above and below.</summary>
    private const double Padding = 0.08;

    /// <summary>
    ///   How much of the plot's height the heaviest day's bar fills. The bars are a backdrop
    ///   behind the three lines, so they are given half the plot and no more.
    /// </summary>
    private const double BarShareOfPlot = 0.5;

    /// <summary>
    ///   The narrowest a bar may be drawn. Over a 180-day window the arithmetic width falls below
    ///   a pixel, and a sub-pixel rect is one nobody sees — the backdrop would simply vanish at
    ///   the widest window, which is the one the page opens on.
    /// </summary>
    private const double MinimumBarWidth = 2;

    /// <summary>The gap between neighbouring bars, so a busy week reads as bars and not a block.</summary>
    private const double BarGap = 2;

    /// <summary>
    ///   The plot's coordinate space, formatted here for the same reason the points are: this is
    ///   the only other place a number becomes part of an SVG attribute, and leaving it in the
    ///   component would mean two places to get culture right instead of one.
    /// </summary>
    public static string ViewBox(double width, double height) => string.Create(
        CultureInfo.InvariantCulture,
        $"0 0 {width} {height}");

    public static IReadOnlyList<ChartSeries> Plot(
        IReadOnlyList<DailyTrainingMetrics> metrics,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (metrics.Count == 0)
        {
            // Nothing to plot yields nothing at all, never a series with an empty or half-formed
            // points attribute (C91).
            return [];
        }

        var (lowest, highest) = Band(metrics);

        return
        [
            Series("Fitness", "series-fitness", metrics, m => m.Fitness, lowest, highest, width, height),
            Series("Fatigue", "series-fatigue", metrics, m => m.Fatigue, lowest, highest, width, height),
            Series("Form", "series-form", metrics, m => m.Form, lowest, highest, width, height),
        ];
    }

    private static ChartSeries Series(
        string label,
        string cssClass,
        IReadOnlyList<DailyTrainingMetrics> metrics,
        Func<DailyTrainingMetrics, double> value,
        double lowest,
        double highest,
        double width,
        double height)
    {
        // One point per entry, in the same order, none skipped and none duplicated (SC-003, C90).
        // A single-day series sits at the left edge rather than dividing by zero.
        var step = metrics.Count == 1 ? 0 : width / (metrics.Count - 1);

        var points = metrics.Select((metric, i) =>
        {
            var x = i * step;

            return Coordinate(x, Y(value(metric), lowest, highest, height));
        });

        return new ChartSeries(label, string.Join(' ', points), cssClass);
    }

    /// <summary>
    ///   Daily load as bar geometry, behind the three lines (008 FR-006, Amendment 2).
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     On its <em>own</em> scale, not the metric band. The bars are a backdrop: the heaviest
    ///     day fills the same share of the plot whatever the loads happen to be, so a light week
    ///     does not render as a row of stubs beneath the lines and a heavy one does not tower over
    ///     them. Sharing the metric scale would make them a fourth series, which is exactly what
    ///     the design does not want.
    ///   </para>
    ///   <para>
    ///     A rest day yields no rect at all. A zero-height one is an invisible element with a real
    ///     cost, and a floor-height one would claim training that did not happen.
    ///   </para>
    /// </remarks>
    public static IReadOnlyList<LoadBar> LoadBars(
        IReadOnlyList<DailyTrainingLoad> loads,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(loads);

        if (loads.Count == 0)
        {
            return [];
        }

        var heaviest = loads.Max(day => day.Points);

        if (heaviest <= 0)
        {
            // Nothing but rest. No bars, rather than a flat row of them along the baseline.
            return [];
        }

        var step = loads.Count == 1 ? 0 : width / (loads.Count - 1);
        var barWidth = Math.Max(MinimumBarWidth, (width / loads.Count) - BarGap);

        return
        [
            .. loads
                .Select((day, index) => (day, index))
                .Where(entry => entry.day.Points > 0)
                .Select(entry =>
                {
                    var barHeight = (double)(entry.day.Points / heaviest) * height * BarShareOfPlot;

                    return new LoadBar(
                        Number((entry.index * step) - (barWidth / 2)),
                        Number(height - barHeight),
                        Number(barWidth),
                        Number(barHeight));
                }),
        ];
    }

    /// <summary>
    ///   Where form's zero sits, on the <em>shared metric scale</em> (008 FR-006).
    /// </summary>
    /// <remarks>
    ///   The same scale the polylines are drawn on, which is the only thing that makes the rule
    ///   worth drawing: the point where the Form line crosses zero has to be the point where it
    ///   crosses this line. A rule computed on any other scale looks entirely plausible and is
    ///   wrong everywhere except by coincidence.
    ///   <para>
    ///     Null when the band does not reach zero. Pinning the rule to the edge of the plot would
    ///     assert a reference the chart does not contain.
    ///   </para>
    /// </remarks>
    public static string? ZeroRule(
        IReadOnlyList<DailyTrainingMetrics> metrics,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (metrics.Count == 0)
        {
            return null;
        }

        var (lowest, highest) = Band(metrics);

        return lowest > 0 || highest < 0 ? null : Number(Y(0, lowest, highest, height));
    }

    /// <summary>
    ///   Evenly spaced date labels across the window, both ends included (008 FR-005, FR-006).
    /// </summary>
    /// <remarks>
    ///   Through <see cref="Display.Day"/>, so the axis reads in the same unambiguous format as
    ///   every other date on the page and on every machine. A date is a number too:
    ///   <c>ToString("d")</c> would render 18.9.2026 on the developer's laptop and 9/18/2026 on a
    ///   build agent, and neither is what the rest of the page says.
    /// </remarks>
    public static IReadOnlyList<string> AxisTicks(IReadOnlyList<DailyTrainingMetrics> metrics, int count)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (metrics.Count == 0)
        {
            return [];
        }

        // Fewer days than ticks asked for: label each of them rather than repeat one.
        if (count <= 1 || metrics.Count <= count)
        {
            return [.. metrics.Select(metric => Display.Day(metric.Day))];
        }

        return
        [
            .. Enumerable.Range(0, count).Select(tick => Display.Day(
                metrics[(int)Math.Round((double)tick * (metrics.Count - 1) / (count - 1))].Day)),
        ];
    }

    /// <summary>
    ///   The vertical band all three series share, with its breathing room applied.
    /// </summary>
    /// <remarks>
    ///   One scale across all three, so they can be read against each other — and so a negative
    ///   form sits inside the band rather than clipped at zero (C92). Form is routinely negative
    ///   (003 FR-003), which is exactly the state an athlete most wants to see. Extracted so
    ///   <see cref="ZeroRule"/> cannot drift onto a scale of its own.
    /// </remarks>
    private static (double Lowest, double Highest) Band(IReadOnlyList<DailyTrainingMetrics> metrics)
    {
        var values = metrics
            .SelectMany(m => new[] { m.Fitness, m.Fatigue, m.Form })
            .ToList();

        var lowest = values.Min();
        var highest = values.Max();
        var span = highest - lowest;
        var margin = span == 0 ? 1 : span * Padding;

        return (lowest - margin, highest + margin);
    }

    /// <summary>SVG's y grows downward, so the higher figure gets the smaller coordinate.</summary>
    private static double Y(double value, double lowest, double highest, double height) =>
        height - ((value - lowest) / (highest - lowest) * height);

    /// <summary>
    ///   The one place a number becomes part of an SVG attribute, and the reason this class exists
    ///   (C89, research R9).
    /// </summary>
    private static string Coordinate(double x, double y) => $"{Number(x)},{Number(y)}";

    /// <summary>
    ///   Every number that reaches the markup goes through here, in the invariant culture.
    /// </summary>
    /// <remarks>
    ///   On a machine set to Finnish the obvious rendering of the points (0, 45.3) and (1.5, 12.25)
    ///   produces <c>points="0,45,3 1,5,12,25"</c> — valid-looking markup, silently wrong geometry,
    ///   no exception, and perfectly correct on an en-US machine.
    /// </remarks>
    private static string Number(double value) => string.Create(
        CultureInfo.InvariantCulture,
        $"{Math.Round(value, 2)}");
}
