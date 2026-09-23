import enum
from dataclasses import dataclass
from datetime import datetime, timedelta
from decimal import Decimal

from tla.domain.heart_rate import HeartRateSeries


class ActivityType(enum.Enum):
    RUNNING = "Running"
    CYCLING = "Cycling"


class LoadProvenance(enum.Enum):
    MEASURED = "Measured"
    ESTIMATED = "Estimated"


@dataclass(frozen=True, slots=True)
class TrainingLoad:
    points: Decimal
    provenance: LoadProvenance


@dataclass(frozen=True, slots=True)
class TrainingActivity:
    external_id: str
    started_at: datetime
    moving_time: timedelta
    type: ActivityType
    heart_rate: HeartRateSeries | None = None


def training_load(activity: TrainingActivity, maximum_heart_rate: int) -> TrainingLoad:
    raise NotImplementedError
