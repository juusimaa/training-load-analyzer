from dataclasses import dataclass, field
from datetime import datetime


class ActivityRow:
    @classmethod
    def from_domain(cls, activity, provider, heart_rate_outstanding): raise NotImplementedError


@dataclass(frozen=True, slots=True)
class ConnectionRow:
    athlete_id: int
    access_token: str
    refresh_token: str
    expires_at: datetime
    granted_scopes: str
    connected_at: datetime


@dataclass(frozen=True, slots=True)
class SyncStateRow:
    athlete_id: int
    resume_point: datetime
    last_sync_started_at: datetime | None
    last_outcome: str
