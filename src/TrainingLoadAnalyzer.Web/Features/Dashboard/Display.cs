using System.Globalization;

namespace TrainingLoadAnalyzer.Web.Features.Dashboard;

/// <summary>
///   How a figure reaches the page (C99, C100).
/// </summary>
/// <remarks>
///   <para>
///     Everything numeric goes through here, against <see cref="CultureInfo.InvariantCulture"/>
///     rather than the server's. SC-002 writes its worked example as "45.3", and on a machine set
///     to Finnish the obvious rendering produces "45,3" — and, for a negative form, "−13,2" with a
///     U+2212 minus sign rather than a hyphen (research R9).
///   </para>
///   <para>
///     One place, so there is one thing to get right. The same reasoning governs
///     <see cref="MetricsChart"/>, where the consequence is worse than an odd-looking number: a
///     decimal comma inside an SVG coordinate list silently changes the geometry.
///   </para>
/// </remarks>
public static class Display
{
    /// <summary>The em dash a missing figure shows, never a zero (US1 scenario 2, C100).</summary>
    public const string Missing = "—";

    private const string OneDecimal = "0.0";

    /// <summary>Fitness, fatigue or form, to one decimal place (FR-001 - FR-003, SC-002).</summary>
    public static string Metric(double? value) =>
        value is null ? Missing : value.Value.ToString(OneDecimal, CultureInfo.InvariantCulture);

    /// <summary>A training load in TRIMP points (FR-004, FR-007).</summary>
    public static string Points(decimal? value) =>
        value is null ? Missing : value.Value.ToString(OneDecimal, CultureInfo.InvariantCulture);

    /// <summary>A whole-number percentage, sign included (FR-005).</summary>
    public static string Percent(decimal? fraction) =>
        fraction is null
            ? Missing
            : (fraction.Value * 100).ToString("+0;-0;0", CultureInfo.InvariantCulture) + "%";

    /// <summary>A duration as hours and minutes, or minutes alone under an hour (FR-007).</summary>
    public static string Duration(TimeSpan movingTime) =>
        movingTime.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)movingTime.TotalHours}h {movingTime.Minutes:00}m")
            : string.Create(CultureInfo.InvariantCulture, $"{movingTime.Minutes}m");

    /// <summary>A calendar day, unambiguous on any machine and in any locale.</summary>
    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
