namespace TrainingLoadAnalyzer.Web.Tests.Theme;

/// <summary>
///   008 FR-015 and SC-005, measured rather than eyeballed, in both appearances.
/// </summary>
/// <remarks>
///   <para>
///     Repointed for feature 008 from MudBlazor's <c>Palette</c> to the custom properties and rules
///     of <c>wwwroot/Theme/broadsheet.css</c> (research R7). <see cref="ContrastRatio"/> is
///     unchanged — it takes hex strings and never knew what a <c>MudColor</c> was.
///   </para>
///   <para>
///     Most pairings are read from the <em>rule</em> that draws them rather than from the token
///     they are supposed to use. That distinction is the whole audit: the reference design's
///     primary button clears no bar at all while the ramp it could have used clears it comfortably,
///     and a test that measured the ramp would have reported success (research R4).
///   </para>
///   <para>
///     Interactive states are measured alongside the resting one. Stepping the button's base down
///     to clear 4.5:1 left its inherited hover value <em>lighter</em> than the new base — an
///     accessibility defect introduced by the accessibility fix. A resting-only audit would not
///     have seen it.
///   </para>
/// </remarks>
public class PaletteContrastTests
{
    public static TheoryData<string> Schemes => new() { "light", "dark" };

    private static TokenSet For(string scheme) =>
        scheme == "dark" ? BroadsheetTokens.Dark : BroadsheetTokens.Light;

    // ---- Text (4.5:1) ----

    [Theory]
    [MemberData(nameof(Schemes))]
    public void Body_text_clears_four_and_a_half_to_one_on_the_page_and_on_a_surface(string scheme)
    {
        var tokens = For(scheme);

        AssertAtLeast(4.5, tokens, "--color-text", "--color-bg", "body text on the page");
        AssertAtLeast(4.5, tokens, "--color-text", "--color-surface", "body text on a surface");
    }

    /// <summary>
    ///   The primary button's label against its own background, at rest and in both of the states
    ///   the sheet defines for it. Read from the rules, because the state ramp is where the
    ///   reference design's own remedy went wrong.
    /// </summary>
    [Theory]
    [MemberData(nameof(Schemes))]
    public void The_primary_button_label_clears_four_and_a_half_to_one_in_every_state(string scheme)
    {
        var tokens = For(scheme);

        foreach (var (selector, state) in new[]
                 {
                     (".btn-primary", "at rest"),
                     (".btn-primary:hover", "on hover"),
                     (".btn-primary:active", "when pressed"),
                 })
        {
            var background = Rule(Stylesheet.Broadsheet, selector, "background");
            var label = Rule(Stylesheet.Broadsheet, ".btn-primary", "color");

            AssertAtLeast(4.5, tokens, label, background, $"the primary button's label {state}");
        }
    }

    /// <summary>Links, the ghost button and the outline tag all take the same accent as text.</summary>
    [Theory]
    [MemberData(nameof(Schemes))]
    public void Accent_text_clears_four_and_a_half_to_one_against_the_page(string scheme)
    {
        var tokens = For(scheme);

        AssertAtLeast(4.5, tokens, Rule(Stylesheet.Broadsheet, "a", "color"), "--color-bg", "a link");
        AssertAtLeast(4.5, tokens, Rule(Stylesheet.Broadsheet, ".btn-ghost", "color"), "--color-bg", "a ghost button");
        AssertAtLeast(4.5, tokens, Rule(Stylesheet.Broadsheet, ".tag-outline", "color"), "--color-bg", "an outline tag");
    }

