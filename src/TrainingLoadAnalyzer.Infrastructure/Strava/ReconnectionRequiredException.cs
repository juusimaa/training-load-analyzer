namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   Strava rejected the stored renewal credential — the athlete revoked access, or it is otherwise
///   invalid. Its own type, because FR-006 requires this to be distinguishable: reported as a generic
///   error it looks like a defect, and reported as success it looks like an athlete who has not
///   trained.
/// </summary>
/// <remarks>Never carries a token in its message (FR-005, C51).</remarks>
public sealed class ReconnectionRequiredException(long athleteId)
    : Exception(
        $"Strava would not renew access for athlete {athleteId}. Reconnect the account to continue "
            + "synchronising; activities already imported are untouched.")
{
    public long AthleteId { get; } = athleteId;
}
