namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   User Story 1 — read today's fitness, fatigue, and form.
/// </summary>
public class TrainingMetricsTests
{
    /// <summary>
    ///   FR-029: the smoothing factors are irrational, so every figure is asserted to a stated
    ///   tolerance rather than exactly.
    /// </summary>
    private const double Tolerance = 0.0001;

    private static readonly DateOnly FirstDay = new(2026, 3, 1);

    private static DailyTrainingLoad TrainingDay(DateOnly day, decimal points = 100m) =>
        new(day, points, 1, LoadBasis.Measured);

    private static DateRange OneDay(DateOnly day) => new(day, day);

    /// <summary>A continuous run of identical days, starting at <paramref name="from"/>.</summary>
    private static List<DailyTrainingLoad> Run(DateOnly from, int days, decimal points) =>
        [.. Enumerable.Range(0, days).Select(i => TrainingDay(from.AddDays(i), points))];

    private static DateRange RangeOf(IReadOnlyList<DailyTrainingLoad> history) =>
        new(history[0].Day, history[^1].Day);

    /// <summary>
    ///   A continuous history of varied loads, including rest days, built from a fixed pattern so
    ///   it is the same on every run (FR-027).
    /// </summary>
    private static List<DailyTrainingLoad> VariedHistory(DateOnly from, int days) =>
    [
        .. Enumerable.Range(0, days).Select(i => i % 4 == 0
            ? new DailyTrainingLoad(from.AddDays(i), 0m, 0, LoadBasis.None)
            : TrainingDay(from.AddDays(i), 30m + i * 7 % 180)),
    ];

    // User Story 1: one day of load 100 applied to a seed of zero (FR-012) advances Fitness by
    // 100 x alpha, where alpha is 1 - e^(-1/42) (FR-005).
    [Fact]
    public void The_first_day_of_a_history_advances_fitness_from_a_seed_of_zero()
    {
        DailyTrainingLoad[] history = [TrainingDay(FirstDay)];

        var metrics = TrainingMetricsCalculator.Calculate(history, OneDay(FirstDay));

        Assert.Equal(2.352831335, metrics[0].Fitness, tolerance: Tolerance);
    }

    // User Story 1: the same day advances Fatigue by 100 x alpha, where alpha is 1 - e^(-1/7) -
    // a shorter time constant, so a single day moves it much further (FR-002, FR-005).
    [Fact]
    public void The_first_day_of_a_history_advances_fatigue_from_a_seed_of_zero()
    {
        DailyTrainingLoad[] history = [TrainingDay(FirstDay)];

        var metrics = TrainingMetricsCalculator.Calculate(history, OneDay(FirstDay));

        Assert.Equal(13.31221002, metrics[0].Fatigue, tolerance: Tolerance);
    }

    // User Story 1: form is the balance between the two. One hard day from nothing leaves the
    // athlete deep in the red (FR-003).
    [Fact]
    public void Form_is_fitness_minus_fatigue()
    {
        DailyTrainingLoad[] history = [TrainingDay(FirstDay)];

        var metrics = TrainingMetricsCalculator.Calculate(history, OneDay(FirstDay));

        Assert.Equal(-10.95937869, metrics[0].Form, tolerance: Tolerance);
    }

    // FR-005: the true exponential factor, not the 1/N approximation. After 42 days at a load of
    // 100 the exponential form gives 63.212056 where 1/42 gives 63.654405 - a gap of 0.44, more
    // than four thousand times the tolerance. This test was confirmed to fail against 1/42.
    [Fact]
    public void Fitness_follows_the_exponential_factor_and_not_the_reciprocal_approximation()
    {
        var history = Run(FirstDay, 42, 100m);

        var metrics = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        Assert.Equal(63.212056, metrics[^1].Fitness, tolerance: Tolerance);
    }

    // FR-012: the seed is zero, so the first day of a history reads 100 x alpha and not 100. A
    // warm start from the first day's own load would give 100 for both figures; this test was
    // confirmed to fail against that.
    [Fact]
    public void The_history_is_seeded_at_zero_and_not_from_its_first_day()
    {
        DailyTrainingLoad[] history = [TrainingDay(FirstDay)];

        var metrics = TrainingMetricsCalculator.Calculate(history, OneDay(FirstDay));

        Assert.Equal(2.352831335, metrics[0].Fitness, tolerance: Tolerance);
        Assert.Equal(13.31221002, metrics[0].Fatigue, tolerance: Tolerance);
        Assert.NotEqual(100.0, metrics[0].Fitness, tolerance: Tolerance);
        Assert.NotEqual(100.0, metrics[0].Fatigue, tolerance: Tolerance);
    }

