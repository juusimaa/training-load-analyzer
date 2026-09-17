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
    /// <summary>
    ///   The plausible range the session model accepts. Mirrored here rather than read from the
    ///   domain, because the domain deliberately exposes it only as a refusal — this is
    ///   Infrastructure declining to offer what it knows would be refused (FR-017f).
    /// </summary>
    private const int MinimumPlausibleBpm = 20;

    private const int MaximumPlausibleBpm = 250;

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

    /// <summary>
    ///   Turns Strava's two streams into a heart-rate series, discarding samples the session model
    ///   would refuse and reporting how many (FR-017f, FR-017g, C71).
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The discarding happens <strong>here</strong>, on the raw arrays, before
    ///     <see cref="HeartRateSeries"/> is constructed. No domain invariant moves: Infrastructure
    ///     simply never hands the domain a sample it would refuse. Recorded heart-rate data
    ///     routinely contains dropouts — most often in the opening seconds, before a chest strap
    ///     reads reliably — and treating a whole session's evidence as worthless because of them
    ///     would send nearly every real session to estimated load (research R21).
    ///   </para>
    ///   <para>
    ///     Returns null when fewer than two samples survive. Two is the fewest from which any load
    ///     can be computed, so no threshold beyond it is invented; the activity then falls to
    ///     FR-017d and carries estimated load.
    ///   </para>
    /// </remarks>
    public static HeartRateSeries? ToSeries(StravaStreamSet streams, out int discardedSamples)
    {
        ArgumentNullException.ThrowIfNull(streams);

        discardedSamples = 0;

        // A missing stream is an absent key, not a null entry (research R20).
        if (streams.Heartrate is null || streams.Time is null)
        {
            return null;
        }

        var times = streams.Time.Data;
        var beats = streams.Heartrate.Data;
        var usable = Math.Min(times.Count, beats.Count);
        var samples = new List<HeartRateSample>(usable);
        var lastTime = TimeSpan.MinValue;

        for (var i = 0; i < usable; i++)
        {
            if (beats[i] is < MinimumPlausibleBpm or > MaximumPlausibleBpm)
            {
                discardedSamples++;

                continue;
            }

            var at = TimeSpan.FromSeconds(times[i]);

            // The series requires strictly ascending times. A repeated instant is a recording
            // artefact on the same terms as an implausible reading.
            if (at <= lastTime)
            {
                discardedSamples++;

                continue;
            }

            samples.Add(new HeartRateSample(at, beats[i]));
            lastTime = at;
        }

        discardedSamples += beats.Count - usable;

        return samples.Count < 2 ? null : new HeartRateSeries(samples);
    }
}
