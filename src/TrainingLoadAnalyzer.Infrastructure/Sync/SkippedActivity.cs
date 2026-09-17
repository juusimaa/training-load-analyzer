namespace TrainingLoadAnalyzer.Infrastructure.Sync;

/// <summary>Why an activity was not stored. Counted and reported, never discarded silently (FR-038).</summary>
public enum SkipReason
{
    /// <summary>Its sport is outside the analyzer's running-and-cycling scope (FR-009, FR-010).</summary>
    SportOutOfScope,

    /// <summary>The session model refuses it — no moving time, or no start (FR-011).</summary>
    UnusableByDomain,
}

/// <summary>The outcome of mapping one activity.</summary>
public abstract record MappingOutcome;

/// <summary>An activity that became a session.</summary>
public sealed record MappedActivity(
    TrainingLoadAnalyzer.Domain.TrainingActivity Activity,
    int DiscardedSamples) : MappingOutcome;

/// <summary>An activity the sync declined to store, with the identifier it was known by (FR-011).</summary>
public sealed record SkippedActivity(string ExternalId, SkipReason Reason) : MappingOutcome;
