namespace TrainingLoadAnalyzer.Web.Tests.Theme;

/// <summary>
///   008 FR-011 and FR-013: the rail and the content column stack cleanly on a narrow screen.
/// </summary>
/// <remarks>
///   <para>
///     Built like <see cref="ColourDisciplineTests"/> — it walks up to the repository and reads the
///     stylesheets off disk — and <strong>not</strong> as a bUnit test, which could not work. A
///     scoped <c>.razor.css</c> compiles into a separate bundle and never appears in rendered
///     markup, and a headless renderer has no viewport to narrow in any case.
///   </para>
///   <para>
///     So this asserts that the rules <em>exist</em> and say what they are supposed to say. That
///     they produce the right reflow at 320px, at 200% zoom and everywhere between is settled by
///     human review, which quickstart.md lists rather than implies (008 T053).
///   </para>
/// </remarks>
public class ResponsiveRulesTests
{
    /// <summary>The single breakpoint, stated once so a drifting copy is obvious.</summary>
    private const string Breakpoint = "max-width: 60rem";

    /// <summary>
    ///   Below the breakpoint the grid becomes one column and the rail stops sticking — a sticky
    ///   rail in a stacked layout pins a screen's worth of controls above the content the athlete
    ///   scrolled down to read.
    /// </summary>
    [Fact]
    public void Below_the_breakpoint_the_page_stacks_and_the_rail_stops_sticking()
    {
        var narrow = Narrow(Stylesheet.Dashboard);

        Assert.Equal("1fr", Stylesheet.DeclarationIn(narrow, ".page", "grid-template-columns"));
        Assert.Equal("static", Stylesheet.DeclarationIn(narrow, ".rail", "position"));
    }

    /// <summary>
    ///   The four figures become two rows of two rather than four columns 80px wide.
    /// </summary>
    /// <remarks>
    ///   Read from <c>MetricRow.razor.css</c> and not from the page's, which is where the rule has
    ///   to live: scoped CSS carries the owning component's scope attribute, so a rule written in
    ///   <c>Dashboard.razor.css</c> would never match an element <c>MetricRow</c> rendered.
    /// </remarks>
    [Fact]
    public void Below_the_breakpoint_the_metric_row_becomes_two_columns()
    {
        var narrow = Narrow(Stylesheet.UnderWeb("Components", "Dashboard", "MetricRow.razor.css"));

        Assert.Equal("repeat(2, 1fr)", Stylesheet.DeclarationIn(narrow, ".metrics", "grid-template-columns"));
    }

    /// <summary>
    ///   FR-013 at the other end: on a wide display the page does not run to the edges, and prose
    ///   does not run to the width of the chart beneath it.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Two caps, because they do different jobs. The page's keeps the whole grid from spanning
    ///     a 2560px display; the prose blocks' keeps an explanation to a measure the eye can track
    ///     back from, which the page cap alone would not — the content column is still some 930px
    ///     wide inside it, which is right for a chart and a table and too long for a paragraph.
    ///   </para>
    ///   <para>
    ///     Deliberately <em>not</em> a <c>max-width</c> on <c>.content</c> itself. Inside a page
    ///     already capped at 1240px such a rule would never bind, and a declaration that can never
    ///     take effect is worse than none: it reads as a guarantee and is not one.
    ///   </para>
    /// </remarks>
    [Fact]
    public void On_a_wide_display_the_page_and_its_prose_are_both_capped()
    {
        Assert.Equal("1240px", Stylesheet.Declaration(Stylesheet.Dashboard, ".page", "max-width"));

        var prose = Stylesheet.Declaration(Stylesheet.Dashboard, ".state", "max-width");

        Assert.False(
            string.IsNullOrWhiteSpace(prose),
            "An explanation runs the full width of the content column, which is a chart's measure "
            + "rather than a paragraph's (FR-013).");
    }

    /// <summary>
    ///   FR-014: the plot fills whatever width it is given rather than a fixed one, so it scales
    ///   with the column instead of overflowing it or leaving a gap beside it.
    /// </summary>
    [Fact]
    public void The_chart_scales_to_its_container()
    {
        Assert.Equal("100%", Stylesheet.Declaration(Stylesheet.MetricsChart, ".plot", "width"));
    }

    private static string Narrow(string stylesheet)
    {
        var narrow = Stylesheet.MediaQuery(stylesheet, Breakpoint);

        Assert.False(
            narrow is null,
            $"{Path.GetFileName(stylesheet)} declares no '{Breakpoint}' media query, so nothing "
            + "makes it stack (FR-011).");

        return narrow!;
    }
}
