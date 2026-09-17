namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   User Story 3 — know when a trend is not worth trusting.
/// </summary>
public class TrendReliabilityTests
{
    private static readonly DateOnly W10 = new(2026, 3, 2);

    private static IsoWeek Week(int offsetWeeks) => IsoWeek.For(W10.AddDays(7 * offsetWeeks));

    private static WeeklyTrainingLoad Load(
        int offsetWeeks, decimal points, LoadBasis basis = LoadBasis.Measured) =>
        new(Week(offsetWeeks), points, basis == LoadBasis.None ? 0 : 4, basis);

    private static DateRange WholeWeek(int offsetWeeks) =>
        new(Week(offsetWeeks).Monday, Week(offsetWeeks).Sunday);

    private static WeeklyLoadTrend Only(
        WeeklyTrainingLoad previous, WeeklyTrainingLoad current, DateRange range) =>
        Assert.Single(TrainingLoadTrendCalculator.Calculate([previous, current], range));

    // T044 - FR-015, FR-016: a week the range does not wholly contain is reported as partial.
    [Fact]
    public void A_week_the_range_cuts_short_is_reported_as_partial()
    {
        var previous = Load(0, 500m);
        var current = Load(1, 200m);

        var whole = Only(previous, current, WholeWeek(1));
        var cut = Only(
            previous, current,
            new DateRange(Week(1).Monday, Week(1).Monday.AddDays(2)));

        Assert.True(whole.IsComplete);
        Assert.False(cut.IsComplete);
    }

    // T046 - FR-016: partiality is not only an end-of-range phenomenon. An implementation
    // checking only range.End passes T044 and fails here.
    [Fact]
    public void A_week_the_range_joins_late_is_partial_too()
    {
        var cut = Only(
            Load(0, 500m), Load(1, 200m),
            new DateRange(Week(1).Monday.AddDays(2), Week(1).Sunday));

        Assert.False(cut.IsComplete);
    }

    // T047 - User Story 3, scenario 3 (FR-017). A three-day week compared against a seven-day
    // one is not a like-for-like comparison, so no judgement is offered. Calling it a
    // significant decrease would report a drop the athlete never took.
    [Fact]
    public void A_partial_week_is_never_labelled_a_significant_decrease()
    {
        var cut = Only(
            Load(0, 500m), Load(1, 200m),
            new DateRange(Week(1).Monday, Week(1).Monday.AddDays(2)));

        Assert.Equal(TrendClassification.Indeterminate, cut.Classification);
    }

    // T049 - FR-018, C37. The changes are facts about the load actually recorded, so they are
    // qualified rather than withheld - the same treatment a warm-up day's figures get in
    // feature 003 (C23).
    [Fact]
    public void A_partial_week_still_reports_both_of_its_changes()
    {
        var cut = Only(
            Load(0, 500m), Load(1, 200m),
            new DateRange(Week(1).Monday, Week(1).Monday.AddDays(2)));

        Assert.Equal(-300m, cut.AbsoluteChange);
        Assert.Equal(-0.6m, cut.RelativeChange);
    }

    // T050 - FR-006, FR-019, C39. Both totals are used exactly as the aggregation produced
    // them, and neither week is scaled to make the comparison even. Extrapolating
    // three days of training into a notional week would invent load the athlete did not do and
    // turn a light Monday into a fictional ramp-up.
    [Fact]
    public void A_partial_week_is_compared_against_the_whole_week_before_it()
    {
        var cut = Only(
            Load(0, 500m), Load(1, 200m),
            new DateRange(Week(1).Monday, Week(1).Monday.AddDays(2)));

        Assert.Equal(500m, cut.PreviousPoints);
        Assert.Equal(200m, cut.Points);
    }

    // T051 - C38: completeness depends on the week and the range, and on nothing else.
    [Fact]
    public void Completeness_does_not_depend_on_the_totals()
    {
        var range = new DateRange(Week(1).Monday, Week(1).Monday.AddDays(2));

        var light = Only(Load(0, 1m), Load(1, 2m), range);
        var heavy = Only(Load(0, 5000m), Load(1, 9m), range);

        Assert.Equal(light.IsComplete, heavy.IsComplete);

        var fourWholeWeeks = TrainingLoadTrendCalculator.Calculate(
            [Load(0, 400m), Load(1, 500m), Load(2, 600m), Load(3, 700m), Load(4, 800m)],
            new DateRange(Week(1).Monday, Week(4).Sunday));

        Assert.All(fourWholeWeeks, t => Assert.True(t.IsComplete));
    }

    // T052 - FR-020: a comparison of two measured weeks rests on measured load.
    [Fact]
    public void A_comparison_of_two_measured_weeks_is_measured()
    {
        var trend = Only(
            Load(0, 500m, LoadBasis.Measured),
            Load(1, 600m, LoadBasis.Measured),
            WholeWeek(1));

        Assert.Equal(LoadBasis.Measured, trend.Basis);
    }

