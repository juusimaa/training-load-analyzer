"""A single completed training session and its training load (001 FR-001 – FR-024)."""

import enum
from dataclasses import dataclass
from datetime import datetime, timedelta
from decimal import Decimal

from tla.domain._decimal import LOAD, minutes
from tla.domain.heart_rate import HeartRateSeries

# Fixed by 001's Assumptions: a session without heart-rate data is weighted as zone 2.
_DEFAULT_INTENSITY_WEIGHT = Decimal(2)


class ActivityType(enum.Enum):
    """001 FR-005: nothing else is accepted. The value is the name the reference prints."""

    RUNNING = "Running"
    CYCLING = "Cycling"


class LoadProvenance(enum.Enum):
    MEASURED = "Measured"
    ESTIMATED = "Estimated"


@dataclass(frozen=True, slots=True)
class TrainingLoad:
    """The points and how they were arrived at, inseparable (001 SC-007)."""

    points: Decimal
    provenance: LoadProvenance


@dataclass(frozen=True, slots=True)
class TrainingActivity:
    """Frozen, so a correction is a new record. Every rule is checked before the value exists."""

    external_id: str
    started_at: datetime
    moving_time: timedelta
    type: ActivityType
    heart_rate: HeartRateSeries | None = None

    def __post_init__(self) -> None:
        # Stored verbatim: 001 FR-002 forbids interpreting or trimming the identifier.
        if not isinstance(self.external_id, str) or not self.external_id.strip():
            raise ValueError("A session must carry an external identifier.")

        # A start without its offset is as incomplete as no start at all (001 FR-003).
        if not isinstance(self.started_at, datetime) or self.started_at.utcoffset() is None:
            raise ValueError("A session must have a start time.")

        if not self.moving_time > timedelta(0):
            raise ValueError("A session's moving time must be a positive span of time.")

        if not isinstance(self.type, ActivityType):
            raise ValueError("A session must be classified as either running or cycling.")


def training_load(activity: TrainingActivity, maximum_heart_rate: int) -> TrainingLoad:
    """Measured from the series when there is one (FR-009), otherwise estimated from moving time (FR-013)."""
    if activity.heart_rate is None:
        return TrainingLoad(LOAD.multiply(minutes(activity.moving_time), _DEFAULT_INTENSITY_WEIGHT), LoadProvenance.ESTIMATED)

    if maximum_heart_rate <= 0:
        raise ValueError("A measured training load needs a positive maximum heart rate to classify zones against.")

    return TrainingLoad(activity.heart_rate.trimp_points(maximum_heart_rate), LoadProvenance.MEASURED)
