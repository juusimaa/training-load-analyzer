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

    /// <summary>How many sessions the recent list shows (FR-007).</summary>
    private const int RecentCount = 7;

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

        var weekly = TrainingLoadAggregator.AggregateWeekly(
            activities,
            new DateRange(historyStart, historyEnd),
            maximumHeartRate);

        var thisWeek = IsoWeek.For(today);

        return new DashboardView
        {
            AsOf = today,
            IsStravaConnected = isStravaConnected,
            Metrics = TrainingMetricsCalculator.Calculate(daily, new DateRange(chartStart, today)),
            CurrentWeek = weekly.FirstOrDefault(w => w.Week == thisWeek),
            Trend = TrendFor(weekly, historyStart, today),
            Recent = RecentFrom(activities, maximumHeartRate),
        };
    }

    /// <summary>
    ///   This week's comparison against the one before it, or nothing when there is no week before
    ///   it to compare against (FR-005, C82).
    /// </summary>
    /// <remarks>
    ///   The guard is a <em>precondition check</em>, not a caught exception, and the difference is
    ///   the point. <see cref="TrainingLoadTrendCalculator"/> refuses a history that cannot reach
    ///   the previous week rather than inventing a rest week to compare against (004 FR-023) — and
    ///   an athlete whose first session is in the current week is an ordinary new user, not an
    ///   error. "Is there a previous week?" is a question with an answer (research R19).
    /// </remarks>
    private static WeeklyLoadTrend? TrendFor(
        IReadOnlyList<WeeklyTrainingLoad> weekly,
        DateOnly historyStart,
        DateOnly today)
    {
        var thisMonday = IsoWeek.For(today).Monday;

        if (IsoWeek.For(historyStart).Monday > thisMonday.AddDays(-7))
        {
            return null;
        }

        var trends = TrainingLoadTrendCalculator.Calculate(weekly, new DateRange(thisMonday, today));

        return trends.Count == 0 ? null : trends[^1];
    }

    /// <summary>
    ///   The most recent sessions, newest first (FR-007, FR-008, C84).
    /// </summary>
    /// <remarks>
    ///   A sort and a <c>Take</c> over the history that is already in memory. No query of its own:
    ///   the headline figures need the whole history regardless, so a "most recent seven" query
    ///   would be a second code path returning a subset of what was already read (research R15).
    ///   <para>
    ///     The load keeps its <c>TrainingLoad</c> shape rather than being split into a number and a
    ///     flag, because feature 001 made the two inseparable so that a consumer could not drop one
    ///     of them (001 SC-007) — which is precisely what FR-008 needs.
    ///   </para>
    /// </remarks>
    private static IReadOnlyList<RecentActivity> RecentFrom(
        IReadOnlyList<TrainingActivity> activities,
        int maximumHeartRate) =>
    [
        .. activities
            .OrderByDescending(a => a.StartedAt)
            .Take(RecentCount)
            .Select(a => new RecentActivity(
                DayOf(a),
                a.Type,
                a.MovingTime,
                a.CalculateTrainingLoad(maximumHeartRate))),
    ];

    /// <summary>
    ///   The calendar day a session is attributed to: the athlete's local day at the session's own
    ///   recorded offset, exactly as feature 002 buckets it (002 FR-003, FR-004).
    /// </summary>
    private static DateOnly DayOf(TrainingActivity activity) =>
        DateOnly.FromDateTime(activity.StartedAt.DateTime);
}
