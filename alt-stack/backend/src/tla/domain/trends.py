import enum
from dataclasses import dataclass
from decimal import Decimal

from tla.domain.aggregation import LoadBasis
from tla.domain.iso_week import IsoWeek


class TrendClassification(enum.Enum):
    INDETERMINATE = "Indeterminate"
    STEADY = "Steady"
    SIGNIFICANT_INCREASE = "SignificantIncrease"
    SIGNIFICANT_DECREASE = "SignificantDecrease"


@dataclass(frozen=True, slots=True)
class WeeklyLoadTrend:
    week: IsoWeek
    points: Decimal
    previous_points: Decimal
    is_complete: bool
    basis: LoadBasis


def calculate_trends(history, date_range):
    raise NotImplementedError
