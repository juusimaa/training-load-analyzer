namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   ISO-8601 week identity: Monday to Sunday, numbered in the ISO week-numbering year, which is
///   not always the calendar year of its days (FR-002).
/// </summary>
public class IsoWeekTests
{
    // FR-002: the week containing Monday 2026-03-02 is 2026-W10, running 03-02 to 03-08.
    [Fact]
    public void A_week_knows_its_iso_year_number_and_both_of_its_ends()
    {
        var week = IsoWeek.For(new DateOnly(2026, 3, 2));

        Assert.Equal(2026, week.Year);
        Assert.Equal(10, week.Week);
        Assert.Equal(new DateOnly(2026, 3, 2), week.Monday);
        Assert.Equal(new DateOnly(2026, 3, 8), week.Sunday);
    }
}
