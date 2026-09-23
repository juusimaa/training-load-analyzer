"""What the page says about the last sync: SyncMessage.cs, verbatim (006 FR-010, US5)."""

from typing import TYPE_CHECKING

from tla.sync.results import SyncOutcome

if TYPE_CHECKING:
    # Only for the annotation: the coordinator imports this module for its prompt.
    from tla.sync.coordinator import SyncStatus

# Also what the empty state prompts, so both routes read alike.
CONNECTION_REQUIRED = "Strava connection required."


def for_status(status: "SyncStatus") -> str:
    if status.is_running:
        return "Syncing activities…"
    if status.failure is not None:
        return CONNECTION_REQUIRED
    result = status.result
    if result is None:
        return ""

    match result.outcome:
        case SyncOutcome.COMPLETED if result.imported > 0:
            return f"{result.imported} activities imported."
        case SyncOutcome.COMPLETED:
            return "Already up to date."
        case SyncOutcome.RATE_LIMITED:
            retry = status.retry_after_local
            if retry is None:
                return "Rate limited by Strava. Try again shortly."
            return f"Rate limited by Strava. Available again at {retry.hour:02d}:{retry.minute:02d}."
        case SyncOutcome.INTERRUPTED:
            return "Sync interrupted. Your stored history is unchanged — try again."
        case SyncOutcome.RECONNECTION_REQUIRED:
            return CONNECTION_REQUIRED
        case SyncOutcome.REFUSED:
            return "A sync is already running."
        case _:
            # An outcome nobody anticipated still says something: a blank panel reads as broken.
            return "Sync finished with an outcome this page does not recognise."
