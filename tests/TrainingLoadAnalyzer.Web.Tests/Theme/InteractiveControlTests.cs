namespace TrainingLoadAnalyzer.Web.Tests.Theme;

/// <summary>
///   008 FR-016, FR-017 and FR-024: every control can be hit, seen when focused, and does not move
///   for anyone who asked it not to.
/// </summary>
/// <remarks>
///   Stylesheet-reading, for the reason every rule test in this project is: scoped
///   <c>.razor.css</c> compiles into a separate bundle that rendered markup never carries, and a
///   headless renderer has no notion of a computed height in any case. These assert the rules
///   exist and say the right thing; that a thumb actually lands on them is human-verified (T059).
/// </remarks>
public class InteractiveControlTests
{
    /// <summary>
    ///   FR-017: 48x48, and specifically not the reference design's 44px.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The reference page sets <c>.seg-opt { min-height: 44px }</c>. The four pixels matter
    ///     because the previous interface already met 48 — <c>app.css</c> carried a floor for
    ///     MudBlazor's buttons — so taking the reference's number would have been a regression
    ///     dressed as fidelity (008 research R9).
    ///   </para>
    ///   <para>
    ///     The error notice's "Reload" link is deliberately absent from this list. It sits inside a
    ///     sentence, which is the inline exception WCAG 2.5.5 makes: padding it to 48px would break
    ///     the line it is part of, to no one's benefit.
    ///   </para>
    /// </remarks>
    [Theory]
    [InlineData("Components/Pages/Dashboard.razor.css", ".seg-opt", "the window options")]
    [InlineData("Components/Pages/Dashboard.razor.css", ".connect", "the connect action")]
    [InlineData("Components/Dashboard/SyncPanel.razor.css", ".sync-panel .btn", "the sync and reconnect actions")]
    [InlineData("Components/Layout/ReconnectModal.razor.css", "#components-reconnect-modal button", "the reconnect dialog's actions")]
    public void Every_control_clears_forty_eight_pixels(string stylesheet, string selector, string what)
    {
        var declared = Stylesheet.Declaration(
            Stylesheet.UnderWeb([.. stylesheet.Split('/')]), selector, "min-height");

        Assert.True(
            declared == "48px",
            $"{what} declare min-height: {declared ?? "nothing"}, not the 48px FR-017 requires.");
    }

    /// <summary>
    ///   FR-016: a keyboard user can see where they are. The vendored sheet's own rule provides it
    ///   for every control at once, which is why no component declares a focus style of its own.
    /// </summary>
    [Fact]
    public void Keyboard_focus_draws_a_visible_ring()
    {
        var outline = Stylesheet.Declaration(Stylesheet.Broadsheet, ":focus-visible", "outline");

        Assert.False(
            string.IsNullOrWhiteSpace(outline),
            "Nothing draws a focus ring, so a keyboard user cannot see where they are (FR-016).");

        // The sheet also switches the default ring off, so the replacement is not optional.
        Assert.Equal("none", Stylesheet.Declaration(Stylesheet.Broadsheet, ":focus", "outline"));
        Assert.Contains("var(--color-accent)", outline!, StringComparison.Ordinal);
    }

    /// <summary>
    ///   007 FR-024, retained: the global reduced-motion reset survives the removal of the
    ///   component library whose ripples and state layers first made it necessary.
    /// </summary>
    /// <remarks>
    ///   Worth pinning precisely because its original reason is gone. A rule kept for a library
    ///   that is no longer here is the kind of thing a later cleanup deletes — and the reconnect
    ///   dialog still animates, so the reset still has work to do.
    /// </remarks>
    [Fact]
    public void The_reduced_motion_reset_is_still_in_place()
    {
        var reset = Stylesheet.MediaQuery(
            Stylesheet.UnderWeb("wwwroot", "app.css"), "prefers-reduced-motion: reduce");

        Assert.False(reset is null, "The global reduced-motion reset has been removed (007 FR-024).");
        Assert.Contains("animation-duration", reset!, StringComparison.Ordinal);
        Assert.Contains("transition-duration", reset, StringComparison.Ordinal);
    }
}
