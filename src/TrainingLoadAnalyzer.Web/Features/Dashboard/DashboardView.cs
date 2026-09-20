using TrainingLoadAnalyzer.Domain;

namespace TrainingLoadAnalyzer.Web.Features.Dashboard;

/// <summary>
///   Everything one dashboard load displays (FR-001 - FR-011).
/// </summary>
/// <remarks>
///   Built from domain types and nothing else. No Strava type appears here, and none may (C85).
/// </remarks>
public sealed record DashboardView
{
    /// <summary>
    ///   How many days of history the chart needs before it is worth drawing (US3 scenario 2).
    /// </summary>
    private const int MinimumChartDays = 30;

    /// <summary>The athlete's local day this view describes (research R22).</summary>
    public required DateOnly AsOf { get; init; }

    public required bool IsStravaConnected { get; init; }

    /// <summary>
    ///   The maximum heart rate every measured load behind these figures was computed from
    ///   (008 Amendment 1(a), FR-003).
    /// </summary>
    /// <remarks>
    ///   Carried through from the argument the builder is given, which is the same value
    ///   <see cref="DashboardReader"/> already reads out of configuration for the estimation
    ///   itself. Reading it a second time at the point of display would create a second place for
    ///   the displayed number and the computed one to disagree — and the whole reason the rail
    ///   states it is so a wrong setting is visible rather than only implied by odd figures.
    /// </remarks>
    public required int MaximumHeartRate { get; init; }

    /// <summary>
    ///   The ISO-8601 week <see cref="AsOf"/> falls in, as <c>2026-W38</c> (008 Amendment 1(a),
    ///   FR-003).
    /// </summary>
    /// <remarks>
    ///   A label, derived and never stored. It is the same ISO week numbering the domain already
    ///   uses for <see cref="WeeklyTrainingLoad"/>, and no weekly computation reads it back.
    /// </remarks>
    public required string IsoWeek { get; init; }

    /// <summary>
    ///   The stored data could not be read. The page says so rather than crashing (C87).
    /// </summary>
    public bool IsUnavailable { get; init; }

    /// <summary>At most 180 days ending on <see cref="AsOf"/>, gap-free and ascending (FR-006).</summary>
    public IReadOnlyList<DailyTrainingMetrics> Metrics { get; init; } = [];

    public WeeklyTrainingLoad? CurrentWeek { get; init; }

    /// <summary>Null when the history does not reach the previous ISO week (C82).</summary>
    public WeeklyLoadTrend? Trend { get; init; }

    /// <summary>
    ///   Each day's total load over the same range as <see cref="Metrics"/>, aligned index for
    ///   index (008 Amendment 2, FR-006).
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The chart draws daily load as bars behind the three lines, and
    ///     <see cref="DailyTrainingMetrics"/> carries no load — it is fitness, fatigue and their
    ///     difference. This is the aggregation the builder already performs on its way to the
    ///     metrics series, carried through rather than discarded: no new query, no new arithmetic.
    ///   </para>
    ///   <para>
    ///     Its alignment with <see cref="Metrics"/> is the property the plot depends on. Both are
    ///     taken over one range, so an off-by-one cannot arise from a second calculation — it
    ///     would have to be introduced deliberately.
    ///   </para>
    /// </remarks>
    public IReadOnlyList<DailyTrainingLoad> DailyLoad { get; init; } = [];

    public IReadOnlyList<RecentActivity> Recent { get; init; } = [];

    /// <summary>
    ///   Today's figures: the last point of the chart, derived rather than stored (FR-001 - FR-003).
    /// </summary>
    /// <remarks>
    ///   This is what makes SC-002 structural. The three headline tiles and the chart's final point
    ///   are the same value by construction, so no amount of later editing can make them disagree —
    ///   the same device <see cref="DailyTrainingMetrics.Form"/> already uses (C80).
    /// </remarks>
    public DailyTrainingMetrics? Current => Metrics.Count == 0 ? null : Metrics[^1];

    /// <summary>
    ///   Whether there is any training to show at all (FR-011).
    /// </summary>
    /// <remarks>
    ///   Derived for the same reason as <see cref="Current"/>: no independent flag can drift out of
    ///   step with the figures. Taken from <see cref="Metrics"/> rather than from
    ///   <see cref="Recent"/> because the metrics series is the history itself, while the recent
    ///   list is one display slice of it — and if the slice ever came back empty by mistake, this
    ///   way the page shows a dashboard with an empty list rather than hiding the whole dashboard.
    /// </remarks>
    public bool HasActivities => Metrics.Count > 0;

    /// <summary>US3 scenario 2: under 30 days, a chart would be a line between two points.</summary>
    public bool HasEnoughHistoryForChart => Metrics.Count >= MinimumChartDays;
}
