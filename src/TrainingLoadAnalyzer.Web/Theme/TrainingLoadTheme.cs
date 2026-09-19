using MudBlazor;

namespace TrainingLoadAnalyzer.Web.Theme;

/// <summary>
///   The application's entire visual system: both colour schemes, the type scale and the shape of
///   its surfaces (007 FR-004, FR-025).
/// </summary>
/// <remarks>
///   <para>
///     One definition site, as 007 NFR-002 requires — a change to a colour here takes effect
///     everywhere it is used. Colour lives in C# rather than in a stylesheet for a second reason:
///     it lets <c>PaletteContrastTests</c> read the object the application actually renders with,
///     instead of parsing CSS and hoping the two agree (007 research R2, R6).
///   </para>
///   <para>
///     Every value below was measured before it was written. The margins are in 007 research R4;
///     the thing to know is that both schemes clear their WCAG minimums, and the test will say so
///     if an edit stops that being true.
///   </para>
///   <para>
///     Only the slots this application uses are set. Material 3 defines roughly thirty colour
///     roles; setting the eleven that are actually rendered keeps the contrast test's obligations
///     honest and avoids declaring colours nothing consumes (Principle III).
///   </para>
/// </remarks>
public static class TrainingLoadTheme
{
    /// <summary>The theme the application renders with. Read by the layout and by its tests.</summary>
    public static MudTheme Instance { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Background = "#FDFCFF",
            Surface = "#F1F3F9",
            TextPrimary = "#1A1C1E",
            TextSecondary = "#43474E",
            TextDisabled = "#5C6068",
            Primary = "#00639B",
            AppbarBackground = "#00639B",
            AppbarText = "#FFFFFF",

            // Decorative only. It does not clear 3:1 against the card surface and is not meant to
            // - grouping is carried by spacing and elevation. PaletteContrastTests excludes it by
            // name, with the WCAG 1.4.11 reasoning written out there.
            Divider = "#C3C7CF",

            Error = "#BA1A1A",
        },

        PaletteDark = new PaletteDark
        {
            Background = "#111318",
            Surface = "#1E2025",
            TextPrimary = "#E2E2E6",
            TextSecondary = "#C3C7CF",
            TextDisabled = "#8D9199",
            Primary = "#96CCFF",

            // Not the primary colour, unlike the light scheme. A saturated blue bar against a dark
            // page reads as a light-scheme leftover; Material's dark guidance raises the surface
            // tone instead of carrying the accent across.
            AppbarBackground = "#1E2025",
            AppbarText = "#E2E2E6",

            Divider = "#43474E",
            Error = "#FFB4AB",
        },

        Typography = new Typography
        {
            H4 = new H4Typography { FontSize = "1.75rem", FontWeight = "400", LineHeight = "1.3" },
            H6 = new H6Typography { FontSize = "1.125rem", FontWeight = "500", LineHeight = "1.4" },

            // The three headline figures and the weekly total. Deliberately the largest thing on
            // the page: SC-008 asks an unfamiliar athlete to find them within five seconds.
            H3 = new H3Typography { FontSize = "2.5rem", FontWeight = "400", LineHeight = "1.1" },

            Body1 = new Body1Typography { FontSize = "0.9375rem", FontWeight = "400", LineHeight = "1.5" },
            Caption = new CaptionTypography { FontSize = "0.8125rem", FontWeight = "500", LineHeight = "1.4" },
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
        },
    };
}
