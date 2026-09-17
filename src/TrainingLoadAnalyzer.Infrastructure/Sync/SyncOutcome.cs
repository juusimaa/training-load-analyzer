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
}
