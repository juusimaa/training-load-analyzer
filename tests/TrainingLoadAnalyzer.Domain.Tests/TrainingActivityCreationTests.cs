namespace TrainingLoadAnalyzer.Domain.Tests;

public class TrainingActivityCreationTests
{
    // User Story 1, scenario 1 (FR-001, FR-002, FR-003, FR-004, FR-005)
    [Fact]
    public void A_recorded_run_reads_back_every_detail_unchanged()
    {
        var startedAt = new DateTimeOffset(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2));

        var activity = new TrainingActivity(
            "A-1",
            startedAt,
            TimeSpan.FromMinutes(45),
            ActivityType.Running);

        Assert.Equal("A-1", activity.ExternalId);
        Assert.Equal(startedAt, activity.StartedAt);
        Assert.Equal(TimeSpan.FromMinutes(45), activity.MovingTime);
        Assert.Equal(ActivityType.Running, activity.Type);
    }

    // User Story 1, scenario 2 (FR-005)
    [Fact]
    public void A_recorded_ride_classifies_as_cycling_and_is_distinct_from_the_run()
    {
        var startedAt = new DateTimeOffset(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2));

        var run = new TrainingActivity("A-1", startedAt, TimeSpan.FromMinutes(45), ActivityType.Running);
        var ride = new TrainingActivity("A-2", startedAt, TimeSpan.FromMinutes(45), ActivityType.Cycling);

        Assert.Equal(ActivityType.Cycling, ride.Type);
        Assert.Equal("A-2", ride.ExternalId);
        Assert.NotSame(run, ride);
        Assert.NotEqual(run.ExternalId, ride.ExternalId);
    }

    // User Story 1, scenario 3 (FR-003)
    [Fact]
    public void Both_the_utc_instant_and_the_athletes_local_start_time_are_recoverable()
    {
        var startedAt = new DateTimeOffset(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2));

        var activity = new TrainingActivity("A-1", startedAt, TimeSpan.FromMinutes(45), ActivityType.Running);

        Assert.Equal(new DateTime(2026, 3, 1, 5, 30, 0, DateTimeKind.Utc), activity.StartedAt.UtcDateTime);
        Assert.Equal(new DateTime(2026, 3, 1, 7, 30, 0), activity.StartedAt.DateTime);
        Assert.Equal(TimeSpan.FromHours(2), activity.StartedAt.Offset);
    }

    // SC-008: the repeated hour of a daylight-saving transition. DateTimeOffset carries an
    // explicit offset rather than a timezone, so the ambiguity never arises in the first place.
    [Fact]
    public void The_same_wall_clock_time_at_two_offsets_stays_two_instants_on_one_local_day()
    {
        var beforeTransition = new TrainingActivity(
            "A-1",
            new DateTimeOffset(2026, 10, 25, 3, 30, 0, TimeSpan.FromHours(3)),
            TimeSpan.FromMinutes(45),
            ActivityType.Running);

        var afterTransition = new TrainingActivity(
            "A-2",
            new DateTimeOffset(2026, 10, 25, 3, 30, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(45),
            ActivityType.Running);

        Assert.NotEqual(beforeTransition.StartedAt.UtcDateTime, afterTransition.StartedAt.UtcDateTime);
        Assert.Equal(TimeSpan.FromHours(1), afterTransition.StartedAt.UtcDateTime - beforeTransition.StartedAt.UtcDateTime);

        Assert.Equal(new DateTime(2026, 10, 25), beforeTransition.StartedAt.Date);
        Assert.Equal(new DateTime(2026, 10, 25), afterTransition.StartedAt.Date);

        Assert.Equal(new TimeSpan(3, 30, 0), beforeTransition.StartedAt.TimeOfDay);
        Assert.Equal(new TimeSpan(3, 30, 0), afterTransition.StartedAt.TimeOfDay);
    }

    // FR-002: the identifier is opaque. A .Trim() added for tidiness would pass every other
    // test in this feature; this is the only one that catches it.
    [Fact]
    public void An_external_identifier_round_trips_byte_for_byte_including_whitespace()
    {
        var activity = new TrainingActivity(
            "  A-1  ",
            new DateTimeOffset(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(45),
            ActivityType.Running);

        Assert.Equal("  A-1  ", activity.ExternalId);
    }

    // Spec Assumptions: guarding against upstream clock skew is not this feature's job.
    // This test exists so no later change quietly adds a rule the specification does not state.
    [Fact]
    public void A_start_time_in_the_future_is_accepted()
    {
        var future = DateTimeOffset.UtcNow.AddDays(1);

        var activity = new TrainingActivity("A-1", future, TimeSpan.FromMinutes(45), ActivityType.Running);

        Assert.Equal(future, activity.StartedAt);
    }

    // FR-024: immutability is a guarded property, not just an instruction in the constructor.
    [Fact]
    public void A_training_activity_is_sealed_and_exposes_no_public_setter()
    {
        Assert.True(typeof(TrainingActivity).IsSealed);

        var settable = typeof(TrainingActivity)
            .GetProperties()
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(settable);
    }

    // User Story 1, scenario 4 (FR-006): absence is explicit, not a zero or an empty series.
    [Fact]
    public void An_activity_recorded_without_heart_rate_data_has_no_series_at_all()
    {
        var activity = new TrainingActivity(
            "A-1",
            new DateTimeOffset(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(45),
            ActivityType.Running);

        Assert.Null(activity.HeartRate);
    }

    // FR-007: a series attached to an activity round-trips unchanged.
    [Fact]
    public void An_activity_recorded_with_heart_rate_data_returns_that_series_unchanged()
    {
        var samples = new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),
            new HeartRateSample(TimeSpan.FromMinutes(10), 175),
            new HeartRateSample(TimeSpan.FromMinutes(20), 160),
        };
        var series = new HeartRateSeries(samples);

        var activity = new TrainingActivity(
            "A-1",
            new DateTimeOffset(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(45),
            ActivityType.Running,
            series);

        Assert.Same(series, activity.HeartRate);
        Assert.Equal(samples, activity.HeartRate!.Samples);
    }

    // Spec Assumptions: moving time excludes stops while a monitor keeps recording through them,
    // so a series routinely spans more time than the moving time it belongs to. Refusing that
    // would reject ordinary, correct data.
    [Fact]
    public void A_series_extending_beyond_the_activitys_moving_time_is_accepted()
    {
        var series = new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),
            new HeartRateSample(TimeSpan.FromMinutes(90), 160),
        });

        var activity = new TrainingActivity(
            "A-1",
            new DateTimeOffset(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(45),
            ActivityType.Running,
            series);

        Assert.Equal(TimeSpan.FromMinutes(90), activity.HeartRate!.Samples[^1].TimeFromStart);
        Assert.True(activity.HeartRate.Samples[^1].TimeFromStart > activity.MovingTime);
    }
}
