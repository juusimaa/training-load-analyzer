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
    ///   How many days of history must stand behind a figure before the zero seed has decayed far
    ///   enough out of it for the figure to be presented without qualification (FR-014).
    /// </summary>
    private const int WarmUpDays = 42;

    /// <summary>
    ///   How many days back the basis of fitness is read from: its own time constant, so the
    ///   qualifier covers the period the figure actually reflects (FR-019).
    /// </summary>
    private const int FitnessWindowDays = 42;

    /// <summary>The same for fatigue, over its own shorter time constant (FR-019).</summary>
    private const int FatigueWindowDays = 7;

    /// <summary>
    ///   The fitness, fatigue, and form of every day in <paramref name="range"/> (FR-008).
    /// </summary>
    public static IReadOnlyList<DailyTrainingMetrics> Calculate(
        IReadOnlyList<DailyTrainingLoad> history,
        DateRange range)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(range);

        // Ordered before the guard below, which indexes the first day (research R13).
        if (history.Count == 0)
        {
            throw new ArgumentException(
                "The history is empty, so it cannot reach back to the requested range's first "
                    + $"day of {range.Start:yyyy-MM-dd}.",
                nameof(history));
        }

        if (history[0].Day > range.Start)
        {
            throw new ArgumentException(
                $"The history starts on {history[0].Day:yyyy-MM-dd}, after the requested range "
                    + $"starts on {range.Start:yyyy-MM-dd}. The metrics of the range's first day "
                    + "need every day before it.",
                nameof(history));
        }

        if (history[^1].Day < range.End)
        {
            throw new ArgumentException(
                $"The history ends on {history[^1].Day:yyyy-MM-dd}, before the requested range "
                    + $"ends on {range.End:yyyy-MM-dd}. The missing days cannot be treated as "
                    + "rest without inventing training history.",
                nameof(history));
        }

        // One check catches all three shapes FR-023 names: a gap, a duplicate and an inversion
        // are the same violation seen from different sides. Refused rather than repaired -
        // filling a gap with a rest day would silently change every figure after it.
        for (var i = 1; i < history.Count; i++)
        {
            if (history[i].Day != history[i - 1].Day.AddDays(1))
            {
                throw new ArgumentException(
                    $"The history is not continuous: the day at index {i} is "
                        + $"{history[i].Day:yyyy-MM-dd}, which does not follow "
                        + $"{history[i - 1].Day:yyyy-MM-dd}.",
                    nameof(history));
            }
        }

        // Both metrics start at zero on the notional day before the history begins (FR-012).
        var fitness = 0.0;
        var fatigue = 0.0;
        var reliableFrom = history[0].Day.AddDays(WarmUpDays);
        var series = new List<DailyTrainingMetrics>();

        for (var i = 0; i < history.Count; i++)
        {
            var day = history[i];
            var load = (double)day.Points;

            fitness += (load - fitness) * AlphaFitness;
            fatigue += (load - fatigue) * AlphaFatigue;

            // The accumulators advance across every day of the history, but only the requested
            // range is returned: the days before it are what give the range's first day its
            // accumulated effect (FR-011, FR-024).
            if (day.Day >= range.Start && day.Day <= range.End)
            {
                series.Add(new DailyTrainingMetrics(
                    day.Day,
                    fitness,
                    fatigue,
                    day.Day >= reliableFrom,
                    BasisOver(history, i, FitnessWindowDays),
                    BasisOver(history, i, FatigueWindowDays)));
            }
        }

        return series;
    }

    /// <summary>
    ///   The combined basis of the <paramref name="windowDays"/> days ending at
    ///   <paramref name="last"/>, that day included (FR-019).
    /// </summary>
    /// <remarks>
    ///   A day contributes exactly when its own basis is not <see cref="LoadBasis.None"/> — which
    ///   by feature 002's guarantee C12 is exactly when it had at least one session. A day that
    ///   carried no training contributed no load, so it has nothing that could have been measured
    ///   or estimated (FR-018). Where the window reaches back past the start of the history it is
    ///   truncated to the days present, for the same reason.
    /// </remarks>
    private static LoadBasis BasisOver(IReadOnlyList<DailyTrainingLoad> history, int last, int windowDays)
    {
        var first = Math.Max(0, last - windowDays + 1);
        var anyMeasured = false;
        var anyEstimated = false;

        for (var i = first; i <= last; i++)
        {
            anyMeasured |= history[i].Basis is LoadBasis.Measured or LoadBasis.Mixed;
            anyEstimated |= history[i].Basis is LoadBasis.Estimated or LoadBasis.Mixed;
        }

        // Feature 002's rule, reused unchanged: one estimate among many measurements makes the
        // whole window mixed, and the proportion is deliberately not recorded (FR-017).
        return (anyMeasured, anyEstimated) switch
        {
            (true, true) => LoadBasis.Mixed,
            (true, false) => LoadBasis.Measured,
            (false, true) => LoadBasis.Estimated,
            (false, false) => LoadBasis.None,
        };
    }
}
