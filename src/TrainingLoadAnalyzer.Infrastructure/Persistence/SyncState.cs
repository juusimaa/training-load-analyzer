namespace TrainingLoadAnalyzer.Infrastructure.Persistence;

/// <summary>
///   What the next sync needs in order to be incremental (FR-025). One row, alongside the connection.
/// </summary>
public sealed class SyncState
{
    /// <summary>The connection this state belongs to.</summary>
    public long AthleteId { get; set; }

    /// <summary>
    ///   Where the next sync starts reading (FR-027, FR-028). Recomputed at the end of every sync as
    ///   <c>min(latest stored activity start, earliest activity with an outstanding series) − 7 days</c>,
    ///   and advanced only over work that is durably stored (FR-029).
    /// </summary>
    public long ResumePointUtcTicks { get; set; }

    public long? LastSyncStartedAtUtcTicks { get; set; }

    /// <summary>
    ///   How the last sync ended, so a sync that stopped early can be told apart from one that
    ///   completed — by the athlete and by anything reading this row later (FR-025, FR-039).
    /// </summary>
    public string LastOutcome { get; set; } = string.Empty;
}
