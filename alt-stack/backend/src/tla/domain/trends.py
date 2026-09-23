"""Each ISO week's load compared against the week before it (004)."""

import enum
from collections.abc import Sequence
from dataclasses import dataclass
from datetime import timedelta
from decimal import Decimal

from tla.domain._decimal import LOAD
from tla.domain.aggregation import LoadBasis, WeeklyTrainingLoad
from tla.domain.date_range import DateRange
from tla.domain.iso_week import IsoWeek

# Private on purpose: a test asserting against these would only show the code agrees with itself.
_ABSOLUTE_FLOOR = Decimal("50")
_RELATIVE_THRESHOLD = Decimal("0.15")


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

    @property
    def absolute_change(self) -> Decimal:
        return LOAD.subtract(self.points, self.previous_points)

    @property
    def relative_change(self) -> Decimal | None:
        """None, never zero, when the week before carried no load (004 FR-003, FR-004)."""
        if self.previous_points == 0:
            return None
        return LOAD.divide(self.absolute_change, self.previous_points)

    @property
    def classification(self) -> TrendClassification:
        # Completeness first, then the floor, then the proportion: the order is load-bearing.
        if not self.is_complete:
            return TrendClassification.INDETERMINATE
        if abs(self.absolute_change) < _ABSOLUTE_FLOOR:
            return TrendClassification.STEADY
        relative = self.relative_change
        if relative is not None and abs(relative) < _RELATIVE_THRESHOLD:
            return TrendClassification.STEADY
        return (
            TrendClassification.SIGNIFICANT_INCREASE
            if self.absolute_change > 0
            else TrendClassification.SIGNIFICANT_DECREASE
        )


def calculate_trends(history: Sequence[WeeklyTrainingLoad], date_range: DateRange) -> list[WeeklyLoadTrend]:
    """The trend of every ISO week touching the range (004 FR-028). Weeks are ordered by Monday."""
    if history is None:
        raise ValueError("history must not be None")
    if date_range is None:
        raise ValueError("date_range must not be None")

    first_monday = IsoWeek.for_day(date_range.start).monday
    last_monday = IsoWeek.for_day(date_range.end).monday
    required_first = first_monday - timedelta(days=7)

    if len(history) == 0:
        raise ValueError(
            "The history is empty, so it cannot reach back to the week before the requested "
            f"range, beginning {required_first.isoformat()}."
        )
    if history[0].week.monday > required_first:
        raise ValueError(
            f"The history starts with the week beginning {history[0].week.monday.isoformat()}, "
            f"so the week beginning {required_first.isoformat()} is missing. The range's "
            "first week has nothing to be compared against."
        )
    if history[-1].week.monday < last_monday:
        raise ValueError(
            f"The history ends with the week beginning {history[-1].week.monday.isoformat()}, "
            f"before the requested range's last week, beginning {last_monday.isoformat()}. "
            "The missing weeks cannot be treated as rest without inventing training "
            "history."
        )
    for i in range(1, len(history)):
        if history[i].week.monday != history[i - 1].week.monday + timedelta(days=7):
            raise ValueError(
                f"The history is not continuous: the week at index {i} begins "
                f"{history[i].week.monday.isoformat()}, which does not follow the week "
                f"beginning {history[i - 1].week.monday.isoformat()}."
            )

    trends = []
    for i in range(1, len(history)):
        week = history[i].week
        if week.monday < first_monday or week.monday > last_monday:
            continue
        trends.append(WeeklyLoadTrend(
            week,
            history[i].points,
            history[i - 1].points,
            # From the range alone, never from the clock (004 FR-016).
            week.monday >= date_range.start and week.sunday <= date_range.end,
            _combined_basis(history[i - 1].basis, history[i].basis),
        ))
    return trends


def _combined_basis(previous: LoadBasis, current: LoadBasis) -> LoadBasis:
    """002's rule, the third local copy (004 research R12)."""
    any_measured = previous in (LoadBasis.MEASURED, LoadBasis.MIXED) or current in (LoadBasis.MEASURED, LoadBasis.MIXED)
    any_estimated = previous in (LoadBasis.ESTIMATED, LoadBasis.MIXED) or current in (LoadBasis.ESTIMATED, LoadBasis.MIXED)
    if any_measured and any_estimated:
        return LoadBasis.MIXED
    if any_measured:
        return LoadBasis.MEASURED
    if any_estimated:
        return LoadBasis.ESTIMATED
    return LoadBasis.NONE
