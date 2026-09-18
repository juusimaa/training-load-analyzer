using TrainingLoadAnalyzer.Infrastructure.Sync;

namespace TrainingLoadAnalyzer.Web.Features.Sync;

/// <summary>
///   What the coordinator remembers between circuits (FR-010, FR-012, FR-012a).
/// </summary>
/// <remarks>
///   <see cref="Result"/> is feature 005's <see cref="SyncResult"/> unchanged. It already carries the
///   counts, the outcome and the retry time, and it is documented as carrying no credential in any
///   field or message (005 FR-005, C66) — which is what makes it safe to put on a page. Copying it
///   into a shape of our own would add a mapping to keep correct and gain nothing.
/// </remarks>
public sealed record SyncStatus
{
    /// <summary>Nothing has been attempted yet.</summary>
    public static readonly SyncStatus Never = new();

    /// <summary>US5 scenario 1, and the page-refresh edge case: a reload must be able to see this.</summary>
    public bool IsRunning { get; init; }

    public SyncResult? Result { get; init; }

    /// <summary>
    ///   A sync that could not start at all — <c>SyncAsync</c> throws when no account is connected
    ///   (US5 scenario 5). Never a credential, and never an exception's full text.
    /// </summary>
    public string? Failure { get; init; }

    /// <summary>When the last attempt ended, so "last synced" survives a reload (FR-012).</summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>
    ///   When a rate-limited sync can be retried, already converted into the athlete's own offset.
    /// </summary>
    /// <remarks>
    ///   Converted by the coordinator, which is where the clock and the athlete's time zone live,
    ///   rather than formatted with <c>ToLocalTime()</c> at the point of display. That method reads
    ///   the machine's zone, so "available again at 07:15" would read 04:15 on a UTC build agent and
    ///   on any server not sitting in the athlete's own country — the same class of defect as
    ///   research R9 and R22, and invisible on the developer's laptop.
    /// </remarks>
    public DateTimeOffset? RetryAfterLocal { get; init; }
}
