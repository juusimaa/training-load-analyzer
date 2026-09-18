using System.Globalization;
using TrainingLoadAnalyzer.Domain;

namespace TrainingLoadAnalyzer.Web.Features.Dashboard;

/// <summary>One plotted line, ready to become a <c>&lt;polyline&gt;</c>.</summary>
public sealed record ChartSeries(string Label, string Points, string CssClass);

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

        // One scale across all three series, so they can be read against each other - and so a
        // negative form is inside the band rather than clipped at zero (C92). Form is routinely
        // negative (003 FR-003), which is exactly the state an athlete most wants to see.
        var values = metrics
            .SelectMany(m => new[] { m.Fitness, m.Fatigue, m.Form })
            .ToList();

        var lowest = values.Min();
        var highest = values.Max();
        var span = highest - lowest;
        var margin = span == 0 ? 1 : span * Padding;

        lowest -= margin;
        highest += margin;

        return
        [
            Series("Fitness", "fitness", metrics, m => m.Fitness, lowest, highest, width, height),
            Series("Fatigue", "fatigue", metrics, m => m.Fatigue, lowest, highest, width, height),
            Series("Form", "form", metrics, m => m.Form, lowest, highest, width, height),
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
        var range = highest - lowest;

        var points = metrics.Select((metric, i) =>
        {
            var x = i * step;

            // SVG's y grows downward, so the higher figure gets the smaller coordinate.
            var y = height - ((value(metric) - lowest) / range * height);

            return Coordinate(x, y);
        });

        return new ChartSeries(label, string.Join(' ', points), cssClass);
    }

    /// <summary>
    ///   The one place a number becomes part of an SVG attribute, and the reason this class exists
    ///   (C89, research R9).
    /// </summary>
    private static string Coordinate(double x, double y) => string.Create(
        CultureInfo.InvariantCulture,
        $"{Math.Round(x, 2)},{Math.Round(y, 2)}");
}
