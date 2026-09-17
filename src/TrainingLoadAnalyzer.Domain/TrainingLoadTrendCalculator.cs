namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   Compares each ISO-8601 week's training load against the week immediately before it.
/// </summary>
public static class TrainingLoadTrendCalculator
{
    /// <summary>
    ///   The trend of every ISO week touching <paramref name="range"/> (FR-028).
    /// </summary>
    public static IReadOnlyList<WeeklyLoadTrend> Calculate(
        IReadOnlyList<WeeklyTrainingLoad> history,
        DateRange range)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(range);

        // Weeks are ordered by their Monday rather than by (Year, Week): the ISO week-numbering
        // year is not the calendar year at the boundary, and 2026 is a 53-week year, which is
        // exactly where a comparison on the numbers goes wrong (research R9).
        var firstMonday = IsoWeek.For(range.Start).Monday;
        var lastMonday = IsoWeek.For(range.End).Monday;
        var requiredFirst = firstMonday.AddDays(-7);

        // Ordered before the guard below, which indexes the first week (research R13).
        if (history.Count == 0)
        {
            throw new ArgumentException(
                "The history is empty, so it cannot reach back to the week before the requested "
                    + $"range, beginning {requiredFirst:yyyy-MM-dd}.",
                nameof(history));
        }

        if (history[0].Week.Monday > requiredFirst)
        {
            throw new ArgumentException(
                $"The history starts with the week beginning {history[0].Week.Monday:yyyy-MM-dd}, "
                    + $"so the week beginning {requiredFirst:yyyy-MM-dd} is missing. The range's "
                    + "first week has nothing to be compared against.",
                nameof(history));
        }

        if (history[^1].Week.Monday < lastMonday)
        {
            throw new ArgumentException(
                $"The history ends with the week beginning {history[^1].Week.Monday:yyyy-MM-dd}, "
                    + $"before the requested range's last week, beginning {lastMonday:yyyy-MM-dd}. "
                    + "The missing weeks cannot be treated as rest without inventing training "
                    + "history.",
                nameof(history));
        }

        // One check catches all three shapes FR-024 names: a gap, a repeated week and an
        // inversion are the same violation seen from different sides. Refused rather than
        // repaired - a silently skipped week compares against the wrong predecessor.
        for (var i = 1; i < history.Count; i++)
        {
            if (history[i].Week.Monday != history[i - 1].Week.Monday.AddDays(7))
            {
                throw new ArgumentException(
                    $"The history is not continuous: the week at index {i} begins "
                        + $"{history[i].Week.Monday:yyyy-MM-dd}, which does not follow the week "
                        + $"beginning {history[i - 1].Week.Monday:yyyy-MM-dd}.",
                    nameof(history));
            }
        }

        var trends = new List<WeeklyLoadTrend>();

        // Each week is compared against its immediate predecessor, so the first week of the
        // history has nothing to report (FR-001). Weeks outside the range are walked past
        // rather than skipped over: the one before the range is what its first entry compares
        // against (FR-023).
        for (var i = 1; i < history.Count; i++)
        {
            var monday = history[i].Week.Monday;

            if (monday < firstMonday || monday > lastMonday)
            {
                continue;
            }

            var week = history[i].Week;

            trends.Add(new WeeklyLoadTrend(
                week,
                history[i].Points,
                history[i - 1].Points,
                // Determined from the range alone, never from the totals and never from the
                // clock, so the same week and range answer the same on any date (FR-016).
                week.Monday >= range.Start && week.Sunday <= range.End,
                CombinedBasis(history[i - 1].Basis, history[i].Basis)));
        }

        return trends;
    }

    /// <summary>
    ///   How far a comparison of the two weeks can be trusted (FR-021).
    /// </summary>
    /// <remarks>
    ///   Feature 002's rule, reused unchanged: one estimate alongside a measurement makes the
    ///   whole thing mixed, and the proportion is deliberately not recorded. A basis of
    ///   <see cref="LoadBasis.None"/> contributes to neither tally, so a measured week following
    ///   an untrained one is measured - nothing about that comparison was estimated.
    ///   <para>
    ///     This is the third place in the solution expressing this rule. Extracting it was
    ///     considered and declined on 2026-09-17 (research R12): the three sites share only this
    ///     final switch and each accumulates differently. The trigger for revisiting is a fourth
    ///     occurrence.
    ///   </para>
    /// </remarks>
    private static LoadBasis CombinedBasis(LoadBasis previous, LoadBasis current)
    {
        var anyMeasured = Measured(previous) || Measured(current);
        var anyEstimated = Estimated(previous) || Estimated(current);

        return (anyMeasured, anyEstimated) switch
        {
            (true, true) => LoadBasis.Mixed,
            (true, false) => LoadBasis.Measured,
            (false, true) => LoadBasis.Estimated,
            (false, false) => LoadBasis.None,
        };

        static bool Measured(LoadBasis basis) =>
            basis is LoadBasis.Measured or LoadBasis.Mixed;

        static bool Estimated(LoadBasis basis) =>
            basis is LoadBasis.Estimated or LoadBasis.Mixed;
    }
}
