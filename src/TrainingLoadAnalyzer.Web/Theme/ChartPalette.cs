namespace TrainingLoadAnalyzer.Web.Theme;

/// <summary>
///   The three trend-chart series colours, for each scheme (007 FR-029).
/// </summary>
/// <remarks>
///   <para>
///     Separate from <see cref="TrainingLoadTheme"/> because <c>MudTheme</c> has no slot for a
///     chart series. Mapping them onto Info/Warning/Success was considered and rejected: those
///     roles mean something else, and any later use of them would silently repaint the chart
///     (007 research R5).
///   </para>
///   <para>
///     All six values are from Wong's colour-blind-safe palette. The light trio is feature 006's,
///     carried over unchanged. The dark trio lightens fitness and fatigue because
///     <c>#0072B2</c> measures about 2.5:1 against the dark plot surface, below the 3:1 FR-019
///     requires; form already measures 4.76:1 there and is deliberately shared between schemes.
///   </para>
///   <para>
///     <b>The series no longer carry dash patterns.</b> MudChart draws every series with a solid
///     stroke and offers no dash option, so feature 006's colour-blind differentiation in the
///     static plot is gone. A series is now identified by the legend and hover label instead
///     (FR-013a). This is the accepted loss recorded in the specification's Amendment 1 — it is a
///     real accessibility regression, not an oversight, and restoring it via a CSS
///     <c>stroke-dasharray</c> override was offered and deliberately declined.
///   </para>
/// </remarks>
public static class ChartPalette
{
    private const string LightFitness = "#0072B2";
    private const string LightFatigue = "#D55E00";
    private const string DarkFitness = "#56B4E9";
    private const string DarkFatigue = "#E69F00";

    /// <summary>Measures 3.08:1 on the light surface and 4.76:1 on the dark one, so it is shared.</summary>
    private const string Form = "#009E73";

    /// <summary>
    ///   The palette in the order the series are supplied to the chart. The order is positional —
    ///   MudChart matches colour to series by index — so it must match
    ///   <c>MetricsChartView</c>'s series order exactly.
    /// </summary>
    public static string[] For(bool isDarkMode) => isDarkMode
        ? [DarkFitness, DarkFatigue, Form]
        : [LightFitness, LightFatigue, Form];

    /// <summary>The same colours, named, so a failing contrast test can say which series failed.</summary>
    public static IEnumerable<(string Name, string Colour)> Named(bool isDarkMode)
    {
        var colours = For(isDarkMode);

        yield return ("fitness", colours[0]);
        yield return ("fatigue", colours[1]);
        yield return ("form", colours[2]);
    }
}
