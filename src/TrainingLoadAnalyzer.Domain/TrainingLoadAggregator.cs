namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   Totals the training loads of many activities by calendar day and by ISO-8601 week.
/// </summary>
public static class TrainingLoadAggregator
{
    private const int DaysPerWeek = 7;

    /// <summary>
    ///   The total load of every activity in <paramref name="range"/>, per calendar day (FR-001).
    /// </summary>
    public static IReadOnlyList<DailyTrainingLoad> AggregateDaily(
        IReadOnlyCollection<TrainingActivity> activities,
        DateRange range,
        int maximumHeartRate)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(range);

        var pointsByDay = PointsByDay(activities, maximumHeartRate);
        var series = new List<DailyTrainingLoad>();

        // Walking the range rather than grouping the activities is what makes the series
        // gap-free: a day nobody trained on has no entry to group (FR-005, FR-010).
        foreach (var day in range.Days)
        {
            series.Add(new DailyTrainingLoad(day, pointsByDay.GetValueOrDefault(day)));
        }

        return series;
    }

    /// <summary>
    ///   The total load of every activity in <paramref name="range"/>, per ISO-8601 week (FR-002).
    /// </summary>
    public static IReadOnlyList<WeeklyTrainingLoad> AggregateWeekly(
        IReadOnlyCollection<TrainingActivity> activities,
        DateRange range,
        int maximumHeartRate)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(range);

        // FR-013: a weekly total covers its whole ISO week, so the series is built over a
        // range extended out to the Monday opening the first week and the Sunday closing the
        // last - which is why it can include load from days outside the requested range.
        var extended = new DateRange(
            IsoWeek.For(range.Start).Monday,
            IsoWeek.For(range.End).Sunday);

        // Chunking a contiguous daily series into sevens is what makes FR-017 and SC-012
        // structural rather than properties to be maintained: a week IS the sum of its seven
        // days, and every week covers seven days, by construction (research R8).
        var days = AggregateDaily(activities, extended, maximumHeartRate);
        var series = new List<WeeklyTrainingLoad>();

        for (var first = 0; first < days.Count; first += DaysPerWeek)
        {
            var points = 0m;

            for (var offset = 0; offset < DaysPerWeek; offset++)
            {
                points += days[first + offset].Points;
            }

            series.Add(new WeeklyTrainingLoad(IsoWeek.For(days[first].Day), points));
        }

        return series;
    }

    /// <summary>
    ///   The calendar day an activity is attributed to: the athlete's local day at the activity's
    ///   own recorded offset (FR-003, FR-004).
    /// </summary>
    private static DateOnly DayOf(TrainingActivity activity) =>
        DateOnly.FromDateTime(activity.StartedAt.DateTime);

    private static Dictionary<DateOnly, decimal> PointsByDay(
        IReadOnlyCollection<TrainingActivity> activities,
        int maximumHeartRate)
    {
        var pointsByDay = new Dictionary<DateOnly, decimal>();

        foreach (var activity in activities)
        {
            var day = DayOf(activity);
            pointsByDay[day] =
                pointsByDay.GetValueOrDefault(day)
                + activity.CalculateTrainingLoad(maximumHeartRate).Points;
        }

        return pointsByDay;
    }
}
