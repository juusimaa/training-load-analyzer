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

    // User Story 2, scenario 6: the ISO week-numbering year runs ahead of the calendar year at
    // the end of December and behind it at the start of January. Values verified against the
    // real ISO calendar in research R5.
    [Theory]
    [InlineData(2026, 3, 1, 2026, 9)]     // Sunday closing 2026-W09, the day before 2026-W10
    [InlineData(2025, 12, 29, 2026, 1)]   // still December, already 2026-W01
    [InlineData(2027, 1, 3, 2026, 53)]    // already January, still 2026-W53
    [InlineData(2027, 1, 4, 2027, 1)]     // 2027-W01 opens here
    public void A_week_at_the_year_boundary_is_numbered_by_its_iso_year_not_its_calendar_year(
        int year, int month, int day, int expectedIsoYear, int expectedWeek)
    {
        var week = IsoWeek.For(new DateOnly(year, month, day));

        Assert.Equal(expectedIsoYear, week.Year);
        Assert.Equal(expectedWeek, week.Week);
    }

    // 2026 is a 53-week ISO year. Walking Mondays crosses into 2027-W01 with no gap and no
    // duplicate - which is exactly why the weekly series is enumerated that way rather than by
    // incrementing a week number (research R6).
    [Fact]
    public void Walking_mondays_crosses_the_fifty_third_week_of_2026_without_a_gap()
    {
        var week53 = IsoWeek.For(new DateOnly(2026, 12, 28));
        Assert.Equal((2026, 53), (week53.Year, week53.Week));

        var next = IsoWeek.For(week53.Monday.AddDays(7));

        Assert.Equal((2027, 1), (next.Year, next.Week));
        Assert.Equal(new DateOnly(2027, 1, 4), next.Monday);
        Assert.Equal(week53.Sunday.AddDays(1), next.Monday);
    }
}
