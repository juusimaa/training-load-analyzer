using System.Reflection;
using MudBlazor.Utilities;
using TrainingLoadAnalyzer.Web.Theme;

namespace TrainingLoadAnalyzer.Web.Tests.Theme;

/// <summary>
///   FR-025 and FR-028: the two palettes are a matched pair, not one palette and some overrides.
/// </summary>
public class PaletteSlotTests
{
    /// <summary>
    ///   The slots this feature sets by hand. Every one must be set in <em>both</em> palettes, or
    ///   a surface renders with MudBlazor's default in one scheme and the theme's value in the
    ///   other — the drift FR-028 exists to prevent.
    /// </summary>
    private static readonly string[] DeclaredSlots =
    [
        "Background",
        "Surface",
        "TextPrimary",
        "TextSecondary",
        "Primary",
        "AppbarBackground",
        "AppbarText",
        "Divider",
        "Error",
        "TextDisabled",
    ];

    [Fact]
    public void Both_palettes_declare_the_same_slots()
    {
        var light = Declared(TrainingLoadTheme.Instance.PaletteLight);
        var dark = Declared(TrainingLoadTheme.Instance.PaletteDark);

        Assert.Equal(DeclaredSlots.Order(), light.Order());
        Assert.Equal(light.Order(), dark.Order());
    }

    /// <summary>
    ///   A slot set to the same value in both palettes is almost always a copy-paste rather than a
    ///   decision. The one legitimate exception is the form series colour, which measures well
    ///   against both surfaces and is deliberately shared — and it lives in
    ///   <see cref="ChartPalette"/>, not here.
    /// </summary>
    [Fact]
    public void No_slot_carries_the_same_colour_in_both_schemes()
    {
        foreach (var slot in DeclaredSlots)
        {
            var light = Read(TrainingLoadTheme.Instance.PaletteLight, slot);
            var dark = Read(TrainingLoadTheme.Instance.PaletteDark, slot);

            Assert.False(
                string.Equals(light, dark, StringComparison.OrdinalIgnoreCase),
                $"'{slot}' is {light} in both schemes. A dark palette is a different tonal mapping, "
                + "not a copy of the light one.");
        }
    }

    private static IEnumerable<string> Declared(object palette) =>
        DeclaredSlots.Where(slot => Read(palette, slot) is not null)!;

    private static string? Read(object palette, string slot) =>
        (palette.GetType().GetProperty(slot, BindingFlags.Public | BindingFlags.Instance)?
            .GetValue(palette) as MudColor)?.Value;
}
