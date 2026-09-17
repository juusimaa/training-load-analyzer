namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   User Story 2 — follow how the three metrics evolved.
/// </summary>
public class MetricsSeriesTests
{
    private const double Tolerance = 0.0001;

    private static DailyTrainingLoad TrainingDay(DateOnly day, decimal points = 100m) =>
        new(day, points, 1, LoadBasis.Measured);

    private static DailyTrainingLoad RestDay(DateOnly day) => new(day, 0m, 0, LoadBasis.None);

    private static List<DailyTrainingLoad> Run(DateOnly from, int days, decimal points) =>
        [.. Enumerable.Range(0, days).Select(i => TrainingDay(from.AddDays(i), points))];

    private static List<DailyTrainingLoad> RestRun(DateOnly from, int days) =>
        [.. Enumerable.Range(0, days).Select(i => RestDay(from.AddDays(i)))];

    // User Story 2, scenario 1 (FR-008, FR-011, FR-024, C19): the range decides what comes back,
    // the history decides what is counted. March's first day already carries two months of work.
    [Fact]
    public void Only_the_requested_range_is_returned_but_earlier_history_still_counts()
    {
        var history = Run(new DateOnly(2026, 1, 1), 90, 100m);
        var march = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var series = TrainingMetricsCalculator.Calculate(history, march);

        Assert.Equal(31, series.Count);
        Assert.Equal(new DateOnly(2026, 3, 1), series[0].Day);
        Assert.Equal(new DateOnly(2026, 3, 31), series[^1].Day);
        Assert.Equal(76.034896, series[0].Fitness, tolerance: Tolerance);
        Assert.Equal(99.981056, series[0].Fatigue, tolerance: Tolerance);
    }

    // The other half of FR-024: history reaching past the range's end is used for nothing the
    // caller sees, and the days it does cover still accumulate normally.
    [Fact]
    public void History_extending_past_the_range_stops_contributing_at_the_range_end()
    {
        var history = Run(new DateOnly(2026, 1, 1), 90, 100m);
        var toMidMarch = new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 15));

        var truncated = TrainingMetricsCalculator.Calculate(history, toMidMarch);
        var whole = TrainingMetricsCalculator.Calculate(
            history,
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)));

        Assert.Equal(15, truncated.Count);
        Assert.Equal(new DateOnly(2026, 3, 15), truncated[^1].Day);
        Assert.Equal(88.268083, whole[^1].Fitness, tolerance: Tolerance);
    }

    // User Story 2, scenario 3 (FR-009, C22): a rest day is a real input. An implementation that
    // iterates only the days with training would leave both figures unchanged here.
    [Fact]
    public void A_rest_day_decays_the_metrics_rather_than_being_skipped()
    {
        var firstDay = new DateOnly(2026, 3, 1);
        DailyTrainingLoad[] history = [TrainingDay(firstDay), RestDay(firstDay.AddDays(1))];

        var series = TrainingMetricsCalculator.Calculate(
            history,
            new DateRange(firstDay, firstDay.AddDays(1)));

        Assert.Equal(2.2974731819, series[1].Fitness, tolerance: Tolerance);
        Assert.Equal(11.5400606675, series[1].Fatigue, tolerance: Tolerance);
        Assert.True(series[1].Fitness < series[0].Fitness, "fitness decayed across the rest day");
        Assert.True(series[1].Fatigue < series[0].Fatigue, "fatigue decayed across the rest day");
    }

    // User Story 2, scenario 2 (FR-009, SC-006): no training at all still produces a full series.
    [Fact]
    public void A_range_with_no_training_still_produces_an_entry_for_every_day()
    {
        var firstDay = new DateOnly(2026, 3, 1);
        var neverTrained = RestRun(firstDay, 30);

        var flat = TrainingMetricsCalculator.Calculate(
            neverTrained,
            new DateRange(firstDay, firstDay.AddDays(29)));

        Assert.Equal(30, flat.Count);
        Assert.All(flat, day => Assert.Equal(0.0, day.Fitness));
        Assert.All(flat, day => Assert.Equal(0.0, day.Fatigue));

        var trainedThenStopped = Run(firstDay, 10, 100m);
        trainedThenStopped.AddRange(RestRun(firstDay.AddDays(10), 30));

        var decaying = TrainingMetricsCalculator.Calculate(
            trainedThenStopped,
            new DateRange(firstDay.AddDays(10), firstDay.AddDays(39)));

        Assert.Equal(30, decaying.Count);

        for (var i = 1; i < decaying.Count; i++)
        {
            Assert.True(decaying[i].Fitness < decaying[i - 1].Fitness, "fitness decreases daily");
            Assert.True(decaying[i].Fatigue < decaying[i - 1].Fatigue, "fatigue decreases daily");
            Assert.True(decaying[i].Fitness > 0, "fitness decays toward zero without reaching it");
        }
    }

    // User Story 2, scenario 4 (SC-002, C21): every consecutive pair follows FR-005's recurrence,
    // so any day in the series can be reproduced from the day before it.
    [Fact]
    public void Every_day_follows_from_the_day_before_it_under_the_stated_recurrence()
    {
        var alphaFitness = 1.0 - Math.Exp(-1.0 / 42.0);
        var alphaFatigue = 1.0 - Math.Exp(-1.0 / 7.0);

        var firstDay = new DateOnly(2026, 3, 1);
        List<DailyTrainingLoad> history =
        [
            .. Enumerable.Range(0, 60).Select(i => i % 5 == 0
                ? RestDay(firstDay.AddDays(i))
                : TrainingDay(firstDay.AddDays(i), 40m + i * 11 % 150)),
        ];

        var series = TrainingMetricsCalculator.Calculate(
            history,
            new DateRange(firstDay, firstDay.AddDays(59)));

        for (var i = 1; i < series.Count; i++)
        {
            var load = (double)history[i].Points;

            Assert.Equal(
                series[i - 1].Fitness + (load - series[i - 1].Fitness) * alphaFitness,
                series[i].Fitness,
                tolerance: Tolerance);
            Assert.Equal(
                series[i - 1].Fatigue + (load - series[i - 1].Fatigue) * alphaFatigue,
                series[i].Fatigue,
                tolerance: Tolerance);
        }
    }

    // User Story 2, scenario 5 (SC-001): a one-day range is one entry, not an error.
    [Fact]
    public void A_single_day_range_produces_exactly_one_entry()
    {
        var firstDay = new DateOnly(2026, 3, 1);
        var history = Run(firstDay, 10, 100m);
        var oneDay = firstDay.AddDays(5);

        var series = TrainingMetricsCalculator.Calculate(history, new DateRange(oneDay, oneDay));

        Assert.Single(series);
        Assert.Equal(oneDay, series[0].Day);
    }
}
