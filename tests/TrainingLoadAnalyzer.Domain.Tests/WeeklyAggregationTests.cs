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

    // User Story 2, scenario 3 (FR-006, FR-007): a week nobody trained in is a real zero week,
    // not a missing entry. 2026-03-02 to 2026-03-29 is exactly four ISO weeks, W10 to W13.
    [Fact]
    public void Every_week_the_range_touches_is_reported_including_weeks_with_no_training()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", new DateOnly(2026, 3, 3), movingMinutes: 15),   // W10
            EstimatedActivity("A-2", new DateOnly(2026, 3, 10), movingMinutes: 20),  // W11
            EstimatedActivity("A-3", new DateOnly(2026, 3, 24), movingMinutes: 25),  // W13
        };
        var range = new DateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 29));

        var weekly = TrainingLoadAggregator.AggregateWeekly(activities, range, MaximumHeartRate);

        Assert.Equal(4, weekly.Count);
        Assert.Equal([10, 11, 12, 13], weekly.Select(w => w.Week.Week));
        Assert.Equal(30m, weekly[0].Points);
        Assert.Equal(40m, weekly[1].Points);
        Assert.Equal(0m, weekly[2].Points);
        Assert.Equal(50m, weekly[3].Points);
    }

    // User Story 2, scenario 5 (FR-013, C9, C10) - the decisive behaviour of this feature.
    // The weekly series covers whole ISO weeks and therefore reaches OUTSIDE the requested
    // range; the daily series never does. Both halves are asserted together because the
    // asymmetry is the point.
    [Fact]
    public void A_weekly_total_covers_its_whole_week_including_days_outside_the_range()
    {
        var mondayActivity = EstimatedActivity("A-1", new DateOnly(2026, 3, 2), movingMinutes: 30);
        var range = new DateRange(new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 5));

        var weekly = TrainingLoadAggregator.AggregateWeekly(
            [mondayActivity], range, MaximumHeartRate);
        var daily = TrainingLoadAggregator.AggregateDaily(
            [mondayActivity], range, MaximumHeartRate);

        var week = Assert.Single(weekly);
        Assert.Equal(new DateOnly(2026, 3, 2), week.Week.Monday);
        Assert.Equal(new DateOnly(2026, 3, 8), week.Week.Sunday);
        Assert.Equal(60m, week.Points);

        Assert.Equal(
            [new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 5)],
            daily.Select(d => d.Day));
        Assert.All(daily, d => Assert.Equal(0m, d.Points));
    }

    // User Story 2, scenario 4 (FR-017, C10, SC-003): over a range covering whole ISO weeks, the
    // two views agree exactly, week by week.
    [Fact]
    public void Each_week_equals_the_sum_of_its_seven_days()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", new DateOnly(2026, 3, 3), movingMinutes: 15),
            EstimatedActivity("A-2", new DateOnly(2026, 3, 10), movingMinutes: 20),
            EstimatedActivity("A-3", new DateOnly(2026, 3, 24), movingMinutes: 25),
        };
        var range = new DateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 29));

        var weekly = TrainingLoadAggregator.AggregateWeekly(activities, range, MaximumHeartRate);
        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        foreach (var week in weekly)
        {
            var itsDays = daily.Where(d => d.Day >= week.Week.Monday && d.Day <= week.Week.Sunday);
            Assert.Equal(itsDays.Sum(d => d.Points), week.Points);
        }
    }

    // The other half of C10: where a week is extended past the range, the difference between the
    // weekly total and the in-range daily totals is exactly the load of the extended days, and
    // nothing else.
    [Fact]
    public void An_extended_weeks_surplus_is_exactly_the_load_of_the_days_outside_the_range()
    {
        var mondayActivity = EstimatedActivity("A-1", new DateOnly(2026, 3, 2), movingMinutes: 30);
        var wednesdayActivity = EstimatedActivity("A-2", new DateOnly(2026, 3, 4), movingMinutes: 15);
        TrainingActivity[] activities = [mondayActivity, wednesdayActivity];
        var range = new DateRange(new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 5));

        var weekly = TrainingLoadAggregator.AggregateWeekly(activities, range, MaximumHeartRate);
        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        var surplus = Assert.Single(weekly).Points - daily.Sum(d => d.Points);
        Assert.Equal(mondayActivity.CalculateTrainingLoad(MaximumHeartRate).Points, surplus);
    }

    // C9, SC-012: no weekly entry is ever partial, for any range.
    [Theory]
    [InlineData("2026-03-04", "2026-03-05")]   // two days, mid-week
    [InlineData("2026-03-04", "2026-04-10")]   // starts and ends mid-week
    [InlineData("2026-01-01", "2026-06-30")]   // several months
    [InlineData("2026-12-30", "2027-01-05")]   // across the 53-week boundary
    public void Every_weekly_entry_covers_seven_days_monday_to_sunday(string from, string to)
    {
        var range = new DateRange(DateOnly.Parse(from), DateOnly.Parse(to));

        var weekly = TrainingLoadAggregator.AggregateWeekly([], range, MaximumHeartRate);

        Assert.NotEmpty(weekly);
        Assert.All(weekly, w =>
        {
            Assert.Equal(DayOfWeek.Monday, w.Week.Monday.DayOfWeek);
            Assert.Equal(w.Week.Monday.AddDays(6), w.Week.Sunday);
        });
    }

    // Spec Edge Cases: a range inside one ISO week still yields that whole week.
    [Fact]
    public void A_range_inside_one_iso_week_yields_exactly_that_whole_week()
    {
        var range = new DateRange(new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 5));

        var week = Assert.Single(
            TrainingLoadAggregator.AggregateWeekly([], range, MaximumHeartRate));

        Assert.Equal(new DateOnly(2026, 3, 2), week.Week.Monday);
        Assert.Equal(new DateOnly(2026, 3, 8), week.Week.Sunday);
    }

    // User Story 2, scenario 6: across the year boundary the weeks stay consecutive, and an
    // activity on 2027-01-03 belongs to 2026-W53.
    [Fact]
    public void Weeks_stay_consecutive_across_the_iso_year_boundary()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", new DateOnly(2027, 1, 3), movingMinutes: 15),
        };
        var range = new DateRange(new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 17));

        var weekly = TrainingLoadAggregator.AggregateWeekly(activities, range, MaximumHeartRate);

        Assert.Equal(
            [(2026, 53), (2027, 1), (2027, 2)],
            weekly.Select(w => (w.Week.Year, w.Week.Week)));
        Assert.Equal(30m, weekly[0].Points);
        Assert.Equal(0m, weekly[1].Points);
    }
}
