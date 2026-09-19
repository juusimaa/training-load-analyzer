using MudBlazor;
using MudBlazor.Utilities;
using TrainingLoadAnalyzer.Web.Theme;

namespace TrainingLoadAnalyzer.Web.Tests.Theme;

/// <summary>
///   SC-005 and FR-019, asserted rather than eyeballed, in both palettes.
/// </summary>
/// <remarks>
///   Reads the theme object the application actually renders with, so there is no stylesheet to
///   parse and no chance of testing a copy of the palette rather than the palette
///   (007 research R6).
/// </remarks>
public class PaletteContrastTests
{
    public static TheoryData<string, Palette> Palettes => new()
    {
        { "light", TrainingLoadTheme.Instance.PaletteLight },
        { "dark", TrainingLoadTheme.Instance.PaletteDark },
    };

    /// <summary>FR-019: body text clears 4.5:1 against whatever surface it sits on.</summary>
    [Theory]
    [MemberData(nameof(Palettes))]
    public void Text_clears_four_and_a_half_to_one_on_every_surface(string scheme, Palette palette)
    {
        AssertAtLeast(4.5, palette.TextPrimary, palette.Background, scheme, "primary text on the page");
        AssertAtLeast(4.5, palette.TextPrimary, palette.Surface, scheme, "primary text on a card");
        AssertAtLeast(4.5, palette.TextSecondary, palette.Background, scheme, "secondary text on the page");
        AssertAtLeast(4.5, palette.TextSecondary, palette.Surface, scheme, "secondary text on a card");
    }

    /// <summary>The sync button's label, and the error notice (FR-014, FR-015).</summary>
    [Theory]
    [MemberData(nameof(Palettes))]
    public void Interactive_and_error_colours_clear_four_and_a_half_to_one(string scheme, Palette palette)
    {
        AssertAtLeast(4.5, palette.Primary, palette.Background, scheme, "primary on the page");
        AssertAtLeast(4.5, palette.Error, palette.Background, scheme, "error on the page");
        AssertAtLeast(4.5, palette.AppbarText, palette.AppbarBackground, scheme, "app bar title");
    }

    /// <summary>
    ///   The disabled sync button still carries the word "Syncing…", so its colour is text, not
    ///   decoration (FR-002). MudBlazor supplies this value itself unless the theme overrides it,
    ///   which is exactly why it is asserted rather than assumed (007 research R4).
    /// </summary>
    [Theory]
    [MemberData(nameof(Palettes))]
    public void The_disabled_label_is_still_readable(string scheme, Palette palette)
    {
        AssertAtLeast(4.5, palette.TextDisabled, palette.Surface, scheme, "disabled button label");
    }

    /// <summary>
    ///   FR-029: each series against the plot's background. Note it is each line against its
    ///   <em>background</em>, never against another line — the three are deliberately close in
    ///   luminance and chasing line-to-line contrast would wreck the palette for no gain
    ///   (007 research R5).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_chart_series_clears_three_to_one_against_the_plot(bool isDarkMode)
    {
        var palette = isDarkMode
            ? (Palette)TrainingLoadTheme.Instance.PaletteDark
            : TrainingLoadTheme.Instance.PaletteLight;

        var scheme = isDarkMode ? "dark" : "light";

        foreach (var (name, colour) in ChartPalette.Named(isDarkMode))
        {
            AssertAtLeast(3.0, colour, palette.Surface, scheme, $"the {name} series");
        }
    }

    /// <summary>
    ///   <para>
    ///     A deliberate exclusion, written down rather than left out.
    ///   </para>
    ///   <para>
    ///     <c>Divider</c> measures roughly 1.5:1 (light) and 1.8:1 (dark) against the card surface
    ///     and does <em>not</em> clear 3:1. It is used only for decorative separation, where the
    ///     grouping is already carried by spacing and elevation. WCAG 1.4.11 scopes its 3:1
    ///     requirement to indicators needed to identify components or understand content, and a
    ///     decorative divider is neither — which is why FR-019 is worded as "meaningful non-text
    ///     indicators".
    ///   </para>
    ///   <para>
    ///     This test exists so the exclusion is a decision with a reason attached. If a boundary
    ///     ever becomes the only thing carrying a meaning, it must stop using <c>Divider</c>.
    ///   </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Palettes))]
    public void The_divider_is_decorative_and_is_excluded_by_name(string scheme, Palette palette)
    {
        var measured = ContrastRatio.Between(palette.Divider.Value, palette.Surface.Value);

        Assert.True(
            measured < 3.0,
            $"The {scheme} divider now measures {measured:0.00}:1 against the card surface. If it "
            + "was raised deliberately it may be carrying meaning, and this exclusion should be "
            + "removed rather than updated.");
    }

    private static void AssertAtLeast(double minimum, MudColor foreground, MudColor background, string scheme, string what)
    {
        var measured = ContrastRatio.Between(foreground.Value, background.Value);

        Assert.True(
            measured >= minimum,
            $"In the {scheme} scheme, {what} measures {measured:0.00}:1 "
            + $"({foreground.Value} on {background.Value}), below the required {minimum:0.0}:1.");
    }
}
