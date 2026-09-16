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
}
