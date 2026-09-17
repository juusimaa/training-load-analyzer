namespace TrainingLoadAnalyzer.Infrastructure.Sync;

/// <summary>Why a stored session was reconciled away (FR-031, FR-031e).</summary>
public enum RemovalReason
{
    /// <summary>It is no longer in Strava's history over a span read to completion.</summary>
    DeletedAtSource,

    /// <summary>Its sport type changed to one outside the analyzer's scope (FR-031e).</summary>
    SportNowOutOfScope,
}

/// <summary>A session reconciliation removed, reported so a disappearance is never silent (FR-031d).</summary>
public sealed record RemovedActivity(string ExternalId, RemovalReason Reason);

/// <summary>Implausible heart-rate samples dropped while mapping one activity (FR-017g).</summary>
public sealed record DiscardedSamples(string ExternalId, int Count);

/// <summary>
///   What one sync did (FR-038). Returned to the caller, never stored, and carrying no credential in
///   any field or message (FR-005, C66).
/// </summary>
public sealed record SyncResult
{
    public int Imported { get; init; }

    public int Updated { get; init; }

    /// <summary>Each with its identifier and reason. Counted, never discarded silently (FR-011).</summary>
    public IReadOnlyList<SkippedActivity> Skipped { get; init; } = [];

    /// <summary>
    ///   Sessions reconciled away. Non-empty <strong>only</strong> when <see cref="Outcome"/> is
    ///   <see cref="SyncOutcome.Completed"/> (FR-031c, C61).
    /// </summary>
    public IReadOnlyList<RemovedActivity> Removed { get; init; } = [];

    /// <summary>Series owed but not retrieved (FR-017d).</summary>
    public int SeriesOutstanding { get; init; }

    /// <summary>Where samples were dropped, and how many — the repair made visible (FR-017g).</summary>
    public IReadOnlyList<DiscardedSamples> Discarded { get; init; } = [];

    public SyncOutcome Outcome { get; init; } = SyncOutcome.Completed;
}