    /// <summary>
    ///   The muted greys. They are written as ink at a percentage, so what a reader measures
    ///   depends on what they sit on — the resolver composites them over the page before measuring.
    /// </summary>
    [Theory]
    [MemberData(nameof(Schemes))]
    public void Muted_text_clears_four_and_a_half_to_one_against_the_page(string scheme)
    {
        var tokens = For(scheme);

        AssertAtLeast(4.5, tokens, Rule(Stylesheet.Broadsheet, ".table th", "color"), "--color-bg", "a table heading");
        AssertAtLeast(4.5, tokens, Rule(Stylesheet.Broadsheet, ".text-muted", "color"), "--color-bg", "muted text");
        AssertAtLeast(4.5, tokens, Rule(Stylesheet.Broadsheet, "figcaption", "color"), "--color-bg", "a caption");
    }

    // ---- Non-text indicators (3:1) ----

    /// <summary>
    ///   The four display figures. Large text, so 3:1 — but the two that carry a series colour are
    ///   stepped down their ramp anyway, because a figure the size of a headline is still the thing
    ///   the athlete came to read.
    /// </summary>
    [Theory]
    [MemberData(nameof(Schemes))]
    public void The_display_figures_clear_three_to_one_against_the_page(string scheme)
    {
        var tokens = For(scheme);

        AssertAtLeast(3.0, tokens, "--color-accent-700", "--color-bg", "the fitness figure");
        AssertAtLeast(3.0, tokens, "--color-accent-2-700", "--color-bg", "the fatigue figure");
        AssertAtLeast(3.0, tokens, "--color-text", "--color-bg", "the form and week figures");
    }

    /// <summary>
    ///   The chart's three strokes and its zero rule, read from the component's own stylesheet.
    ///   <para>
    ///     Each line is measured against its <em>background</em>, never against another line. The
    ///     three are deliberately close in luminance and chasing line-to-line contrast would wreck
    ///     the palette for no gain; what distinguishes them is the legend and, since this feature
    ///     restored it, the dash pattern.
    ///   </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Schemes))]
    public void Every_chart_line_clears_three_to_one_against_the_plot(string scheme)
    {
        var tokens = For(scheme);

        AssertAtLeast(3.0, tokens, Rule(Stylesheet.MetricsChart, ".series-fitness", "stroke"), "--color-bg", "the fitness line");
        AssertAtLeast(3.0, tokens, Rule(Stylesheet.MetricsChart, ".series-fatigue", "stroke"), "--color-bg", "the fatigue line");
        AssertAtLeast(3.0, tokens, Rule(Stylesheet.MetricsChart, ".series-form", "stroke"), "--color-bg", "the form line");
        AssertAtLeast(3.0, tokens, Rule(Stylesheet.MetricsChart, ".zero-rule", "stroke"), "--color-bg", "the zero rule");
    }

    /// <summary>
    ///   The chart's text: its date axis, and the legend label for the one series whose swatch is a
    ///   neutral rather than an accent. Both are text at 14px, so both need 4.5:1 — the reference
    ///   draws them at a step that clears only 3.85:1 (research R4).
    /// </summary>
    [Theory]
    [MemberData(nameof(Schemes))]
    public void The_charts_own_text_clears_four_and_a_half_to_one(string scheme)
    {
        var tokens = For(scheme);

        AssertAtLeast(4.5, tokens, Rule(Stylesheet.MetricsChart, ".chart-axis", "color"), "--color-bg", "the date axis");
        AssertAtLeast(4.5, tokens, Rule(Stylesheet.MetricsChart, ".legend-form", "color"), "--color-bg", "the Form legend label");
    }

    /// <summary>
    ///   The hover readout is measured by being put somewhere already measured.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Its contents are ordinary ink and the three accented labels, every one of which this
    ///     class already measures against <c>--color-bg</c>. That only holds while the readout
    ///     actually sits on <c>--color-bg</c> — moving it to the surface tone would quietly take
    ///     four pairings out of the audit without failing anything.
    ///   </para>
    ///   <para>
    ///     So the ground is asserted rather than assumed. This is the cheap half of a decision the
    ///     other half of which was visual: a paper-coloured box with a hairline reads as a clipping
    ///     laid on the page, which is what the design wants anyway.
    ///   </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Schemes))]
    public void The_hover_readout_sits_on_ground_this_audit_already_measures(string scheme)
    {
        var tokens = For(scheme);
        var ground = Rule(Stylesheet.MetricsChart, ".readout", "background");

        Assert.Equal(tokens.Resolve("var(--color-bg)"), tokens.Resolve(ground));

        AssertAtLeast(4.5, tokens, "--color-text", ground, "a readout figure");
        AssertAtLeast(4.5, tokens, Rule(Stylesheet.MetricsChart, ".legend-form", "color"), ground, "a readout label");
    }

