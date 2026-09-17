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

    // FR-022: seeding mid-range would produce figures that look genuine and are not, so a history
    // that does not reach back to the range's first day is refused.
    [Fact]
    public void A_history_that_starts_after_the_range_is_refused()
    {
        DailyTrainingLoad[] history =
        [
            TrainingDay(new DateOnly(2026, 3, 5)),
            TrainingDay(new DateOnly(2026, 3, 6)),
        ];
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 6));

        var refusal = Assert.Throws<ArgumentException>(
            () => TrainingMetricsCalculator.Calculate(history, range));

        Assert.Equal("history", refusal.ParamName);
        Assert.Contains("2026-03-05", refusal.Message);
        Assert.Contains("2026-03-01", refusal.Message);
    }

    // FR-022 again: an empty history trivially fails to reach back to the range's first day, so
    // this is that rule and not a separate one. The message must say the history is empty rather
    // than report a date that does not exist (research R13).
    [Fact]
    public void An_empty_history_is_refused_and_named_as_empty()
    {
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 6));

        var refusal = Assert.Throws<ArgumentException>(
            () => TrainingMetricsCalculator.Calculate([], range));

        Assert.Equal("history", refusal.ParamName);
        Assert.Contains("empty", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    // FR-022a: returning a short series would break FR-008, and padding the missing days with rest
    // would invent training history the athlete never supplied.
    [Fact]
    public void A_history_that_ends_before_the_range_is_refused()
    {
        List<DailyTrainingLoad> history =
        [
            .. Enumerable.Range(0, 20).Select(i => TrainingDay(new DateOnly(2026, 3, 1).AddDays(i))),
        ];
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var refusal = Assert.Throws<ArgumentException>(
            () => TrainingMetricsCalculator.Calculate(history, range));

        Assert.Equal("history", refusal.ParamName);
        Assert.Contains("2026-03-20", refusal.Message);
        Assert.Contains("2026-03-31", refusal.Message);
    }

    // FR-023: a gap, a duplicate and an inversion are one violation seen from three sides. Each is
    // refused rather than repaired - this feature cannot tell a genuinely absent day from an
    // aggregation defect, and a silently skipped day changes every figure after it.
    public static TheoryData<string, DateOnly[]> MalformedHistories() => new()
    {
        {
            "a missing day",
            [new(2026, 3, 1), new(2026, 3, 2), new(2026, 3, 4), new(2026, 3, 5)]
        },
        {
            "a repeated day",
            [new(2026, 3, 1), new(2026, 3, 2), new(2026, 3, 2), new(2026, 3, 3)]
        },
        {
            "days out of order",
            [new(2026, 3, 1), new(2026, 3, 2), new(2026, 3, 4), new(2026, 3, 3)]
        },
    };

    [Theory]
    [MemberData(nameof(MalformedHistories))]
    public void A_history_that_is_not_continuous_is_refused(string shape, DateOnly[] days)
    {
        var history = days.Select(TrainingDay).ToList();
        var range = new DateRange(days[0], new DateOnly(2026, 3, 3));

        var refusal = Assert.Throws<ArgumentException>(
            () => TrainingMetricsCalculator.Calculate(history, range));

        Assert.Equal("history", refusal.ParamName);
        Assert.Contains("2026-03-0", refusal.Message);
        Assert.False(string.IsNullOrWhiteSpace(shape));
    }

    // Feature 001's contract guarantee C3, applied to the five refusals that share the parameter
    // name "history" (FR-026, C28). Without distinct messages, a test for one would pass against
    // another and the refusals would be indistinguishable to a caller.
    [Fact]
    public void The_five_history_refusals_are_told_apart_by_their_messages()
    {
        var range = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3));

        DailyTrainingLoad[] startsLate =
        [
            TrainingDay(new DateOnly(2026, 3, 2)),
            TrainingDay(new DateOnly(2026, 3, 3)),
        ];
        DailyTrainingLoad[] endsEarly = [TrainingDay(new DateOnly(2026, 3, 1))];
        DailyTrainingLoad[] hasGap =
        [
            TrainingDay(new DateOnly(2026, 3, 1)),
            TrainingDay(new DateOnly(2026, 3, 3)),
        ];

        string[] messages =
        [
            Assert.Throws<ArgumentNullException>(
                () => TrainingMetricsCalculator.Calculate(null!, range)).Message,
            Assert.Throws<ArgumentException>(
                () => TrainingMetricsCalculator.Calculate([], range)).Message,
            Assert.Throws<ArgumentException>(
                () => TrainingMetricsCalculator.Calculate(startsLate, range)).Message,
            Assert.Throws<ArgumentException>(
                () => TrainingMetricsCalculator.Calculate(endsEarly, range)).Message,
            Assert.Throws<ArgumentException>(
                () => TrainingMetricsCalculator.Calculate(hasGap, range)).Message,
        ];

        Assert.Equal(messages.Length, messages.Distinct().Count());
    }

    // FR-025, C29: only a malformed history is refused. A history of nothing but rest days is
    // perfectly valid input and returns a full series decaying toward zero.
    [Fact]
    public void A_history_of_nothing_but_rest_days_is_not_a_refusal()
    {
        var firstDay = new DateOnly(2026, 3, 1);
        List<DailyTrainingLoad> allZero =
        [
            .. Enumerable.Range(0, 60).Select(i => new DailyTrainingLoad(
                firstDay.AddDays(i), 0m, 0, LoadBasis.None)),
        ];

        var series = TrainingMetricsCalculator.Calculate(
            allZero,
            new DateRange(firstDay, firstDay.AddDays(59)));

        Assert.Equal(60, series.Count);

        // Exact for the same reason as in MetricsSeriesTests: a history of pure rest never moves
        // either accumulator off zero.
        Assert.All(series, day => Assert.Equal(0.0, day.Fitness));
        Assert.All(series, day => Assert.Equal(0.0, day.Fatigue));
    }
}
