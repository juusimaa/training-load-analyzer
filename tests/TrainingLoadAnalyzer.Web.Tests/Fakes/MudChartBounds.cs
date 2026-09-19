using Bunit;
using MudBlazor.Interop;

namespace TrainingLoadAnalyzer.Web.Tests.Fakes;

/// <summary>
///   Tells a rendered <c>MudChart</c> how big it is.
/// </summary>
/// <remarks>
///   <para>
///     <c>MatchBoundsToSize</c> makes the chart size its view box to the element it lands in, which
///     is what stops the plot being letterboxed inside its panel. The measurement comes from
///     JavaScript, and <c>MudAxisChartBase.OnParametersSet</c> does this with it:
///   </para>
///   <code>
///     if (MatchBoundsToSize &amp;&amp; _elementSize is null) return;   // RebuildChart never runs
///   </code>
///   <para>
///     Under bUnit there is no browser, and loose interop answers an unplanned call with
///     <c>default</c> — so <c>_elementSize</c> stays null, the chart builds no series, and it
///     renders with no lines and, more to the point, no legend. Since 007 Amendment 1 removed the
///     dash patterns, that legend is the only thing identifying one series from another (FR-013a),
///     so the tests guarding it must keep working.
///   </para>
///   <para>
///     Supplying the size is the honest fix: the assertions stay exactly as they were, and what
///     changes is only that the test now tells the component something the browser would have.
///   </para>
/// </remarks>
internal static class MudChartBounds
{
    /// <summary>A plausible desktop panel. Only that it is non-null and positive matters.</summary>
    public static void Supply(BunitJSInterop jsInterop) =>
        jsInterop
            .Setup<ElementSize>("mudObserveElementSize", _ => true)
            .SetResult(new ElementSize { Width = 800, Height = 240 });
}