    /// <summary>
    ///   <para>
    ///     The one exclusion, named rather than omitted — Amendment 1(b) to the specification.
    ///   </para>
    ///   <para>
    ///     The chart's daily-load bars measure 1.33:1 against the page and do not clear 3:1. The
    ///     full neutral ramp against the page is 1.02 / 1.10 / 1.33 / 1.80 / 2.59 / 3.85 / 5.83 /
    ///     9.04 / 12.60:1, so the lightest step that would comply is <c>neutral-600</c> — the same
    ///     value as the Form line. Complying would give the backdrop the visual weight of the data
    ///     and collapse the figure-and-ground separation the design is built on.
    ///   </para>
    ///   <para>
    ///     They are therefore treated as contextual backdrop rather than an indicator needed to
    ///     understand the content: the three lines carry the chart's message, the chart is titled
    ///     with its window, and every underlying daily figure is also published as text in the
    ///     recent-sessions table.
    ///   </para>
    ///   <para>
    ///     This is written as an assertion, not as a gap. If the bars are ever darkened past 3:1
    ///     they may have started carrying meaning, and the exemption should be removed rather than
    ///     updated.
    ///   </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Schemes))]
    public void The_load_bars_are_a_backdrop_and_are_excluded_by_name(string scheme)
    {
        var tokens = For(scheme);
        var bars = Rule(Stylesheet.MetricsChart, ".load-bar", "fill");

        Assert.False(
            bars is null,
            "The load-bar exemption names a rule that no longer exists. An exclusion for something "
            + "that is not drawn is not an exclusion.");

        var measured = ContrastRatio.Between(tokens.Resolve(bars!), tokens.Resolve("var(--color-bg)"));

        Assert.True(
            measured < 3.0,
            $"In the {scheme} scheme the load bars now measure {measured:0.00}:1 against the page. "
            + "If they were darkened deliberately they may be carrying meaning, and Amendment 1(b) "
            + "should be withdrawn rather than this exclusion updated.");
    }

    // ---- Helpers ----

    /// <summary>
    ///   A declared value, or a failure naming the rule. A missing rule is a real RED state — the
    ///   surface it was meant to colour is being drawn with something nobody chose.
    /// </summary>
    private static string Rule(string stylesheet, string selector, string property)
    {
        var declared = Stylesheet.Declaration(stylesheet, selector, property);

        Assert.True(
            declared is not null,
            $"'{selector} {{ {property} }}' is not declared in {Path.GetFileName(stylesheet)}, so "
            + "nothing pins the colour this pairing is supposed to measure.");

        return declared!;
    }

    private static void AssertAtLeast(
        double minimum,
        TokenSet tokens,
        string foreground,
        string background,
        string what)
    {
        var ground = tokens.Resolve(Expression(background));
        var ink = tokens.Resolve(Expression(foreground), Expression(background));

        var measured = ContrastRatio.Between(ink, ground);

        Assert.True(
            measured >= minimum,
            $"In the {tokens.Scheme} scheme, {what} measures {measured:0.00}:1 ({ink} on {ground}), "
            + $"below the required {minimum:0.0}:1.");
    }

    /// <summary>A bare token name is shorthand for reading it; anything else is an expression.</summary>
    private static string Expression(string value) =>
        value.StartsWith("--", StringComparison.Ordinal) ? $"var({value})" : value;
}
