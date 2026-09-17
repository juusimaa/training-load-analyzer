using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Sync;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   Turns one Strava activity into a session, or explains why it could not (FR-009 - FR-020).
///   Pure: the same summary and streams always yield the same outcome, with no clock read and no
///   ambient state (FR-019).
/// </summary>
public static class StravaActivityMapper
{
    /// <summary>
    ///   The sport types in scope, fixed by FR-010 and not to be extended, inferred, or defaulted.
    ///   A sport type absent from this table is skipped — never guessed at by name, by pattern, or
    ///   by similarity to a listed type.
    /// </summary>
    /// <remarks>
    ///   <c>EBikeRide</c> and <c>EMountainBikeRide</c> are excluded deliberately: load for a session
    ///   without a heart-rate series is estimated from moving time, and an assisted hour is not an
    ///   unassisted one. Treadmill runs need no entry — Strava has no treadmill sport type, and they
    ///   arrive as <c>Run</c> with <c>trainer: true</c> (FR-010b).
    /// </remarks>
    private static readonly Dictionary<string, ActivityType> InScope = new(StringComparer.Ordinal)
    {
        ["Run"] = ActivityType.Running,
        ["TrailRun"] = ActivityType.Running,
        ["VirtualRun"] = ActivityType.Running,
        ["Ride"] = ActivityType.Cycling,
        ["GravelRide"] = ActivityType.Cycling,
        ["MountainBikeRide"] = ActivityType.Cycling,
        ["VirtualRide"] = ActivityType.Cycling,
    };

    public static MappingOutcome Map(StravaActivitySummary summary, HeartRateSeries? heartRate)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (!InScope.TryGetValue(summary.SportType, out var type))
        {
            return new SkippedActivity(summary.Id, SkipReason.SportOutOfScope);
        }

        try
        {
            // FR-015: the instant together with the athlete's offset, so the session falls on the
            // local day they trained on.
            var startedAt = new DateTimeOffset(summary.StartDate.UtcDateTime)
                .ToOffset(TimeSpan.FromSeconds(summary.UtcOffset));

            return new MappedActivity(
                new TrainingActivity(
                    summary.Id,
                    startedAt,
                    // FR-016: time spent moving, never elapsed time.
                    TimeSpan.FromSeconds(summary.MovingTime),
                    type,
                    heartRate),
                DiscardedSamples: 0);
        }
        catch (ArgumentException)
        {
            // FR-011: the domain's refusal is a fact about one activity, not about the import. One
            // unusable activity must never abort a walk (C65).
            return new SkippedActivity(summary.Id, SkipReason.UnusableByDomain);
        }
    }
}
