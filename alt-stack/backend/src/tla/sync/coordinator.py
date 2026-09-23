"""Runs a manual sync and remembers what happened (006 FR-009, FR-010, FR-012, FR-012a).

One instance per process (held on app.state), which is why the server runs one worker (research
R5): the guard and the last status live in memory, as in the reference's singleton SyncCoordinator.
"""

import logging
import threading
from collections.abc import Callable
from dataclasses import dataclass
from datetime import datetime

from tla.dashboard.sync_message import CONNECTION_REQUIRED
from tla.sync.activity_sync import NotConnected
from tla.sync.results import SyncResult

_log = logging.getLogger("tla.sync")


@dataclass(frozen=True, slots=True)
class SyncStatus:
    """What a newly opened page reads. `failure` is only ever the connection prompt, never an
    exception's text, and nothing here carries a credential."""

    is_running: bool = False
    result: SyncResult | None = None
    failure: str | None = None
    finished_at: datetime | None = None
    retry_after_local: datetime | None = None


class SyncCoordinator:
    def __init__(self, clock):
        self._clock = clock
        # Acquired without blocking, so a second request is refused rather than queued behind the first.
        self._running = threading.Lock()
        self.status = SyncStatus()

    def run(self, sync_once: Callable[[], SyncResult]) -> SyncStatus:
        """Runs `sync_once` (one ActivitySync.run over its own connection and client), or returns the
        running status at once if a sync is already in flight."""
        if not self._running.acquire(blocking=False):
            return self.status

        self.status = SyncStatus(is_running=True)
        try:
            result = sync_once()
            self.status = SyncStatus(
                result=result,
                finished_at=self._clock.now_local(),
                # Converted here, where the athlete's zone is known (research R16).
                retry_after_local=None if result.retry_after is None else self._clock.to_local(result.retry_after),
            )
        except NotConnected:
            _log.warning("A sync was requested with no Strava account connected.")
            self.status = SyncStatus(failure=CONNECTION_REQUIRED, finished_at=self._clock.now_local())
        finally:
            self._running.release()

        return self.status
