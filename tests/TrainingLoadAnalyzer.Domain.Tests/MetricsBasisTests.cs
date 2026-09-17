namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   User Story 3 — know how far to trust the three numbers.
/// </summary>
public class MetricsBasisTests
{
    private const double Tolerance = 0.0001;

    private static readonly DateOnly HistoryStart = new(2026, 1, 1);

    private static DailyTrainingLoad MeasuredDay(DateOnly day) => new(day, 100m, 1, LoadBasis.Measured);

    private static List<DailyTrainingLoad> MeasuredRun(DateOnly from, int days) =>
        [.. Enumerable.Range(0, days).Select(i => MeasuredDay(from.AddDays(i)))];

    private static DateRange RangeOf(IReadOnlyList<DailyTrainingLoad> history) =>
        new(history[0].Day, history[^1].Day);

    // FR-013, FR-014: the warm-up is 42 days counted from and including the history's first day,
    // so 2026-02-11 is the 42nd day and 2026-02-12 the 43rd. No figure changes at this boundary,
    // which is why only the flag can reveal an off-by-one.
    [Fact]
    public void The_warm_up_ends_42_days_after_the_history_begins()
    {
        var history = MeasuredRun(HistoryStart, 100);

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));
        var fortySecondDay = series.Single(d => d.Day == new DateOnly(2026, 2, 11));
        var fortyThirdDay = series.Single(d => d.Day == new DateOnly(2026, 2, 12));

        Assert.False(fortySecondDay.IsReliable);
        Assert.True(fortyThirdDay.IsReliable);
    }

    // SC-007, C23: the boundary holds across the whole series, with no exceptions on either side.
    [Fact]
    public void Every_day_before_the_boundary_is_unreliable_and_every_day_after_it_is_reliable()
    {
        var history = MeasuredRun(HistoryStart, 100);

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        Assert.Equal(100, series.Count);
        Assert.All(series.Take(42), day => Assert.False(day.IsReliable));
        Assert.All(series.Skip(42), day => Assert.True(day.IsReliable));
    }

    // FR-015, C23: a warm-up day's figures are produced and qualified, never withheld or blanked.
    // They are the same numbers the recurrence gives; only the flag differs.
    [Fact]
    public void Figures_inside_the_warm_up_are_produced_and_merely_qualified()
    {
        var history = MeasuredRun(HistoryStart, 100);

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));
        var insideWarmUp = series[10];

        Assert.False(insideWarmUp.IsReliable);
        // The closed form of FR-005's recurrence for n days of a constant load, derived
        // independently of the implementation: load x (1 - e^(-n/42)). series[10] is day 11.
        Assert.Equal(100.0 * (1.0 - Math.Exp(-11.0 / 42.0)), insideWarmUp.Fitness, tolerance: Tolerance);
        Assert.True(insideWarmUp.Fitness > 0, "the figure is real, not blanked");
        Assert.True(insideWarmUp.Fatigue > 0, "the figure is real, not blanked");

        // The same day calculated from a longer history has identical figures: the warm-up flag
        // qualifies the number, it does not change it.
        var longer = TrainingMetricsCalculator.Calculate(
            MeasuredRun(HistoryStart, 200),
            new DateRange(HistoryStart, HistoryStart.AddDays(99)));

        Assert.Equal(series[10].Fitness, longer[10].Fitness);
        Assert.Equal(series[10].Fatigue, longer[10].Fatigue);
    }

    // User Story 3, scenario 1 (FR-016, FR-017): a figure built only from heart-rate-measured days
    // is reported as measured.
    [Fact]
    public void A_figure_built_only_from_measured_days_is_reported_as_measured()
    {
        var history = MeasuredRun(HistoryStart, 50);

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        Assert.Equal(LoadBasis.Measured, series[^1].FitnessBasis);
    }

    // FR-019: the two windows are different lengths. An estimated day 8 days back is inside the
    // 42-day fitness window and outside the 7-day fatigue one, so the two bases must disagree.
    [Fact]
    public void The_fatigue_window_is_shorter_than_the_fitness_window()
    {
        var history = MeasuredRun(HistoryStart, 50);
        var eightDaysBack = history.Count - 8;
        history[eightDaysBack] = history[eightDaysBack] with { Basis = LoadBasis.Estimated };

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        Assert.Equal(LoadBasis.Mixed, series[^1].FitnessBasis);
        Assert.Equal(LoadBasis.Measured, series[^1].FatigueBasis);
    }

    // User Story 3, scenario 2, and FR-017: one estimate among many measurements makes the whole
    // window mixed. The proportion is deliberately not recorded.
    [Fact]
    public void A_single_estimated_day_makes_the_whole_window_mixed()
    {
        var history = MeasuredRun(HistoryStart, 50);
        history[^3] = history[^3] with { Basis = LoadBasis.Estimated };

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        Assert.Equal(LoadBasis.Mixed, series[^1].FitnessBasis);
        Assert.Equal(LoadBasis.Mixed, series[^1].FatigueBasis);
    }

    // A history with no heart-rate data anywhere is estimated throughout - not mixed, and
    // certainly not measured.
    [Fact]
    public void A_figure_built_only_from_estimated_days_is_reported_as_estimated()
    {
        List<DailyTrainingLoad> history =
        [
            .. Enumerable.Range(0, 50).Select(i => new DailyTrainingLoad(
                HistoryStart.AddDays(i), 100m, 1, LoadBasis.Estimated)),
        ];

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        Assert.Equal(LoadBasis.Estimated, series[^1].FitnessBasis);
        Assert.Equal(LoadBasis.Estimated, series[^1].FatigueBasis);
    }

    // SC-009, C25: the window forgets. An unbounded rule would mark every day mixed forever after
    // the athlete's first ride without a heart-rate strap, which would tell them nothing.
    [Fact]
    public void An_estimated_day_stops_affecting_the_basis_once_it_leaves_the_window()
    {
        var history = MeasuredRun(HistoryStart, 100);
        const int EstimatedIndex = 20;
        history[EstimatedIndex] = history[EstimatedIndex] with { Basis = LoadBasis.Estimated };

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        for (var i = 0; i < series.Count; i++)
        {
            var daysSince = i - EstimatedIndex;
            var inFatigueWindow = daysSince >= 0 && daysSince < 7;
            var inFitnessWindow = daysSince >= 0 && daysSince < 42;

            Assert.Equal(
                inFatigueWindow ? LoadBasis.Mixed : LoadBasis.Measured,
                series[i].FatigueBasis);
            Assert.Equal(
                inFitnessWindow ? LoadBasis.Mixed : LoadBasis.Measured,
                series[i].FitnessBasis);
        }
    }

    // User Story 3, scenario 5, and FR-020: none means nothing in the window carried training.
    [Fact]
    public void A_window_containing_no_training_reports_no_basis()
    {
        List<DailyTrainingLoad> allRest =
        [
            .. Enumerable.Range(0, 60).Select(i => new DailyTrainingLoad(
                HistoryStart.AddDays(i), 0m, 0, LoadBasis.None)),
        ];

        var never = TrainingMetricsCalculator.Calculate(allRest, RangeOf(allRest));

        Assert.All(never, day => Assert.Equal(LoadBasis.None, day.FitnessBasis));
        Assert.All(never, day => Assert.Equal(LoadBasis.None, day.FatigueBasis));

        var trainedThenStopped = MeasuredRun(HistoryStart, 10);
        trainedThenStopped.AddRange(
            Enumerable.Range(0, 50).Select(i => new DailyTrainingLoad(
                HistoryStart.AddDays(10 + i), 0m, 0, LoadBasis.None)));

        var stopped = TrainingMetricsCalculator.Calculate(
            trainedThenStopped,
            RangeOf(trainedThenStopped));

        Assert.Equal(LoadBasis.None, stopped[^1].FitnessBasis);
        Assert.Equal(LoadBasis.None, stopped[^1].FatigueBasis);
    }

    // FR-018, C22: this distinction is the reason the basis has four states rather than three. A
    // rest day and a session that scored zero produce identical figures; only the basis tells them
    // apart, and collapsing them would make a rest day indistinguishable from a recorded easy one.
    [Fact]
    public void A_zero_point_session_contributes_its_basis_but_a_rest_day_does_not()
    {
        var restDay = new DailyTrainingLoad(HistoryStart, 0m, 0, LoadBasis.None);
        var zeroPointSession = new DailyTrainingLoad(HistoryStart, 0m, 1, LoadBasis.Measured);
        var oneDay = new DateRange(HistoryStart, HistoryStart);

        var rested = TrainingMetricsCalculator.Calculate([restDay], oneDay);
        var recorded = TrainingMetricsCalculator.Calculate([zeroPointSession], oneDay);

        Assert.Equal(LoadBasis.None, rested[0].FitnessBasis);
        Assert.Equal(LoadBasis.Measured, recorded[0].FitnessBasis);

        Assert.Equal(rested[0].Fitness, recorded[0].Fitness);
        Assert.Equal(rested[0].Fatigue, recorded[0].Fatigue);
    }

    // User Story 3, scenario 6 (FR-019a): the athlete trained with a heart-rate monitor three
    // weeks ago and has rested since. An empty fatigue window contributes nothing; it does not
    // count as disagreement, and nothing about this figure was estimated.
    [Fact]
    public void An_empty_fatigue_window_does_not_make_form_mixed()
    {
        var history = MeasuredRun(HistoryStart, 40);
        history.AddRange(
            Enumerable.Range(0, 10).Select(i => new DailyTrainingLoad(
                HistoryStart.AddDays(40 + i), 0m, 0, LoadBasis.None)));

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        Assert.Equal(LoadBasis.Measured, series[^1].FitnessBasis);
        Assert.Equal(LoadBasis.None, series[^1].FatigueBasis);
        Assert.Equal(LoadBasis.Measured, series[^1].FormBasis);
    }

    /// <summary>
    ///   A history mixing all four day shapes, built from a fixed pattern so it is the same on
    ///   every run.
    /// </summary>
    private static List<DailyTrainingLoad> VariedHistory(int days) =>
    [
        .. Enumerable.Range(0, days).Select(i => (i % 7) switch
        {
            0 => new DailyTrainingLoad(HistoryStart.AddDays(i), 0m, 0, LoadBasis.None),
            3 => new DailyTrainingLoad(HistoryStart.AddDays(i), 80m, 1, LoadBasis.Estimated),
            5 => new DailyTrainingLoad(HistoryStart.AddDays(i), 0m, 1, LoadBasis.Measured),
            _ => new DailyTrainingLoad(HistoryStart.AddDays(i), 100m, 1, LoadBasis.Measured),
        }),
    ];

    // FR-019b, C24a: the two can never differ, and the reason they cannot is that form's basis is
    // derived rather than stored. Asserted both behaviourally and structurally.
    [Fact]
    public void Form_basis_always_equals_fitness_basis_and_is_not_storable()
    {
        var history = VariedHistory(120);

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        Assert.All(series, day => Assert.Equal(day.FitnessBasis, day.FormBasis));

        var parameters = typeof(DailyTrainingMetrics)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("Form", parameters);
        Assert.DoesNotContain("FormBasis", parameters);

        foreach (var derived in new[] { nameof(DailyTrainingMetrics.Form), nameof(DailyTrainingMetrics.FormBasis) })
        {
            var property = typeof(DailyTrainingMetrics).GetProperty(derived);

            Assert.NotNull(property);
            Assert.Null(property.SetMethod);
        }
    }

    // SC-008: no figure is obtainable without its basis and its reliability marking.
    [Fact]
    public void Every_entry_carries_a_reliability_marking_and_all_three_bases()
    {
        var history = VariedHistory(120);

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        Assert.Equal(120, series.Count);
        Assert.All(series, day =>
        {
            Assert.True(Enum.IsDefined(day.FitnessBasis));
            Assert.True(Enum.IsDefined(day.FatigueBasis));
            Assert.True(Enum.IsDefined(day.FormBasis));
            Assert.Equal(day.Day >= HistoryStart.AddDays(42), day.IsReliable);
        });
    }
}
