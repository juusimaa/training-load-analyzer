namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   The inputs the trend calculation refuses, and what each refusal says (FR-022 - FR-027).
/// </summary>
public class TrendInputTests
{
    private static readonly DateOnly W10 = new(2026, 3, 2);

    private static IsoWeek Week(int offsetWeeks) => IsoWeek.For(W10.AddDays(7 * offsetWeeks));

    private static WeeklyTrainingLoad Load(int offsetWeeks, decimal points = 500m) =>
        new(Week(offsetWeeks), points, 4, LoadBasis.Measured);

    private static DateRange WholeWeek(int offsetWeeks) =>
        new(Week(offsetWeeks).Monday, Week(offsetWeeks).Sunday);

    // T017 - User Story 1, scenario 5 (FR-022). The range's first week has a predecessor like
    // any other week, and it must be supplied even though it lies outside the range. Producing
    // a first entry with nothing to compare against would mean something different from every
    // other entry in the series.
    [Fact]
    public void A_history_that_does_not_reach_back_before_the_range_is_refused()
    {
        var history = new[] { Load(0) };

        var refusal = Assert.Throws<ArgumentException>(
            () => TrainingLoadTrendCalculator.Calculate(history, WholeWeek(0)));

        Assert.Equal("history", refusal.ParamName);
        Assert.Contains("2026-02-23", refusal.Message);
    }

    // T019 - User Story 1, scenario 6 (FR-022b). This rule came from a gap found while planning
    // (research R11). The tempting implementation zero-fills the missing weeks, and the very
    // next thing this feature would do is report a significant decrease for a week that has no
    // data behind it at all.
    [Fact]
    public void A_history_that_stops_before_the_ranges_last_week_is_refused()
    {
        var history = new[] { Load(0), Load(1) };
        var fourWeeks = new DateRange(Week(1).Monday, Week(4).Sunday);

        var refusal = Assert.Throws<ArgumentException>(
            () => TrainingLoadTrendCalculator.Calculate(history, fourWeeks));

        Assert.Equal("history", refusal.ParamName);
        Assert.Contains("2026-03-30", refusal.Message);
    }

    // T021 - FR-024. A gap is refused rather than repaired: filling it with a zero week would
    // compare a week against the wrong predecessor and manufacture a significant decrease out
    // of missing data.
    [Fact]
    public void A_history_with_a_missing_week_is_refused()
    {
        var history = new[] { Load(0), Load(1), Load(3) };

        var refusal = Assert.Throws<ArgumentException>(
            () => TrainingLoadTrendCalculator.Calculate(history, WholeWeek(3)));

        Assert.Equal("history", refusal.ParamName);
        Assert.Contains("2026-03-23", refusal.Message);
    }

    // T023 - a repeated week and an inverted pair are the same violation seen from other sides.
    [Fact]
    public void A_repeated_or_out_of_order_week_is_refused_on_the_same_terms()
    {
        var repeated = new[] { Load(0), Load(1), Load(1) };
        var inverted = new[] { Load(0), Load(2), Load(1) };

        var first = Assert.Throws<ArgumentException>(
            () => TrainingLoadTrendCalculator.Calculate(repeated, WholeWeek(1)));
        var second = Assert.Throws<ArgumentException>(
            () => TrainingLoadTrendCalculator.Calculate(inverted, WholeWeek(1)));

        Assert.Contains("not continuous", first.Message);
        Assert.Contains("not continuous", second.Message);
    }

    // T023 - FR-027, C41. Four refusals share the ParamName "history", so a test asserting only
    // on ParamName would pass against the wrong one. The messages are what tell them apart.
    [Fact]
    public void Every_refusal_names_its_own_rule_distinctly()
    {
        var messages = new[]
        {
            Refusal([], WholeWeek(1)),
            Refusal([Load(1)], WholeWeek(1)),
            Refusal([Load(0), Load(1)], new DateRange(Week(1).Monday, Week(4).Sunday)),
            Refusal([Load(0), Load(1), Load(3)], WholeWeek(3)),
        };

        Assert.All(messages, m => Assert.False(string.IsNullOrWhiteSpace(m)));
        Assert.Equal(4, messages.Distinct().Count());

        static string Refusal(WeeklyTrainingLoad[] history, DateRange range) =>
            Assert.Throws<ArgumentException>(
                () => TrainingLoadTrendCalculator.Calculate(history, range)).Message;
    }

    // T024 - FR-026: an inverted range is refused by DateRange's own constructor, before this
    // feature is reached. A second check here would be a second place for the message to drift.
    [Fact]
    public void An_inverted_range_is_refused_by_the_range_itself()
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => new DateRange(new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 9)));

        Assert.Equal("end", refusal.ParamName);
    }

    // T024 - C42: a refusal returns nothing at all, never a partial series beside the exception.
    [Fact]
    public void A_refusal_carries_no_partial_series_with_it()
    {
        IReadOnlyList<WeeklyLoadTrend>? escaped = null;

        Assert.Throws<ArgumentException>(() =>
            escaped = TrainingLoadTrendCalculator.Calculate(
                [Load(0), Load(1)], new DateRange(Week(1).Monday, Week(4).Sunday)));

        Assert.Null(escaped);
    }

    // T024 - the null guards, on 001's convention (C3).
    [Fact]
    public void A_missing_history_or_range_is_refused()
    {
        Assert.Throws<ArgumentNullException>(
            () => TrainingLoadTrendCalculator.Calculate(null!, WholeWeek(1)));
        Assert.Throws<ArgumentNullException>(
            () => TrainingLoadTrendCalculator.Calculate([Load(0), Load(1)], null!));
    }
}
