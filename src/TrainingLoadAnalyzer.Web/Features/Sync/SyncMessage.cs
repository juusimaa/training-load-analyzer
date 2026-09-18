using System.Globalization;
using TrainingLoadAnalyzer.Infrastructure.Sync;

namespace TrainingLoadAnalyzer.Web.Features.Sync;

/// <summary>
///   What the page says about the last sync (FR-010, US5 scenarios 1-5).
/// </summary>
/// <remarks>
///   A pure function of the status, so every one of US5's scenarios is a plain assertion with no
///   rendering and no Strava. Total by construction: the final arm of the switch means an outcome
///   added to feature 005 later still produces something a person can read, rather than an empty
///   panel (C97, Principle VI).
/// </remarks>
public static class SyncMessage
{
    /// <summary>The prompt that also appears in the empty state, so both routes read alike.</summary>
    public const string ConnectionRequired = "Strava connection required.";

    public static string For(SyncStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (status.IsRunning)
        {
            return "Syncing activities…";
        }

        if (status.Failure is not null)
        {
            return ConnectionRequired;
        }

        if (status.Result is not { } result)
        {
            return string.Empty;
        }

        return result.Outcome switch
        {
            SyncOutcome.Completed when result.Imported > 0 =>
                string.Create(CultureInfo.InvariantCulture, $"{result.Imported} activities imported."),
            SyncOutcome.Completed => "Already up to date.",
            SyncOutcome.RateLimited => RateLimited(status),
            SyncOutcome.Interrupted => "Sync interrupted. Your stored history is unchanged — try again.",
            SyncOutcome.ReconnectionRequired => ConnectionRequired,
            SyncOutcome.Refused => "A sync is already running.",

            // Not a default that shrugs: an outcome nobody anticipated still has to say something,
            // because a blank panel after a click is indistinguishable from a broken page.
            _ => "Sync finished with an outcome this page does not recognise.",
        };
    }

    /// <summary>
    ///   005 FR-035: Strava's own next window boundary, not a fixed delay — so the athlete is told
    ///   when to come back rather than merely to wait. Shown in their local time, because a UTC
    ///   instant is not an answer to "when?".
    /// </summary>
    private static string RateLimited(SyncStatus status) =>
        status.RetryAfterLocal is { } retryAfter
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Rate limited by Strava. Available again at {retryAfter:HH:mm}.")
            : "Rate limited by Strava. Try again shortly.";
}
