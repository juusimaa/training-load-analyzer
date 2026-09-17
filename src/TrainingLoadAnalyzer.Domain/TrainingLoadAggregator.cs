namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   Totals the training loads of many activities by calendar day and by ISO-8601 week.
/// </summary>
public static class TrainingLoadAggregator
{
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

        var pointsByWeek = new Dictionary<IsoWeek, decimal>();

        foreach (var activity in activities)
        {
            var week = IsoWeek.For(DayOf(activity));
            pointsByWeek[week] =
                pointsByWeek.GetValueOrDefault(week)
                + activity.CalculateTrainingLoad(maximumHeartRate).Points;
        }

        var series = new List<WeeklyTrainingLoad>();
        var lastMonday = IsoWeek.For(range.End).Monday;

        // Walking Mondays rather than incrementing a week number: adding seven days cannot get
        // the 52-versus-53-week rule wrong, and 2026 is a 53-week ISO year (research R6).
        for (var monday = IsoWeek.For(range.Start).Monday;
             monday <= lastMonday;
             monday = monday.AddDays(7))
        {
            var week = IsoWeek.For(monday);
            series.Add(new WeeklyTrainingLoad(week, pointsByWeek.GetValueOrDefault(week)));
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
