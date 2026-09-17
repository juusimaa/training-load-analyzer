namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   User Story 3 — know how much to trust a total.
/// </summary>
public class LoadBasisTests
{
    private const int MaximumHeartRate = 190;

    private static DateTimeOffset At(DateOnly day) =>
        new(day.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(2));

    /// <summary>
    ///   Feature 001's worked example: 10 minutes in zone 3 plus 10 in zone 5 is exactly 80
    ///   points, marked Measured.
    /// </summary>
    private static TrainingActivity MeasuredActivity(string externalId, DateOnly day) =>
        new(
            externalId,
            At(day),
            TimeSpan.FromMinutes(20),
            ActivityType.Running,
            new HeartRateSeries(
            [
                new HeartRateSample(TimeSpan.Zero, 150),
                new HeartRateSample(TimeSpan.FromMinutes(10), 175),
                new HeartRateSample(TimeSpan.FromMinutes(20), 160),
            ]));

    /// <summary>No heart-rate data: moving minutes x 2, marked Estimated.</summary>
    private static TrainingActivity EstimatedActivity(string externalId, DateOnly day, int movingMinutes) =>
        new(externalId, At(day), TimeSpan.FromMinutes(movingMinutes), ActivityType.Running);

    private static DateRange OneDay(DateOnly day) => new(day, day);

    // User Story 3, scenario 1 (FR-014): a day built only from heart-rate-measured sessions
    // reports its total as measured.
    [Fact]
    public void A_day_whose_activities_are_all_measured_reports_a_measured_total()
    {
        var day = new DateOnly(2026, 3, 2);
        TrainingActivity[] activities = [MeasuredActivity("A-1", day), MeasuredActivity("A-2", day)];

        var daily = TrainingLoadAggregator.AggregateDaily(activities, OneDay(day), MaximumHeartRate);

        var total = Assert.Single(daily);
        Assert.Equal(160m, total.Points);
        Assert.Equal(LoadBasis.Measured, total.Basis);
    }
}