    // T054 - User Story 3, scenario 4 (FR-021): one estimated week among the two makes the
    // whole comparison weaker than a measured one.
    [Fact]
    public void A_measured_week_compared_with_an_estimated_one_is_mixed()
    {
        var trend = Only(
            Load(0, 500m, LoadBasis.Measured),
            Load(1, 600m, LoadBasis.Estimated),
            WholeWeek(1));

        Assert.Equal(LoadBasis.Mixed, trend.Basis);
    }

    // T055 - User Story 3, scenario 5 (FR-021). A week with no training has nothing that could
    // have been measured or estimated, so it contributes nothing. An implementation treating
    // None as a fourth participating state reports Mixed here and quietly downgrades every
    // comparison that follows a rest week.
    [Fact]
    public void An_untrained_week_contributes_nothing_to_the_basis()
    {
        var trend = Only(
            Load(0, 0m, LoadBasis.None),
            Load(1, 600m, LoadBasis.Measured),
            WholeWeek(1));

        Assert.Equal(LoadBasis.Measured, trend.Basis);
    }

    // T057 - C40: the whole combination table, asserted rather than sampled. Sixteen cases is
    // cheaper to state in full than to argue about.
    [Theory]
    [InlineData(LoadBasis.None, LoadBasis.None, LoadBasis.None)]
    [InlineData(LoadBasis.None, LoadBasis.Measured, LoadBasis.Measured)]
    [InlineData(LoadBasis.None, LoadBasis.Estimated, LoadBasis.Estimated)]
    [InlineData(LoadBasis.None, LoadBasis.Mixed, LoadBasis.Mixed)]
    [InlineData(LoadBasis.Measured, LoadBasis.None, LoadBasis.Measured)]
    [InlineData(LoadBasis.Measured, LoadBasis.Measured, LoadBasis.Measured)]
    [InlineData(LoadBasis.Measured, LoadBasis.Estimated, LoadBasis.Mixed)]
    [InlineData(LoadBasis.Measured, LoadBasis.Mixed, LoadBasis.Mixed)]
    [InlineData(LoadBasis.Estimated, LoadBasis.None, LoadBasis.Estimated)]
    [InlineData(LoadBasis.Estimated, LoadBasis.Measured, LoadBasis.Mixed)]
    [InlineData(LoadBasis.Estimated, LoadBasis.Estimated, LoadBasis.Estimated)]
    [InlineData(LoadBasis.Estimated, LoadBasis.Mixed, LoadBasis.Mixed)]
    [InlineData(LoadBasis.Mixed, LoadBasis.None, LoadBasis.Mixed)]
    [InlineData(LoadBasis.Mixed, LoadBasis.Measured, LoadBasis.Mixed)]
    [InlineData(LoadBasis.Mixed, LoadBasis.Estimated, LoadBasis.Mixed)]
    [InlineData(LoadBasis.Mixed, LoadBasis.Mixed, LoadBasis.Mixed)]
    public void The_basis_of_a_comparison_combines_the_two_weeks_behind_it(
        LoadBasis previous, LoadBasis current, LoadBasis expected)
    {
        var forwards = Only(Load(0, 500m, previous), Load(1, 600m, current), WholeWeek(1));

        // The same two weeks the other way round: order must not change the answer.
        var backwards = Only(Load(0, 500m, current), Load(1, 600m, previous), WholeWeek(1));

        Assert.Equal(expected, forwards.Basis);
        Assert.Equal(expected, backwards.Basis);
    }

    // T058 - FR-021 and feature 002's C12 carried through. A rest week and a week of real
    // sessions totalling zero points are not the same thing, and only the basis can tell them
    // apart: the points are identical.
    [Fact]
    public void A_rest_week_and_a_week_of_zero_point_sessions_differ_only_in_their_basis()
    {
        var rested = Only(
            new WeeklyTrainingLoad(Week(0), 0m, 0, LoadBasis.None),
            new WeeklyTrainingLoad(Week(1), 0m, 0, LoadBasis.None),
            WholeWeek(1));

        var scoredZero = Only(
            new WeeklyTrainingLoad(Week(0), 0m, 3, LoadBasis.Measured),
            new WeeklyTrainingLoad(Week(1), 0m, 2, LoadBasis.Measured),
            WholeWeek(1));

        Assert.Equal(LoadBasis.None, rested.Basis);
        Assert.Equal(LoadBasis.Measured, scoredZero.Basis);
        Assert.Equal(rested.AbsoluteChange, scoredZero.AbsoluteChange);
    }

    // T059 - C44, SC-007: an entirely idle history is valid input, not a refusal, and every
    // entry still states how far it can be trusted.
    [Fact]
    public void A_history_of_nothing_but_idle_weeks_still_produces_a_full_series()
    {
        var idle = Enumerable.Range(0, 5)
            .Select(i => new WeeklyTrainingLoad(Week(i), 0m, 0, LoadBasis.None))
            .ToArray();

        var trends = TrainingLoadTrendCalculator.Calculate(
            idle, new DateRange(Week(1).Monday, Week(4).Sunday));

        Assert.Equal(4, trends.Count);
        Assert.All(trends, t =>
        {
            Assert.Equal(TrendClassification.Steady, t.Classification);
            Assert.Null(t.RelativeChange);
            Assert.Equal(LoadBasis.None, t.Basis);
            Assert.True(t.IsComplete);
        });
    }
}
