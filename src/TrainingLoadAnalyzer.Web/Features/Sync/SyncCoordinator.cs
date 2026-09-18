using TrainingLoadAnalyzer.Infrastructure.Sync;

namespace TrainingLoadAnalyzer.Web.Features.Sync;

/// <summary>
///   Runs a manual sync, and remembers what happened (FR-009, FR-010, FR-012).
/// </summary>
/// <remarks>
///   <para>
///     <strong>A singleton, and that is the whole point.</strong> The specification needs state that
///     outlives a circuit: the edge case "what if the sync is in progress when the page refreshes?"
///     is asking about a <em>new</em> circuit, FR-012 wants the state restored on reload, and
///     FR-010's counts must survive long enough to be read.
///   </para>
///   <para>
///     Restoring feature 005's FR-040 guarantee is a by-product. <c>StravaActivitySync</c> holds its
///     one-at-a-time semaphore in an instance field, and a probe showed it cannot be registered as a
///     singleton at all — its constructor takes a scoped <c>ActivityStore</c>. As a scoped service,
///     two browser tabs get two semaphores and the guard stops guarding. Feature 005 is deliberately
///     left unmodified: inside a single scope its own guard still behaves exactly as its tests
///     assert. The trigger for revisiting is a second caller of <c>StravaActivitySync</c> outside
///     this class (research R13, C98).
///   </para>
/// </remarks>
public sealed class SyncCoordinator(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<SyncCoordinator> logger)
{
    /// <summary>
    ///   Zero timeout, so a second request is <em>refused</em> rather than queued behind the first.
    ///   Queuing would make the button feel broken and could start a second walk minutes later,
    ///   against an athlete who has since navigated away.
    /// </summary>
    private readonly SemaphoreSlim running = new(1, 1);

    /// <summary>What a newly-opened page reads. Never null (C93).</summary>
    public SyncStatus Status { get; private set; } = SyncStatus.Never;

    public async Task<SyncStatus> RunAsync(CancellationToken cancellationToken)
    {
        if (!await running.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return Status;
        }

        Status = new SyncStatus { IsRunning = true };

        try
        {
            // A scope of its own, created and disposed per run, so the sync's ImportDbContext is
            // never a circuit-lifetime one (research R12, C96).
            await using var scope = scopes.CreateAsyncScope();
            var sync = scope.ServiceProvider.GetRequiredService<StravaActivitySync>();

            var result = await sync.SyncAsync(cancellationToken);

            Status = new SyncStatus
            {
                Result = result,
                FinishedAt = clock.GetLocalNow(),
                RetryAfterLocal = InAthletesOffset(result.RetryAfter),
            };
        }
        catch (InvalidOperationException notConnected)
        {
            // The one failure SyncAsync signals by throwing rather than by an outcome: there is no
            // account to sync. Displayed rather than propagated, and logged rather than swallowed
            // (Principle VI, C95). The message is ours, not the exception's - 005 C66 keeps
            // credentials out of SyncResult, and this keeps them out of here too.
            logger.LogWarning(notConnected, "A sync was requested with no Strava account connected.");

            Status = new SyncStatus
            {
                Failure = SyncMessage.ConnectionRequired,
                FinishedAt = clock.GetLocalNow(),
            };
        }
        finally
        {
            running.Release();
        }

        return Status;
    }

    /// <summary>
    ///   Feature 005 reports the retry time against the clock's own offset. Converting it here,
    ///   where the athlete's time zone is known, is what keeps the displayed time correct on a
    ///   server that is not sitting in their country — <c>ToLocalTime()</c> at the point of display
    ///   would read the machine's zone instead (research R22).
    /// </summary>
    private DateTimeOffset? InAthletesOffset(DateTimeOffset? instant) =>
        instant is null
            ? null
            : TimeZoneInfo.ConvertTime(instant.Value, clock.LocalTimeZone);
}
