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

            trends.Add(new WeeklyLoadTrend(
                history[i].Week,
                history[i].Points,
                history[i - 1].Points));
        }

        return trends;
    }
}
