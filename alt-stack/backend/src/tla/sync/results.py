"""What one sync did (005 FR-038). Returned to the caller, never stored, carrying no credential."""

import enum
from dataclasses import dataclass, field
from datetime import datetime

from tla.strava.mapper import SkippedActivity


class SyncOutcome(enum.Enum):
    """How a sync ended. Never an escaping exception, never a silent empty result (005 FR-039)."""

    COMPLETED = "Completed"  # the only outcome under which reconciliation may remove anything
    RATE_LIMITED = "RateLimited"
    INTERRUPTED = "Interrupted"
    RECONNECTION_REQUIRED = "ReconnectionRequired"
    REFUSED = "Refused"


class RemovalReason(enum.Enum):
    DELETED_AT_SOURCE = "DeletedAtSource"
    SPORT_NOW_OUT_OF_SCOPE = "SportNowOutOfScope"


@dataclass(frozen=True, slots=True)
class RemovedActivity:
    external_id: str
    reason: RemovalReason


@dataclass(frozen=True, slots=True)
class DiscardedSamples:
    external_id: str
    count: int


@dataclass(frozen=True, slots=True)
class SyncResult:
    imported: int = 0
    updated: int = 0
    skipped: list[SkippedActivity] = field(default_factory=list)
    removed: list[RemovedActivity] = field(default_factory=list)
    series_outstanding: int = 0
    discarded: list[DiscardedSamples] = field(default_factory=list)
    outcome: SyncOutcome = SyncOutcome.COMPLETED
    retry_after: datetime | None = None
