using System.Globalization;

namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   One ISO-8601 week, running Monday to Sunday (FR-002).
/// </summary>
/// <remarks>
///   <see cref="Year"/> is the ISO week-numbering year, which is not always the calendar year of
///   the week's days: 2025-12-29 belongs to 2026-W01, and 2027-01-03 belongs to 2026-W53.
/// </remarks>
public readonly record struct IsoWeek
{
    private IsoWeek(int year, int week, DateOnly monday)
    {
        Year = year;
        Week = week;
        Monday = monday;
    }

    /// <summary>The ISO-8601 week containing <paramref name="day"/>.</summary>
    public static IsoWeek For(DateOnly day)
    {
        // ISOWeek takes DateTime, so the conversion happens at this one place. The week rule is
        // the platform's rather than ours: the year boundary is where a hand-rolled one goes
        // wrong, and 2026 is a 53-week ISO year (research R5).
        var moment = day.ToDateTime(TimeOnly.MinValue);
        var year = ISOWeek.GetYear(moment);
        var week = ISOWeek.GetWeekOfYear(moment);

        return new IsoWeek(
            year,
            week,
            DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday)));
    }

    /// <summary>The ISO week-numbering year, not necessarily the calendar year of its days.</summary>
    public int Year { get; }

    /// <summary>The ISO week number, 1-53.</summary>
    public int Week { get; }

    /// <summary>The first day of the week.</summary>
    public DateOnly Monday { get; }

    /// <summary>The last day of the week, derived so it can never disagree with the rest.</summary>
    public DateOnly Sunday => Monday.AddDays(6);
}
