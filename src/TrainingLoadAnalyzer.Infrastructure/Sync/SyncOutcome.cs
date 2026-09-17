namespace TrainingLoadAnalyzer.Infrastructure.Sync;

/// <summary>How a sync ended. Never an escaping exception, never a silent empty result (FR-039).</summary>
public enum SyncOutcome
{
    /// <summary>
    ///   Every page was read. The <strong>only</strong> outcome under which reconciliation may
    ///   delete anything (FR-031c, C61) — every other value means the set of activities seen is
    ///   partial, and deleting against a partial set removes training the athlete actually did.
    /// </summary>
    Completed,

    /// <summary>A read limit was reached; <c>RetryAfter</c> names when to try again (FR-035).</summary>
    RateLimited,

    /// <summary>A connection or server failure survived the bounded retry (FR-037).</summary>
    Interrupted,

    /// <summary>Strava rejected the credential; the athlete must reconnect (FR-006).</summary>
    ReconnectionRequired,

    /// <summary>A sync was already running for this connection (FR-040).</summary>
    Refused,
}
