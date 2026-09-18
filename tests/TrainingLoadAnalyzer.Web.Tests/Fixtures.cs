using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Web.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   The fixture histories every test in this feature draws from (tasks.md, "Fixture histories").
/// </summary>
/// <remarks>
///   Loads are <strong>estimated</strong> unless a session is given a heart-rate series, so a load
///   is exactly <c>minutes × 2</c> (001 FR-013) and every expectation is arithmetic a reviewer can
///   check by hand, independent of the maximum heart rate.
/// </remarks>
internal static class Fixtures
{
    /// <summary>The athlete's local day in every test: Friday 2026-09-18, ISO week 2026-W38.</summary>
    public static readonly DateOnly Today = new(2026, 9, 18);

    public const int MaximumHeartRate = 190;

    /// <summary>A 60-minute session, estimated, worth exactly 120 points.</summary>
    public static TrainingActivity Session(
        DateOnly day,
        int minutes = 60,
        ActivityType type = ActivityType.Running,
        string? id = null,
        HeartRateSeries? heartRate = null,
        int hour = 7) => new(
        id ?? $"fixture-{day:yyyyMMdd}-{hour:00}",
        new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), FixedLocalClock.FixtureOffset),
        TimeSpan.FromMinutes(minutes),
        type,
        heartRate);

    /// <summary>A series whose every sample sits in a zone, so the load is measured.</summary>
    public static HeartRateSeries Measured() => new(
        [.. Enumerable.Range(0, 60).Select(i => new HeartRateSample(TimeSpan.FromMinutes(i), 150))]);

    /// <summary>H1 — one 60-minute run on 2026-09-18. 120 points.</summary>
    public static IReadOnlyList<TrainingActivity> H1 => [Session(Today)];

    /// <summary>
    ///   H2 — two ISO weeks: 2026-W37 carries 360 points over three sessions, 2026-W38 carries 480
    ///   over four. The change is +120 points and +1/3, clearing both of feature 004's thresholds.
    /// </summary>
    public static IReadOnlyList<TrainingActivity> H2 =>
    [
        .. new[] { 7, 9, 11 }.Select(d => Session(new DateOnly(2026, 9, d))),
        .. new[] { 14, 15, 16, 17 }.Select(d => Session(new DateOnly(2026, 9, d))),
    ];

    /// <summary>H3b — a single activity, today. No previous week, and far under 30 days.</summary>
    public static IReadOnlyList<TrainingActivity> H3b => [Session(Today)];

    /// <summary>H3c / H3d / H3e — <paramref name="days"/> consecutive daily sessions ending today.</summary>
    public static IReadOnlyList<TrainingActivity> ConsecutiveDays(int days) =>
        [.. Enumerable.Range(0, days).Select(i => Session(Today.AddDays(-i)))];

    /// <summary>H3f — ten sessions over three weeks, the newest of them measured.</summary>
    public static IReadOnlyList<TrainingActivity> H3f =>
    [
        .. Enumerable.Range(1, 9).Select(i => Session(
            Today.AddDays(-i * 2),
            60,
            i % 2 == 0 ? ActivityType.Cycling : ActivityType.Running)),
        Session(Today, 45, ActivityType.Cycling, heartRate: Measured()),
    ];

    /// <summary>H3g — three sessions, fewer than the seven the list shows.</summary>
    public static IReadOnlyList<TrainingActivity> H3g =>
        [.. new[] { 0, 2, 4 }.Select(i => Session(Today.AddDays(-i)))];
}
