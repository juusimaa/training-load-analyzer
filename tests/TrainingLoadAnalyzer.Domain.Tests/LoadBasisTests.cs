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

    /// <summary>
    ///   A real session, measured, that scores exactly zero: 80 bpm is 42% of 190, below every
    ///   zone. It is what a rest day must stay distinguishable from.
    /// </summary>
    private static TrainingActivity ZeroScoringMeasuredActivity(string externalId, DateOnly day) =>
        new(
            externalId,
            At(day),
            TimeSpan.FromMinutes(30),
            ActivityType.Running,
            new HeartRateSeries(
            [
                new HeartRateSample(TimeSpan.Zero, 80),
                new HeartRateSample(TimeSpan.FromMinutes(30), 80),
            ]));

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

    // User Story 3, scenario 2 (FR-014).
    [Fact]
    public void A_day_whose_activities_are_all_estimated_reports_an_estimated_total()
    {
        var day = new DateOnly(2026, 3, 2);
        TrainingActivity[] activities =
        [
            EstimatedActivity("A-1", day, movingMinutes: 15),
            EstimatedActivity("A-2", day, movingMinutes: 20),
        ];

        var daily = TrainingLoadAggregator.AggregateDaily(activities, OneDay(day), MaximumHeartRate);

        var total = Assert.Single(daily);
        Assert.Equal(70m, total.Points);
        Assert.Equal(LoadBasis.Estimated, total.Basis);
    }

    // User Story 3, scenario 3 (FR-014, FR-015): a day mixing the two is neither Measured nor
    // Estimated - a total built partly from guesses is weaker than either.
    [Fact]
    public void A_day_mixing_measured_and_estimated_activities_reports_a_mixed_total()
    {
        var day = new DateOnly(2026, 3, 2);
        TrainingActivity[] activities =
        [
            MeasuredActivity("A-1", day),
            EstimatedActivity("A-2", day, movingMinutes: 15),
        ];

        var daily = TrainingLoadAggregator.AggregateDaily(activities, OneDay(day), MaximumHeartRate);

        var total = Assert.Single(daily);
        Assert.Equal(110m, total.Points);
        Assert.Equal(LoadBasis.Mixed, total.Basis);
    }

    // User Story 3, scenario 4 (FR-014): a rest day has no basis at all. An empty set is
    // vacuously "all measured", so a derivation written as "any estimated ? Mixed : Measured"
    // passes every test above and fails here.
    [Fact]
    public void A_day_with_no_activities_reports_no_basis_rather_than_measured()
    {
        var daily = TrainingLoadAggregator.AggregateDaily(
            [], OneDay(new DateOnly(2026, 3, 2)), MaximumHeartRate);

        var total = Assert.Single(daily);
        Assert.Equal(0m, total.Points);
        Assert.Equal(LoadBasis.None, total.Basis);
    }

    // FR-015: the proportion is not recorded. One estimate among four measurements is enough to
    // qualify the whole total.
    [Fact]
    public void One_estimated_activity_among_four_measured_ones_makes_the_day_mixed()
    {
        var day = new DateOnly(2026, 3, 2);
        TrainingActivity[] activities =
        [
            MeasuredActivity("A-1", day),
            MeasuredActivity("A-2", day),
            MeasuredActivity("A-3", day),
            MeasuredActivity("A-4", day),
            EstimatedActivity("A-5", day, movingMinutes: 5),
        ];

        var daily = TrainingLoadAggregator.AggregateDaily(activities, OneDay(day), MaximumHeartRate);

        Assert.Equal(LoadBasis.Mixed, Assert.Single(daily).Basis);
    }

    // User Story 3, scenario 6 (FR-016, C12): a rest day and a day of real but effortless
    // training both total zero points. Only the count and the basis tell them apart.
    [Fact]
    public void A_rest_day_and_a_day_of_zero_scoring_training_are_distinguishable()
    {
        var day = new DateOnly(2026, 3, 2);

        var restDay = Assert.Single(
            TrainingLoadAggregator.AggregateDaily([], OneDay(day), MaximumHeartRate));
        var trainedDay = Assert.Single(TrainingLoadAggregator.AggregateDaily(
            [ZeroScoringMeasuredActivity("A-1", day)], OneDay(day), MaximumHeartRate));

        Assert.Equal(0m, restDay.Points);
        Assert.Equal(0, restDay.ActivityCount);
        Assert.Equal(LoadBasis.None, restDay.Basis);

        Assert.Equal(0m, trainedDay.Points);
        Assert.Equal(1, trainedDay.ActivityCount);
        Assert.Equal(LoadBasis.Measured, trainedDay.Basis);
    }

    // Contract C12, both directions: no counted day may report None, and no empty day may report
    // anything else. Asserting only one direction would let a counted day marked None through.
    [Fact]
    public void A_total_has_no_basis_exactly_when_no_activity_contributed()
    {
        TrainingActivity[] activities =
        [
            MeasuredActivity("A-1", new DateOnly(2026, 3, 2)),
            EstimatedActivity("A-2", new DateOnly(2026, 3, 5), movingMinutes: 15),
            MeasuredActivity("A-3", new DateOnly(2026, 3, 9)),
            EstimatedActivity("A-4", new DateOnly(2026, 3, 9), movingMinutes: 20),
            ZeroScoringMeasuredActivity("A-5", new DateOnly(2026, 3, 11)),
        ];
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 12));

        var daily = TrainingLoadAggregator.AggregateDaily(activities, range, MaximumHeartRate);

        Assert.Equal(12, daily.Count);
        Assert.All(daily, d => Assert.Equal(d.ActivityCount == 0, d.Basis == LoadBasis.None));
    }

    // FR-014, FR-016 for the weekly view: a week carries the same two markings on the same rules.
    [Fact]
    public void A_week_reports_its_basis_and_its_session_count()
    {
        TrainingActivity[] activities =
        [
            MeasuredActivity("A-1", new DateOnly(2026, 3, 3)),
            MeasuredActivity("A-2", new DateOnly(2026, 3, 5)),
        ];
        var range = new DateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 15));

        var weekly = TrainingLoadAggregator.AggregateWeekly(activities, range, MaximumHeartRate);

        Assert.Equal(160m, weekly[0].Points);
        Assert.Equal(2, weekly[0].ActivityCount);
        Assert.Equal(LoadBasis.Measured, weekly[0].Basis);

        Assert.Equal(0m, weekly[1].Points);
        Assert.Equal(0, weekly[1].ActivityCount);
        Assert.Equal(LoadBasis.None, weekly[1].Basis);
    }

    // User Story 3, scenario 5 (FR-015): one estimate anywhere in the week qualifies the whole
    // week. Folding seven day-bases where five are None must not swallow it.
    [Fact]
    public void A_week_with_one_estimated_activity_among_measured_ones_is_mixed()
    {
        TrainingActivity[] activities =
        [
            MeasuredActivity("A-1", new DateOnly(2026, 3, 2)),
            MeasuredActivity("A-2", new DateOnly(2026, 3, 4)),
            MeasuredActivity("A-3", new DateOnly(2026, 3, 6)),
            EstimatedActivity("A-4", new DateOnly(2026, 3, 8), movingMinutes: 15),
        ];
        var range = new DateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 8));

        var week = Assert.Single(
            TrainingLoadAggregator.AggregateWeekly(activities, range, MaximumHeartRate));

        Assert.Equal(4, week.ActivityCount);
        Assert.Equal(LoadBasis.Mixed, week.Basis);
    }
}
