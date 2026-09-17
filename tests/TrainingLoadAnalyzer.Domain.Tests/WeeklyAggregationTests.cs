namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   User Story 2 — see how much training each week carried.
/// </summary>
public class WeeklyAggregationTests
{
    private const int MaximumHeartRate = 190;

    private static TrainingActivity EstimatedActivity(
        string externalId,
        DateOnly day,
        int movingMinutes) =>
        new(
            externalId,
            new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(movingMinutes),
            ActivityType.Running);

    // User Story 2, scenario 1 (FR-002): activities in the same ISO week are summed into it.
    [Fact]
    public void Activities_in_the_same_iso_week_are_summed_into_that_week()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", new DateOnly(2026, 3, 2), movingMinutes: 15),
            EstimatedActivity("A-2", new DateOnly(2026, 3, 4), movingMinutes: 20),
            EstimatedActivity("A-3", new DateOnly(2026, 3, 6), movingMinutes: 25),
        };
        var range = new DateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 8));

        var weekly = TrainingLoadAggregator.AggregateWeekly(activities, range, MaximumHeartRate);

        var week = Assert.Single(weekly);
        Assert.Equal(IsoWeek.For(new DateOnly(2026, 3, 2)), week.Week);
        Assert.Equal(120m, week.Points);
    }

    // User Story 2, scenario 2 (SC-008): the week boundary is Monday, and Sunday belongs to the
    // week that precedes it.
    [Fact]
    public void A_sunday_and_the_monday_after_it_fall_in_different_weeks()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", new DateOnly(2026, 3, 1), movingMinutes: 15),
            EstimatedActivity("A-2", new DateOnly(2026, 3, 2), movingMinutes: 20),
        };
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 8));

        var weekly = TrainingLoadAggregator.AggregateWeekly(activities, range, MaximumHeartRate);

        Assert.Equal(2, weekly.Count);
        Assert.Equal((2026, 9), (weekly[0].Week.Year, weekly[0].Week.Week));
        Assert.Equal(30m, weekly[0].Points);
        Assert.Equal((2026, 10), (weekly[1].Week.Year, weekly[1].Week.Week));
        Assert.Equal(40m, weekly[1].Points);
    }

    // SC-008 at the exact instants: the first minute of Monday opens the new week, and the last
    // minute of the Sunday before still belongs to the old one.
    [Fact]
    public void The_first_minute_of_monday_and_the_last_of_sunday_fall_in_different_weeks()
    {
        var lastMinuteOfSunday = new DateTimeOffset(2026, 3, 1, 23, 59, 0, TimeSpan.FromHours(2));
        var firstMinuteOfMonday = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.FromHours(2));

        var activities = new[]
        {
            new TrainingActivity("A-1", lastMinuteOfSunday, TimeSpan.FromMinutes(15), ActivityType.Running),
            new TrainingActivity("A-2", firstMinuteOfMonday, TimeSpan.FromMinutes(20), ActivityType.Running),
        };
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 8));

        var weekly = TrainingLoadAggregator.AggregateWeekly(activities, range, MaximumHeartRate);

        Assert.Equal(2, weekly.Count);
        Assert.Equal(30m, weekly[0].Points);
        Assert.Equal(40m, weekly[1].Points);
    }
}
