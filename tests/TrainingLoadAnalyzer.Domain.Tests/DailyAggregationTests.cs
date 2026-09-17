namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   User Story 1 — see how much training each day carried.
/// </summary>
public class DailyAggregationTests
{
    private const int MaximumHeartRate = 190;

    /// <summary>
    ///   An activity with no heart-rate data has an estimated load of moving minutes x 2
    ///   (feature 001, FR-013). 15, 20 and 25 minutes therefore give 30, 40 and 50 points.
    /// </summary>
    private static TrainingActivity EstimatedActivity(
        string externalId,
        DateTimeOffset startedAt,
        int movingMinutes) =>
        new(externalId, startedAt, TimeSpan.FromMinutes(movingMinutes), ActivityType.Running);

    private static DateTimeOffset At(int year, int month, int day, int hour = 8, int offsetHours = 2) =>
        new(year, month, day, hour, 0, 0, TimeSpan.FromHours(offsetHours));

    // User Story 1, scenario 1 (FR-001, FR-008): three activities on one day are summed.
    // Every activity sits at +02:00 at 08:00 local, so this test deliberately does NOT
    // distinguish a UTC-based day assignment from an offset-local one. T013 does that.
    [Fact]
    public void Several_activities_on_the_same_day_are_summed_into_that_days_total()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", At(2026, 3, 2), movingMinutes: 15),
            EstimatedActivity("A-2", At(2026, 3, 2), movingMinutes: 20),
            EstimatedActivity("A-3", At(2026, 3, 2), movingMinutes: 25),
        };
        var range = new DateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        var day = Assert.Single(daily);
        Assert.Equal(new DateOnly(2026, 3, 2), day.Day);
        Assert.Equal(120m, day.Points);
    }

    // User Story 1, scenario 2 (FR-005, FR-007, FR-010, C8): every day in the range is present,
    // in ascending order, including the four on which nothing was trained.
    [Fact]
    public void Every_day_in_the_range_is_reported_including_days_with_no_training()
    {
        var activities = new[] { EstimatedActivity("A-1", At(2026, 3, 2), movingMinutes: 15) };
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        Assert.Equal(5, daily.Count);
        Assert.Equal(
            [
                new DateOnly(2026, 3, 1),
                new DateOnly(2026, 3, 2),
                new DateOnly(2026, 3, 3),
                new DateOnly(2026, 3, 4),
                new DateOnly(2026, 3, 5),
            ],
            daily.Select(d => d.Day));
        Assert.Equal(30m, daily[1].Points);
        Assert.Equal(0m, daily[0].Points);
        Assert.Equal(0m, daily[2].Points);
        Assert.Equal(0m, daily[3].Points);
        Assert.Equal(0m, daily[4].Points);
    }

    // User Story 1, scenario 3 (FR-009, SC-006): activities outside the range contribute nothing,
    // and no entry outside the range is produced.
    [Fact]
    public void Activities_outside_the_range_contribute_to_no_daily_total()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", At(2026, 2, 28), movingMinutes: 15),
            EstimatedActivity("A-2", At(2026, 3, 6), movingMinutes: 20),
        };
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        Assert.Equal(5, daily.Count);
        Assert.All(daily, d => Assert.Equal(0m, d.Points));
        Assert.DoesNotContain(daily, d => d.Day < range.Start || d.Day > range.End);
    }

    // User Story 1, scenario 4 (FR-009): both endpoint days are included in full.
    [Fact]
    public void Activities_on_the_first_and_last_day_of_the_range_are_both_included()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", At(2026, 3, 1), movingMinutes: 15),
            EstimatedActivity("A-2", At(2026, 3, 5), movingMinutes: 20),
        };
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        Assert.Equal(30m, daily[0].Points);
        Assert.Equal(40m, daily[4].Points);
    }

    // SC-001: a range of one day produces exactly one entry.
    [Fact]
    public void A_range_of_a_single_day_produces_exactly_one_entry()
    {
        var range = new DateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        var daily = TrainingLoadAggregator.AggregateDaily([], range, MaximumHeartRate);

        var day = Assert.Single(daily);
        Assert.Equal(new DateOnly(2026, 3, 2), day.Day);
    }

    // User Story 1, scenario 5 (FR-018, C16, SC-002): no activities at all still yields the full
    // gap-free series of zero days, not an empty list and not a failure.
    [Fact]
    public void An_empty_activity_collection_still_yields_the_full_zero_series()
    {
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));

        var daily = TrainingLoadAggregator.AggregateDaily([], range, MaximumHeartRate);

        Assert.Equal(5, daily.Count);
        Assert.All(daily, d => Assert.Equal(0m, d.Points));
    }

    // FR-004, C11, SC-011: the day is the athlete's LOCAL day at the activity's own recorded
    // offset. This activity's UTC instant is 2026-03-01T22:30:00Z, a different calendar day.
    // A UTC-based implementation puts the load on 03-01 and fails here; see the mutation check
    // recorded against T013.
    [Fact]
    public void An_activity_is_attributed_to_its_own_offset_local_day_not_its_utc_day()
    {
        var startedAt = new DateTimeOffset(2026, 3, 2, 0, 30, 0, TimeSpan.FromHours(2));
        Assert.Equal(new DateTime(2026, 3, 1, 22, 30, 0), startedAt.UtcDateTime);

        var activities = new[]
        {
            new TrainingActivity("A-1", startedAt, TimeSpan.FromMinutes(15), ActivityType.Running),
        };
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2));

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        Assert.Equal(0m, daily[0].Points);
        Assert.Equal(30m, daily[1].Points);
    }

    // Spec Assumptions: an athlete who travels can record two activities on the same local day
    // whose absolute instants are 37 hours apart. Both count against that local day.
    [Fact]
    public void Two_activities_on_the_same_local_day_at_different_offsets_both_count_to_that_day()
    {
        var far = new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.FromHours(14));
        var home = new DateTimeOffset(2026, 3, 2, 20, 0, 0, TimeSpan.FromHours(-11));
        Assert.Equal(TimeSpan.FromHours(37), home.UtcDateTime - far.UtcDateTime);

        var activities = new[]
        {
            new TrainingActivity("A-1", far, TimeSpan.FromMinutes(15), ActivityType.Running),
            new TrainingActivity("A-2", home, TimeSpan.FromMinutes(20), ActivityType.Cycling),
        };
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 4));

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        Assert.Equal(0m, daily[0].Points);
        Assert.Equal(70m, daily[1].Points);
        Assert.Equal(0m, daily[2].Points);
        Assert.Equal(0m, daily[3].Points);
    }

    // Spec Edge Cases: the first and last instant of a local day belong to that day, with nothing
    // spilling into its neighbours.
    [Fact]
    public void Activities_at_the_first_and_last_minute_of_a_local_day_belong_to_that_day()
    {
        var midnight = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.FromHours(2));
        var lastMinute = new DateTimeOffset(2026, 3, 2, 23, 59, 0, TimeSpan.FromHours(2));

        var activities = new[]
        {
            new TrainingActivity("A-1", midnight, TimeSpan.FromMinutes(15), ActivityType.Running),
            new TrainingActivity("A-2", lastMinute, TimeSpan.FromMinutes(20), ActivityType.Running),
        };
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3));

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        Assert.Equal(0m, daily[0].Points);
        Assert.Equal(70m, daily[1].Points);
        Assert.Equal(0m, daily[2].Points);
    }

    // User Story 1, scenario 6 (FR-010, FR-011): the totals do not depend on the order the
    // activities were supplied in, and the entries come back ascending regardless.
    [Fact]
    public void Totals_and_ordering_do_not_depend_on_the_order_activities_were_supplied()
    {
        var a = EstimatedActivity("A-1", At(2026, 3, 2), movingMinutes: 15);
        var b = EstimatedActivity("A-2", At(2026, 3, 4), movingMinutes: 20);
        var c = EstimatedActivity("A-3", At(2026, 3, 2), movingMinutes: 25);
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));

        var inOneOrder = TrainingLoadAggregator.AggregateDaily([a, b, c], range, MaximumHeartRate);
        var inAnother = TrainingLoadAggregator.AggregateDaily([c, a, b], range, MaximumHeartRate);

        Assert.Equal(inOneOrder, inAnother);
        Assert.Equal(inAnother.Select(d => d.Day).Order(), inAnother.Select(d => d.Day));
        Assert.Equal(80m, inAnother[1].Points);
    }

    // User Story 1, scenario 7 (FR-012, C13, SC-005): aggregation is a pure function of its
    // arguments, with no dependence on the current date or on any external service.
    [Fact]
    public void Aggregating_the_same_activities_twice_produces_the_same_totals()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", At(2026, 3, 2), movingMinutes: 15),
            EstimatedActivity("A-2", At(2026, 3, 4), movingMinutes: 20),
        };
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));

        var first = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);
        var second = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        Assert.Equal(first, second);
    }

    // FR-022, C14, SC-004: aggregation introduces no rounding of its own. This is not a re-test
    // of feature 001's arithmetic - it compares the day total against the exact sum of the loads
    // that went into it.
    [Fact]
    public void A_days_total_is_the_exact_sum_of_its_activities_own_load_values()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", At(2026, 3, 2), movingMinutes: 37),
            EstimatedActivity("A-2", At(2026, 3, 2), movingMinutes: 53),
            EstimatedActivity("A-3", At(2026, 3, 2), movingMinutes: 11),
        };
        var range = new DateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        var expected = activities
            .Select(a => a.CalculateTrainingLoad(MaximumHeartRate).Points)
            .Aggregate(0m, (running, points) => running + points);
        Assert.Equal(expected, Assert.Single(daily).Points);
    }

    // FR-023, C15: two activities carrying the same external identifier both contribute.
    // Recognising duplicates belongs to import and storage; this test exists so that no later
    // change quietly adds de-duplication here (Principle VII).
    [Fact]
    public void Two_activities_sharing_an_external_identifier_both_contribute()
    {
        var activities = new[]
        {
            EstimatedActivity("A-1", At(2026, 3, 2), movingMinutes: 15),
            EstimatedActivity("A-1", At(2026, 3, 2), movingMinutes: 15),
        };
        var range = new DateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        Assert.Equal(60m, Assert.Single(daily).Points);
    }

    // FR-021, research R12: an absent collection is refused rather than silently treated as
    // empty, which would hide a caller's bug behind a plausible-looking zero series.
    [Fact]
    public void An_absent_activity_collection_or_range_is_refused()
    {
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));

        var noActivities = Assert.Throws<ArgumentNullException>(
            () => TrainingLoadAggregator.AggregateDaily(null!, range, MaximumHeartRate));
        var noRange = Assert.Throws<ArgumentNullException>(
            () => TrainingLoadAggregator.AggregateDaily([], null!, MaximumHeartRate));

        Assert.Equal("activities", noActivities.ParamName);
        Assert.Equal("range", noRange.ParamName);
    }
}
