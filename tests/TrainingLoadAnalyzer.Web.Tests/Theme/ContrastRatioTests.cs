namespace TrainingLoadAnalyzer.Web.Tests.Theme;

/// <summary>
///   The measuring instrument, checked against known values before anything is measured with it.
/// </summary>
public class ContrastRatioTests
{
    /// <summary>The definitional extreme: 21:1 is the maximum WCAG 2.1 can express.</summary>
    [Fact]
    public void Black_against_white_is_the_maximum_ratio()
    {
        Assert.Equal(21.0, ContrastRatio.Between("#000000", "#FFFFFF"), precision: 2);
    }

    [Fact]
    public void A_colour_against_itself_is_one()
    {
        Assert.Equal(1.0, ContrastRatio.Between("#00639B", "#00639B"), precision: 2);
    }

    /// <summary>
    ///   The value computed during 007 planning for the light scheme's primary against its page
    ///   background. If this drifts, the instrument changed, not the palette.
    /// </summary>
    [Fact]
    public void The_light_primary_against_its_background_measures_as_planned()
    {
        Assert.Equal(6.31, ContrastRatio.Between("#00639B", "#FDFCFF"), precision: 2);
    }

    [Fact]
    public void The_order_of_the_two_colours_does_not_matter()
    {
        Assert.Equal(
            ContrastRatio.Between("#1A1C1E", "#FDFCFF"),
            ContrastRatio.Between("#FDFCFF", "#1A1C1E"),
            precision: 10);
    }

    [Fact]
    public void A_three_digit_hex_expands_to_its_six_digit_form()
    {
        Assert.Equal(
            ContrastRatio.Between("#000", "#FFF"),
            ContrastRatio.Between("#000000", "#FFFFFF"),
            precision: 10);
    }
}
