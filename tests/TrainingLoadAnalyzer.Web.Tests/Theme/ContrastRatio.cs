using System.Globalization;

namespace TrainingLoadAnalyzer.Web.Tests.Theme;

/// <summary>
///   WCAG 2.1 relative luminance and contrast ratio, for asserting the theme's palettes.
/// </summary>
/// <remarks>
///   <para>
///     Test-side deliberately. It verifies the product rather than shipping in it, and putting it
///     in <c>src/</c> would add production code that nothing in production calls — the kind of
///     speculative surface Principle III exists to prevent (007 research R6).
///   </para>
///   <para>
///     SC-005 would otherwise be eyeballed, and would rot the first time a colour was nudged.
///     With this, the criterion has a RED state.
///   </para>
/// </remarks>
internal static class ContrastRatio
{
    /// <summary>
    ///   The ratio between two colours, from 1.0 (identical) to 21.0 (black against white).
    ///   Order does not matter: the lighter of the two is always the numerator.
    /// </summary>
    public static double Between(string firstHex, string secondHex)
    {
        var first = RelativeLuminance(firstHex);
        var second = RelativeLuminance(secondHex);

        var lighter = Math.Max(first, second);
        var darker = Math.Min(first, second);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>WCAG 2.1's relative luminance, from a <c>#RRGGBB</c> or <c>#RGB</c> string.</summary>
    private static double RelativeLuminance(string hex)
    {
        var (red, green, blue) = Parse(hex);

        return (0.2126 * Channel(red)) + (0.7152 * Channel(green)) + (0.0722 * Channel(blue));
    }

    private static double Channel(double value) =>
        value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);

    /// <summary>
    ///   MudBlazor's <c>MudColor</c> renders as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>; both are
    ///   accepted, and an alpha channel is ignored because the palette is used opaquely.
    /// </summary>
    private static (double Red, double Green, double Blue) Parse(string hex)
    {
        var value = hex.TrimStart('#');

        if (value.Length == 3)
        {
            value = string.Concat(value.Select(c => new string(c, 2)));
        }

        if (value.Length is not (6 or 8))
        {
            throw new FormatException($"'{hex}' is not a colour this test can read.");
        }

        return (
            Component(value, 0),
            Component(value, 2),
            Component(value, 4));
    }

    private static double Component(string value, int offset) =>
        int.Parse(value.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
}
