"""GET /api/sync/status and POST /api/sync (http-api.md §3; research R9)."""

from fastapi import APIRouter, Request

from tla.dashboard.sync_message import for_status
from tla.sync.coordinator import SyncStatus
from tla.sync.results import SyncOutcome

router = APIRouter()


@router.get("/api/sync/status")
def get_status(request: Request) -> dict:
    return status_view(request.app.state.coordinator.status)


@router.post("/api/sync")
def post_sync(request: Request) -> dict:
    """Blocks until the sync ends. A second request while one runs gets the running status at once."""
    state = request.app.state
    return status_view(state.coordinator.run(state.sync_once))


def status_view(status: SyncStatus) -> dict:
    finished = status.finished_at
    return {
        "isRunning": status.is_running,
        "message": for_status(status),
        # Both ways the athlete can need to connect: never having, and a credential Strava rejected.
        "needsConnection": status.failure is not None
        or (status.result is not None and status.result.outcome is SyncOutcome.RECONNECTION_REQUIRED),
        "lastChecked": None if status.is_running or finished is None
        else f"{finished.year:04d}-{finished.month:02d}-{finished.day:02d} {finished.hour:02d}:{finished.minute:02d}",
    }
