import enum
from dataclasses import dataclass


class SyncOutcome(enum.Enum):
    COMPLETED = "Completed"
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
