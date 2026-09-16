namespace TrainingLoadAnalyzer.Domain.Tests;

public class MeasuredTrainingLoadTests
{
    private static readonly DateTimeOffset AnyStart =
        new(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2));

    private static TrainingActivity ActivityWith(
        HeartRateSeries heartRate,
        ActivityType type = ActivityType.Running) =>
        new("A-1", AnyStart, TimeSpan.FromMinutes(45), type, heartRate);

    // User Story 2, scenarios 1 and 2 (FR-008, FR-009, FR-014).
    [Fact]
    public void A_session_with_heart_rate_data_reports_a_measured_load_of_eighty_points()
    {
        var activity = ActivityWith(new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),
            new HeartRateSample(TimeSpan.FromMinutes(10), 175),
            new HeartRateSample(TimeSpan.FromMinutes(20), 160),
        }));

        var load = activity.CalculateTrainingLoad(maximumHeartRate: 190);

        Assert.Equal(80m, load.Points);
        Assert.Equal(LoadProvenance.Measured, load.Provenance);
    }

    // User Story 2, scenario 3 (SC-004): same intensity, twice the duration, strictly greater load.
    [Fact]
    public void The_longer_of_two_equally_intense_sessions_has_the_strictly_greater_load()
    {
        var shorter = ActivityWith(new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),
            new HeartRateSample(TimeSpan.FromMinutes(30), 150),
        }));

        var longer = ActivityWith(new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),
            new HeartRateSample(TimeSpan.FromMinutes(60), 150),
        }));

        Assert.True(longer.CalculateTrainingLoad(190).Points > shorter.CalculateTrainingLoad(190).Points);
    }

    // User Story 2, scenario 4: zone 5 is worth exactly five times zone 1 for the same duration.
    [Fact]
    public void A_zone_five_session_scores_five_times_a_zone_one_session_of_equal_duration()
    {
        var zoneOne = ActivityWith(new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 100),   // 50% of 200
            new HeartRateSample(TimeSpan.FromMinutes(30), 100),
        }));

        var zoneFive = ActivityWith(new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 180),   // 90% of 200
            new HeartRateSample(TimeSpan.FromMinutes(30), 180),
        }));

        Assert.Equal(30m, zoneOne.CalculateTrainingLoad(200).Points);
        Assert.Equal(150m, zoneFive.CalculateTrainingLoad(200).Points);
        Assert.Equal(zoneOne.CalculateTrainingLoad(200).Points * 5, zoneFive.CalculateTrainingLoad(200).Points);
    }

    // SC-005's second clause, and the surprising half: Edwards TRIMP is a step function, so
    // two intensities inside one band produce equal loads. "Greater OR EQUAL" is deliberate.
    [Fact]
    public void Two_intensities_inside_the_same_zone_produce_equal_loads()
    {
        var lower = ActivityWith(new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 141),   // 70.5% of 200 — zone 3
            new HeartRateSample(TimeSpan.FromMinutes(30), 141),
        }));

        var higher = ActivityWith(new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 159),   // 79.5% of 200 — still zone 3
            new HeartRateSample(TimeSpan.FromMinutes(30), 159),
        }));

        Assert.Equal(lower.CalculateTrainingLoad(200), higher.CalculateTrainingLoad(200));
    }

    // User Story 2, scenario 5: entirely below 50% is a measured zero, not an error and not an estimate.
    [Fact]
    public void A_session_held_below_fifty_percent_of_maximum_is_a_measured_zero()
    {
        var activity = ActivityWith(new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 90),    // 45% of 200
            new HeartRateSample(TimeSpan.FromMinutes(30), 95),
        }));

        var load = activity.CalculateTrainingLoad(maximumHeartRate: 200);

        Assert.Equal(0m, load.Points);
        Assert.Equal(LoadProvenance.Measured, load.Provenance);
    }

    // User Story 2, scenario 7 (FR-011, SC-003, contract C4): the calculation is pure.
    [Fact]
    public void Repeated_computation_from_the_same_inputs_yields_the_same_value()
    {
        var activity = ActivityWith(new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),
            new HeartRateSample(TimeSpan.FromMinutes(10), 175),
            new HeartRateSample(TimeSpan.FromMinutes(20), 160),
        }));

        var first = activity.CalculateTrainingLoad(190);

        for (var repetition = 0; repetition < 100; repetition++)
        {
            Assert.Equal(first, activity.CalculateTrainingLoad(190));
        }
    }

    // User Story 2, scenario 8 (FR-012, SC-009, contract C6): ActivityType never reaches the calculation.
    [Fact]
    public void A_run_and_a_ride_with_identical_series_produce_identical_loads()
    {
        HeartRateSample[] samples =
        [
            new(TimeSpan.FromMinutes(0), 150),
            new(TimeSpan.FromMinutes(10), 175),
            new(TimeSpan.FromMinutes(20), 160),
        ];

        var run = ActivityWith(new HeartRateSeries(samples), ActivityType.Running);
        var ride = ActivityWith(new HeartRateSeries(samples), ActivityType.Cycling);

        Assert.True(run.CalculateTrainingLoad(190) == ride.CalculateTrainingLoad(190));
    }

    // User Story 3, scenario 9 (FR-016): refused only for an activity that actually reads it.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_measured_load_is_refused_when_the_maximum_heart_rate_is_not_positive(int maximumHeartRate)
    {
        var activity = ActivityWith(new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),
            new HeartRateSample(TimeSpan.FromMinutes(10), 175),
        }));

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => activity.CalculateTrainingLoad(maximumHeartRate));

        Assert.Equal("maximumHeartRate", refusal.ParamName);
    }
}
