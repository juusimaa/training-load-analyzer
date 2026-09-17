namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   What the metrics calculation refuses, and why each refusal is distinguishable from the
///   others (FR-021 to FR-026).
/// </summary>
public class MetricsInputTests
{
    private static readonly DateOnly FirstDay = new(2026, 3, 1);

    private static DailyTrainingLoad TrainingDay(DateOnly day) => new(day, 100m, 1, LoadBasis.Measured);

    // FR-026: a missing argument is refused rather than treated as empty, which would hide a
    // caller's bug.
    [Fact]
    public void A_null_history_or_range_is_refused()
    {
        var range = new DateRange(FirstDay, FirstDay);
        DailyTrainingLoad[] history = [TrainingDay(FirstDay)];

        var noHistory = Assert.Throws<ArgumentNullException>(
            () => TrainingMetricsCalculator.Calculate(null!, range));
        var noRange = Assert.Throws<ArgumentNullException>(
            () => TrainingMetricsCalculator.Calculate(history, null!));

        Assert.Equal("history", noHistory.ParamName);
        Assert.Equal("range", noRange.ParamName);
    }
}
