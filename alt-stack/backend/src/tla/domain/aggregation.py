import enum
from dataclasses import dataclass
from datetime import date
from decimal import Decimal

from tla.domain.iso_week import IsoWeek


class LoadBasis(enum.Enum):
    NONE = "None"
    MEASURED = "Measured"
    ESTIMATED = "Estimated"
    MIXED = "Mixed"


@dataclass(frozen=True, slots=True)
class DailyTrainingLoad:
    day: date
    points: Decimal
    activity_count: int
    basis: LoadBasis


@dataclass(frozen=True, slots=True)
class WeeklyTrainingLoad:
    week: IsoWeek
    points: Decimal
    activity_count: int
    basis: LoadBasis


def aggregate_daily(activities, range, maximum_heart_rate):
    raise NotImplementedError


def aggregate_weekly(activities, range, maximum_heart_rate):
    raise NotImplementedError
