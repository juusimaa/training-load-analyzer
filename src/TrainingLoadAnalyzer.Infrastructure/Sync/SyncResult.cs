namespace TrainingLoadAnalyzer.Infrastructure.Sync;

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

    /// <summary>Series owed but not retrieved (FR-017d).</summary>
    public int SeriesOutstanding { get; init; }

    /// <summary>Where samples were dropped, and how many — the repair made visible (FR-017g).</summary>
    public IReadOnlyList<DiscardedSamples> Discarded { get; init; } = [];

    public SyncOutcome Outcome { get; init; } = SyncOutcome.Completed;
}
