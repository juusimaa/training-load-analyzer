from dataclasses import dataclass
from datetime import datetime


@dataclass(frozen=True, slots=True)
class SyncStatus:
    is_running: bool = False
    result: object = None
    failure: str | None = None
    finished_at: datetime | None = None
    retry_after_local: datetime | None = None


class SyncCoordinator:
    def __init__(self, clock):
        self.status = None

    def run(self, make_sync): raise NotImplementedError