    // User Story 1, scenario 1 (SC-004): sustained, unchanging training eventually stops
    // producing either freshness or fatigue.
    [Fact]
    public void Sustained_unchanging_training_settles_both_metrics_at_the_daily_load()
    {
        var history = Run(FirstDay, 365, 100m);

        var settled = TrainingMetricsCalculator.Calculate(history, RangeOf(history))[^1];

        Assert.Equal(99.9832, settled.Fitness, tolerance: Tolerance);
        Assert.Equal(100.0000, settled.Fatigue, tolerance: Tolerance);
        Assert.Equal(-0.0168, settled.Form, tolerance: Tolerance);

        // SC-004 states the outcome as a proportion, so assert it that way too.
        Assert.True(Math.Abs(settled.Fitness - 100.0) < 1.0, "fitness settles within 1% of the load");
        Assert.True(Math.Abs(settled.Fatigue - 100.0) < 1.0, "fatigue settles within 1% of the load");
        Assert.True(Math.Abs(settled.Form) < 1.0, "form settles within 1% of the load of zero");
    }

    // User Story 1, scenario 2: a hard block drives fatigue up faster than fitness, and form
    // negative.
    [Fact]
    public void A_hard_block_raises_fatigue_further_than_fitness_and_turns_form_negative()
    {
        var history = Run(FirstDay, 200, 100m);
        var settledDay = history[^1].Day;
        history.AddRange(Run(settledDay.AddDays(1), 7, 200m));

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));
        var settled = series[199];
        var afterBlock = series[^1];

        Assert.Equal(114.6281, afterBlock.Fitness, tolerance: Tolerance);
        Assert.Equal(163.2121, afterBlock.Fatigue, tolerance: Tolerance);
        Assert.Equal(-48.5839, afterBlock.Form, tolerance: Tolerance);

        Assert.True(
            afterBlock.Fatigue - settled.Fatigue > afterBlock.Fitness - settled.Fitness,
            "fatigue rises further than fitness over the block");
        Assert.True(afterBlock.Form < 0, "form is negative on the last day of the block");
    }

    // User Story 1, scenario 3 (SC-005): a rest week sheds fatigue much faster than fitness, and
    // form climbs. If form does not turn positive here, the time constants are the wrong way round.
    [Fact]
    public void A_rest_week_sheds_fatigue_faster_than_fitness_and_turns_form_positive()
    {
        var history = Run(FirstDay, 200, 100m);
        var settledDay = history[^1].Day;
        history.AddRange(Run(settledDay.AddDays(1), 7, 0m));

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));
        var settled = series[199];
        var afterRest = series[^1];

        Assert.Equal(99.1451, settled.Fitness, tolerance: Tolerance);
        Assert.Equal(100.0000, settled.Fatigue, tolerance: Tolerance);

        Assert.Equal(83.9245, afterRest.Fitness, tolerance: Tolerance);
        Assert.Equal(36.7879, afterRest.Fatigue, tolerance: Tolerance);
        Assert.Equal(47.1365, afterRest.Form, tolerance: Tolerance);

        var fitnessFell = 1.0 - afterRest.Fitness / settled.Fitness;
        var fatigueFell = 1.0 - afterRest.Fatigue / settled.Fatigue;

        Assert.True(fatigueFell > fitnessFell, "fatigue falls by the larger proportion");
        Assert.True(afterRest.Form > 0, "form is positive on the last day of the rest week");
    }

    // User Story 1, scenario 4 (SC-003, C20): form never disagrees with the two figures it comes
    // from. Asserted exactly, with no tolerance, because it is derived rather than accumulated.
    [Fact]
    public void Form_equals_fitness_minus_fatigue_on_every_day()
    {
        var history = VariedHistory(FirstDay, 60);

        var series = TrainingMetricsCalculator.Calculate(history, RangeOf(history));

        Assert.All(series, day => Assert.Equal(day.Fitness - day.Fatigue, day.Form));
    }

    // FR-003 asserted structurally: form is not storable, so no later change can quietly turn it
    // into state that drifts from its components.
    [Fact]
    public void Form_is_not_a_constructor_parameter_and_has_no_setter()
    {
        var constructors = typeof(DailyTrainingMetrics).GetConstructors();
        var parameters = constructors.SelectMany(c => c.GetParameters()).Select(p => p.Name);

        Assert.DoesNotContain("Form", parameters);

        var form = typeof(DailyTrainingMetrics).GetProperty(nameof(DailyTrainingMetrics.Form));

        Assert.NotNull(form);
        Assert.Null(form.SetMethod);
    }

    // User Story 1, scenario 5 (FR-027, C26, SC-010): the same inputs yield the same figures every
    // time, on any date.
    [Fact]
    public void Calculating_twice_over_the_same_inputs_produces_identical_figures()
    {
        var history = VariedHistory(FirstDay, 60);
        var range = RangeOf(history);

        var first = TrainingMetricsCalculator.Calculate(history, range);
        var second = TrainingMetricsCalculator.Calculate(history, range);

        Assert.Equal(first.Count, second.Count);

        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Day, second[i].Day);
            Assert.Equal(first[i].Fitness, second[i].Fitness);
            Assert.Equal(first[i].Fatigue, second[i].Fatigue);
        }
    }
}
