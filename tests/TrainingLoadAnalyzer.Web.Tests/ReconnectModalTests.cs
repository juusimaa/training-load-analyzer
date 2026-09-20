using Bunit;
using TrainingLoadAnalyzer.Web.Components.Layout;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   The connection-lost notice, which had no test at all until feature 008.
/// </summary>
/// <remarks>
///   <para>
///     Written because <c>ReconnectModal</c> is the heaviest consumer of the old palette — seven
///     <c>--mud-palette-*</c> references, more than any other file — and repointing all seven onto
///     the Broadsheet tokens would otherwise have been the one change in this feature that nothing
///     verified (008 plan.md, Complexity Tracking).
///   </para>
///   <para>
///     What it can assert is the content: the three things the modal says as a circuit drops, and
///     the two actions it offers. Whether the restyled dialog <em>looks</em> right in either scheme
///     is listed for human review in quickstart.md rather than asserted vacuously here — but a
///     stylesheet that stopped resolving its colours from the tokens is caught, because
///     <c>ColourDisciplineTests</c> reads this file off disk.
///   </para>
/// </remarks>
public class ReconnectModalTests : BunitContext
{
    /// <summary>
    ///   Every message the athlete can be shown while the circuit is down. They are all present in
    ///   the markup at once and revealed by class, so rendering is enough to assert them.
    /// </summary>
    [Fact]
    public void The_modal_states_every_stage_of_a_lost_connection()
    {
        var modal = Render<ReconnectModal>();

        foreach (var stage in new[]
                 {
                     "Rejoining the server...",
                     "Rejoin failed...",
                     "Failed to rejoin.",
                     "The session has been paused by the server.",
                     "Failed to resume the session.",
                 })
        {
            Assert.Contains(stage, modal.Markup, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///   The two ways out. A notice that explains a dropped connection and offers nothing to do
    ///   about it is a dead end, which is the same objection FR-017 makes of the connect prompt.
    /// </summary>
    [Fact]
    public void The_modal_offers_a_way_to_retry_and_a_way_to_resume()
    {
        var modal = Render<ReconnectModal>();

        Assert.Equal("Retry", modal.Find("#components-reconnect-button").TextContent.Trim());
        Assert.Equal("Resume", modal.Find("#components-resume-button").TextContent.Trim());
    }

    /// <summary>
    ///   008 FR-010: the modal draws from the same tokens as every other surface, and names no
    ///   colour of its own.
    /// </summary>
    /// <remarks>
    ///   A stylesheet-reading assertion, and it has to be: a scoped <c>.razor.css</c> compiles into
    ///   a separate bundle and never reaches the markup bUnit renders. Its seven
    ///   <c>--mud-palette-*</c> references are the thing this feature removes, and this is what
    ///   notices if one comes back.
    /// </remarks>
    [Fact]
    public void The_modal_draws_from_the_broadsheet_tokens_and_not_the_old_palette()
    {
        var stylesheet = File.ReadAllText(
            Theme.Stylesheet.UnderWeb("Components", "Layout", "ReconnectModal.razor.css"));

        Assert.DoesNotContain("--mud-palette", stylesheet, StringComparison.Ordinal);
        Assert.DoesNotContain("--mud-elevation", stylesheet, StringComparison.Ordinal);
        Assert.Contains("var(--color-", stylesheet, StringComparison.Ordinal);
    }
}
