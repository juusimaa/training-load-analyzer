"""Training load totalled by calendar day and by ISO week (002)."""

import enum
from collections.abc import Iterable
from dataclasses import dataclass
from datetime import date
from decimal import Decimal

from tla.domain._decimal import LOAD
from tla.domain.activity import LoadProvenance, TrainingActivity, training_load
from tla.domain.date_range import DateRange
from tla.domain.iso_week import IsoWeek

_DAYS_PER_WEEK = 7


class LoadBasis(enum.Enum):
    """How far a total can be trusted, given where its loads came from (002 FR-014)."""

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


def aggregate_daily(activities: Iterable[TrainingActivity], date_range: DateRange, maximum_heart_rate: int) -> list[DailyTrainingLoad]:
    """The total load per calendar day of the range, gap-free and ascending (002 FR-001, FR-005)."""
    if activities is None:
        raise ValueError("activities must not be None")
    if date_range is None:
        raise ValueError("date_range must not be None")

    totals: dict[date, tuple[Decimal, int, bool, bool]] = {}
    for activity in activities:
        day = _day_of(activity)
        load = training_load(activity, maximum_heart_rate)
        points, count, any_measured, any_estimated = totals.get(day, (Decimal(0), 0, False, False))
        totals[day] = (
            LOAD.add(points, load.points),
            count + 1,
            any_measured or load.provenance is LoadProvenance.MEASURED,
            any_estimated or load.provenance is LoadProvenance.ESTIMATED,
        )

    # Walking the range rather than grouping the activities is what makes the series gap-free.
    series = []
    for day in date_range.days():
        points, count, any_measured, any_estimated = totals.get(day, (Decimal(0), 0, False, False))
        series.append(DailyTrainingLoad(day, points, count, _combine(any_measured, any_estimated)))
    return series


def aggregate_weekly(activities: Iterable[TrainingActivity], date_range: DateRange, maximum_heart_rate: int) -> list[WeeklyTrainingLoad]:
    """The total load per ISO week the range touches, each week whole (002 FR-002, FR-013, FR-017)."""
    if activities is None:
        raise ValueError("activities must not be None")
    if date_range is None:
        raise ValueError("date_range must not be None")

    extended = DateRange(IsoWeek.for_day(date_range.start).monday, IsoWeek.for_day(date_range.end).sunday)
    days = aggregate_daily(activities, extended, maximum_heart_rate)

    # A week IS the sum of its seven days, by construction.
    series = []
    for first in range(0, len(days), _DAYS_PER_WEEK):
        week_days = days[first:first + _DAYS_PER_WEEK]
        points = Decimal(0)
        for day in week_days:
            points = LOAD.add(points, day.points)
        series.append(WeeklyTrainingLoad(
            IsoWeek.for_day(week_days[0].day),
            points,
            sum(day.activity_count for day in week_days),
            _combine(
                any(day.basis in (LoadBasis.MEASURED, LoadBasis.MIXED) for day in week_days),
                any(day.basis in (LoadBasis.ESTIMATED, LoadBasis.MIXED) for day in week_days),
            ),
        ))
    return series


def _day_of(activity: TrainingActivity) -> date:
    """The athlete's local day at the activity's own recorded offset (002 FR-003, FR-004)."""
    return activity.started_at.date()


def _combine(any_measured: bool, any_estimated: bool) -> LoadBasis:
    """One estimate among measurements makes the whole total mixed (002 FR-014, FR-015).
    Kept here rather than shared: 004 research R12 declined extracting it."""
    if any_measured and any_estimated:
        return LoadBasis.MIXED
    if any_measured:
        return LoadBasis.MEASURED
    if any_estimated:
        return LoadBasis.ESTIMATED
    return LoadBasis.NONE
