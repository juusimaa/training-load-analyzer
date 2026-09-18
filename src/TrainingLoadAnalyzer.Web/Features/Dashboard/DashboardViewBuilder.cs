using TrainingLoadAnalyzer.Domain;

namespace TrainingLoadAnalyzer.Web.Features.Dashboard;

/// <summary>
///   Turns a stored history into the figures one dashboard load shows.
/// </summary>
/// <remarks>
///   Pure: the same activities, day and maximum always yield an equal view, with no read of the
///   clock, of storage, or of any ambient state (C79). That is deliberate and is most of this
///   feature's testability — it puts every question with a right answer in reach of a plain xUnit
///   assertion, leaving the components to be tested only for what is genuinely markup
///   (research R11).
/// </remarks>
public static class DashboardViewBuilder
{
    /// <summary>How many days the chart covers, today included (FR-006).</summary>
    private const int ChartDays = 180;

    public static DashboardView Build(
        IReadOnlyList<TrainingActivity> activities,
        DateOnly today,
        int maximumHeartRate,
        bool isStravaConnected)
    {
        ArgumentNullException.ThrowIfNull(activities);

        if (activities.Count == 0)
        {
            return new DashboardView { AsOf = today, IsStravaConnected = isStravaConnected };
        }

        var days = activities.Select(DayOf).ToList();
        var historyStart = days.Min();

        // The end is the later of today and the last recorded day. A session dated in the future -
        // a watch with a wrong clock - would otherwise either throw from DateRange or vanish from
        // the daily totals while still appearing in the recent list (research R22).
        var historyEnd = days.Max() > today ? days.Max() : today;

        var daily = TrainingLoadAggregator.AggregateDaily(
            activities,
            new DateRange(historyStart, historyEnd),
            maximumHeartRate);

        var chartStart = historyStart > today.AddDays(-(ChartDays - 1))
            ? historyStart
            : today.AddDays(-(ChartDays - 1));

        return new DashboardView
        {
            AsOf = today,
            IsStravaConnected = isStravaConnected,
            Metrics = TrainingMetricsCalculator.Calculate(daily, new DateRange(chartStart, today)),
        };
    }

    /// <summary>
    ///   The calendar day a session is attributed to: the athlete's local day at the session's own
    ///   recorded offset, exactly as feature 002 buckets it (002 FR-003, FR-004).
    /// </summary>
    private static DateOnly DayOf(TrainingActivity activity) =>
        DateOnly.FromDateTime(activity.StartedAt.DateTime);
}
