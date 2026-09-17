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
}
