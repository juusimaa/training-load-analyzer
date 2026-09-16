namespace TrainingLoadAnalyzer.Domain.Tests;

public class EstimatedTrainingLoadTests
{
    private static readonly DateTimeOffset AnyStart =
        new(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2));

    // User Story 2, scenario 9 (FR-013): 40 minutes x the default intensity weight of 2.
    [Fact]
    public void A_session_without_heart_rate_data_reports_an_estimated_load_from_moving_time()
    {
        var activity = new TrainingActivity(
            "A-1",
            AnyStart,
            TimeSpan.FromMinutes(40),
            ActivityType.Running);

        var load = activity.CalculateTrainingLoad(maximumHeartRate: 190);

        Assert.Equal(80m, load.Points);
        Assert.Equal(LoadProvenance.Estimated, load.Provenance);
    }

    // User Story 2, scenario 10 (FR-014, SC-007). The two numbers collide deliberately: a test
    // asserting only on Points would pass here without testing FR-014 at all.
    [Fact]
    public void A_measured_and_an_estimated_load_of_the_same_value_stay_distinguishable()
    {
        var measured = new TrainingActivity(
            "A-1",
            AnyStart,
            TimeSpan.FromMinutes(45),
            ActivityType.Running,
            new HeartRateSeries(new[]
            {
                new HeartRateSample(TimeSpan.FromMinutes(0), 150),
                new HeartRateSample(TimeSpan.FromMinutes(10), 175),
                new HeartRateSample(TimeSpan.FromMinutes(20), 160),
            })).CalculateTrainingLoad(maximumHeartRate: 190);

        var estimated = new TrainingActivity(
            "A-2",
            AnyStart,
            TimeSpan.FromMinutes(40),
            ActivityType.Running).CalculateTrainingLoad(maximumHeartRate: 190);

        Assert.Equal(measured.Points, estimated.Points);
        Assert.NotEqual(measured.Provenance, estimated.Provenance);
        Assert.NotEqual(measured, estimated);
    }
}
