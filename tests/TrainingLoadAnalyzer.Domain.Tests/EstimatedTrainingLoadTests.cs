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
}
