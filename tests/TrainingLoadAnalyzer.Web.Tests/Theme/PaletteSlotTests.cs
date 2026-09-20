namespace TrainingLoadAnalyzer.Web.Tests.Theme;

/// <summary>
///   008 FR-020..FR-023: the two schemes are a matched pair, not one palette and some overrides.
/// </summary>
/// <remarks>
///   <para>
///     Repointed for feature 008. It used to reflect over MudBlazor's <c>Palette</c> off
///     <c>TrainingLoadTheme.Instance</c>; that type is gone, and the tokens now live in
///     <c>wwwroot/Theme/broadsheet.css</c>. The claim is unchanged — every slot the interface
///     draws with is declared in both appearances — and only where it is read from has moved
///     (008 research R7).
///   </para>
/// </remarks>
public class PaletteSlotTests
{
    /// <summary>
    ///   Every token group in the feature's data model. A missing one is a surface rendering with
    ///   the browser's default in one scheme and the design's value in the other.
    /// </summary>
    public static readonly string[] RequiredTokens =
    [
        // Ground. --color-scrim is beyond data-model.md §2's list: feature 008 added it for the
        // reconnect dialog's backdrop, and it is covered here for exactly the reason the others
        // are — a scrim that fell through from the light block would wash out on a dark device.
        "--color-bg", "--color-surface", "--color-text", "--color-divider", "--color-scrim",

        // Accents
        "--color-accent", "--color-accent-2",

        // Neutral ramp
        "--color-neutral-100", "--color-neutral-200", "--color-neutral-300",
        "--color-neutral-400", "--color-neutral-500", "--color-neutral-600",
        "--color-neutral-700", "--color-neutral-800", "--color-neutral-900",

        // Accent ramps
        "--color-accent-100", "--color-accent-200", "--color-accent-300",
        "--color-accent-400", "--color-accent-500", "--color-accent-600",
        "--color-accent-700", "--color-accent-800", "--color-accent-900",
        "--color-accent-2-100", "--color-accent-2-200", "--color-accent-2-300",
        "--color-accent-2-400", "--color-accent-2-500", "--color-accent-2-600",
        "--color-accent-2-700", "--color-accent-2-800", "--color-accent-2-900",

        // Type
        "--font-heading", "--font-heading-weight", "--font-body",

        // Spacing
        "--space-1", "--space-2", "--space-3", "--space-4", "--space-6", "--space-8",

        // Radius
        "--radius-sm", "--radius-md", "--radius-lg",

        // Elevation
        "--shadow-sm", "--shadow-md", "--shadow-lg",
    ];

    /// <summary>
    ///   The tokens whose value is a colour, and which therefore have to be re-decided rather than
    ///   inherited when the ground inverts. The shadows are here because an ink-tinted shadow on a
    ///   dark ground is invisible — the sheet's own token comment says so.
    /// </summary>
    private static IEnumerable<string> ColourTokens =>
        RequiredTokens.Where(t => t.StartsWith("--color-", StringComparison.Ordinal)
                                  || t.StartsWith("--shadow-", StringComparison.Ordinal));

    [Fact]
    public void The_light_scheme_declares_every_required_token()
    {
        AssertTheSheetExists();
        AssertComplete(BroadsheetTokens.Light);
    }

    /// <summary>
    ///   The same completeness, resolved as a device set to a dark appearance would resolve it:
    ///   the light block with the media query applied over it, which is what the browser does.
    /// </summary>
    [Fact]
    public void The_dark_scheme_declares_every_required_token()
    {
        AssertTheSheetExists();
        AssertComplete(BroadsheetTokens.Dark);
    }

    /// <summary>
    ///   <para>
    ///     Every colour is redefined by the dark media query itself.
    ///   </para>
    ///   <para>
    ///     Asserted against the overrides rather than the resolved set, and only for the colours.
    ///     A spacing step or a font stack is the same decision in both appearances and repeating it
    ///     would be dead CSS (Principle III); a colour that falls through from the light block is
    ///     the drift this test exists to catch — the failure nobody with a light device ever sees.
    ///   </para>
    /// </summary>
    [Fact]
    public void Every_colour_is_re_decided_by_the_dark_scheme_rather_than_inherited()
    {
        AssertTheSheetExists();

        var overrides = BroadsheetTokens.DarkOverrides;

        var inherited = ColourTokens.Where(token => !overrides.ContainsKey(token)).ToList();

        Assert.True(
            inherited.Count == 0,
            "These colours fall through from the light block into the dark scheme: "
            + string.Join(", ", inherited));
    }

    /// <summary>
    ///   A colour set to the same value in both schemes is almost always a copy-paste rather than a
    ///   decision. A dark palette is a different tonal mapping, not a copy of the light one.
    /// </summary>
    [Fact]
    public void No_colour_carries_the_same_value_in_both_schemes()
    {
        AssertTheSheetExists();

        var light = BroadsheetTokens.Light;
        var dark = BroadsheetTokens.Dark;

        foreach (var token in ColourTokens)
        {
            Assert.False(
                string.Equals(light.Raw(token), dark.Raw(token), StringComparison.OrdinalIgnoreCase),
                $"'{token}' is {light.Raw(token)} in both schemes.");
        }
    }

    /// <summary>The sheet's absence is one failure to read, not forty-eight.</summary>
    internal static void AssertTheSheetExists() =>
        Assert.True(
            BroadsheetTokens.StylesheetExists,
            $"The vendored stylesheet is missing: {BroadsheetTokens.StylesheetPath}");

    private static void AssertComplete(TokenSet tokens)
    {
        var missing = RequiredTokens.Where(token => !tokens.Declares(token)).ToList();

        Assert.True(
            missing.Count == 0,
            $"The {tokens.Scheme} scheme does not declare: {string.Join(", ", missing)}");
    }
}
