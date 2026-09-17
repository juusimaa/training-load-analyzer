namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   Turns a daily training-load history into the day-by-day fitness, fatigue, and form series.
/// </summary>
public static class TrainingMetricsCalculator
{
    /// <summary>
    ///   The exponential smoothing factor for a 42-day time constant (FR-004, FR-005).
    /// </summary>
    /// <remarks>
    ///   Written as the formula rather than a transcribed literal, so a reviewer reads FR-005
    ///   directly and there is no digit to get wrong. Math.Exp gives about 17 significant figures,
    ///   well past FR-029a's ten (research R3).
    /// </remarks>
    private static readonly double AlphaFitness = 1.0 - Math.Exp(-1.0 / 42.0);

    /// <summary>The same for fatigue's 7-day time constant (FR-004, FR-005).</summary>
    private static readonly double AlphaFatigue = 1.0 - Math.Exp(-1.0 / 7.0);

    /// <summary>
    ///   The fitness, fatigue, and form of every day in <paramref name="range"/> (FR-008).
    /// </summary>
    public static IReadOnlyList<DailyTrainingMetrics> Calculate(
        IReadOnlyList<DailyTrainingLoad> history,
        DateRange range)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(range);

        // Both metrics start at zero on the notional day before the history begins (FR-012).
        var fitness = 0.0;
        var fatigue = 0.0;
        var series = new List<DailyTrainingMetrics>();

        foreach (var day in history)
        {
            var load = (double)day.Points;

            fitness += (load - fitness) * AlphaFitness;
            fatigue += (load - fatigue) * AlphaFatigue;

            series.Add(new DailyTrainingMetrics(day.Day, fitness, fatigue));
        }

        return series;
    }
}
