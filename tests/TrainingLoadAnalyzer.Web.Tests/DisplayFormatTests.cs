using System.Globalization;
using TrainingLoadAnalyzer.Web.Features.Dashboard;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   SC-002's worked example reads the same on every machine (C99, C100, research R9).
/// </summary>
public class DisplayFormatTests
{
    /// <summary>
    ///   Runs the assertion with the current culture set to one that uses a decimal comma — and a
    ///   U+2212 minus sign, which is the less obvious half of the same problem.
    /// </summary>
    private static void InFinnish(Action assertion)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fi-FI");

        try
        {
            assertion();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    ///   The three figures of fixture H1, as SC-002 writes them. Without a fixed culture this
    ///   machine renders `2,8` and `−13,2` with a typographic minus — which is not what the
    ///   specification says, and not a number any reader would paste anywhere.
    /// </summary>
    [Theory]
    [InlineData(2.8233976017308082, "2.8")]
    [InlineData(15.974652029978209, "16.0")]
    [InlineData(-13.151254428247402, "-13.2")]
    [InlineData(0.0, "0.0")]
    public void A_figure_renders_to_one_decimal_place_in_a_fixed_culture(double value, string expected)
        => InFinnish(() => Assert.Equal(expected, Display.Metric(value)));

    /// <summary>C100: a missing figure is an em dash, never a zero (US1 scenario 2).</summary>
    [Fact]
    public void A_missing_figure_renders_as_a_dash()
        => InFinnish(() => Assert.Equal("—", Display.Metric(null)));

    /// <summary>Training load is a decimal, and it rounds the same way.</summary>
    [Fact]
    public void Training_load_renders_the_same_way()
        => InFinnish(() => Assert.Equal("480.0", Display.Points(480m)));
}
