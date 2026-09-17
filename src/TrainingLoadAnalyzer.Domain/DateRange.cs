namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   The stretch of calendar days an aggregation was asked about, bounded at both ends and
///   including both of them (FR-009).
/// </summary>
public sealed class DateRange
{
    public DateRange(DateOnly start, DateOnly end)
    {
        // The missing-bound checks run first: a default end is also earlier than any real start,
        // so the opposite order would report the wrong rule (FR-021).
        if (start == default)
        {
            throw new ArgumentException(
                "The range starts unbounded. A range must be bounded at both ends.",
                nameof(start));
        }

        if (end == default)
        {
            throw new ArgumentException(
                "The range ends unbounded. A range must be bounded at both ends.",
                nameof(end));
        }

        if (end < start)
        {
            throw new ArgumentException(
                $"The range ends on {end:yyyy-MM-dd}, before it starts on {start:yyyy-MM-dd}.",
                nameof(end));
        }

        Start = start;
        End = end;
    }

    /// <summary>First day of the range, included.</summary>
    public DateOnly Start { get; }

    /// <summary>Last day of the range, included.</summary>
    public DateOnly End { get; }

    /// <summary>
    ///   Every day from <see cref="Start"/> to <see cref="End"/> inclusive, ascending (FR-005).
    /// </summary>
    public IEnumerable<DateOnly> Days
    {
        get
        {
            for (var day = Start; day <= End; day = day.AddDays(1))
            {
                yield return day;
            }
        }
    }
}
