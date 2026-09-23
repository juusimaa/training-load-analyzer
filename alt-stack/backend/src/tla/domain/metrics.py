"""The day-by-day Fitness, Fatigue and Form series (003)."""

import math
from collections.abc import Sequence
from dataclasses import dataclass
from datetime import date, timedelta

from tla.domain.aggregation import DailyTrainingLoad, LoadBasis
from tla.domain.date_range import DateRange

# Written as the formulas rather than transcribed literals (003 FR-005, FR-029a).
_ALPHA_FITNESS = 1 - math.exp(-1 / 42)
_ALPHA_FATIGUE = 1 - math.exp(-1 / 7)

# Days of history behind a figure before the zero seed has decayed out of it (003 FR-014).
_WARM_UP_DAYS = 42

# How far back each figure's basis is read: its own time constant (003 FR-019).
_FITNESS_WINDOW_DAYS = 42
_FATIGUE_WINDOW_DAYS = 7


@dataclass(frozen=True, slots=True)
class DailyTrainingMetrics:
    day: date
    fitness: float
    fatigue: float
    is_reliable: bool
    fitness_basis: LoadBasis
    fatigue_basis: LoadBasis

    @property
    def form(self) -> float:
        """Computed on every read and never stored, so it cannot disagree with its parts (003 FR-003)."""
        return self.fitness - self.fatigue

    @property
    def form_basis(self) -> LoadBasis:
        """Always fitness's: fatigue's window lies inside fitness's (003 FR-019b)."""
        return self.fitness_basis


def calculate_metrics(history: Sequence[DailyTrainingLoad], date_range: DateRange) -> list[DailyTrainingMetrics]:
    """The figures of every day in the range, accumulated from the first day of the history (003 FR-008)."""
    if history is None:
        raise ValueError("history must not be None")
    if date_range is None:
        raise ValueError("date_range must not be None")

    if len(history) == 0:
        raise ValueError(
            "The history is empty, so it cannot reach back to the requested range's first "
            f"day of {date_range.start.isoformat()}."
        )
    if history[0].day > date_range.start:
        raise ValueError(
            f"The history starts on {history[0].day.isoformat()}, after the requested range "
            f"starts on {date_range.start.isoformat()}. The metrics of the range's first day "
            "need every day before it."
        )
    if history[-1].day < date_range.end:
        raise ValueError(
            f"The history ends on {history[-1].day.isoformat()}, before the requested range "
            f"ends on {date_range.end.isoformat()}. The missing days cannot be treated as "
            "rest without inventing training history."
        )
    # One check catches a gap, a duplicate and an inversion (003 FR-023).
    for i in range(1, len(history)):
        if history[i].day != history[i - 1].day + timedelta(days=1):
            raise ValueError(
                f"The history is not continuous: the day at index {i} is "
                f"{history[i].day.isoformat()}, which does not follow "
                f"{history[i - 1].day.isoformat()}."
            )

    # Both figures start at zero on the notional day before the history (003 FR-012).
    fitness = 0.0
    fatigue = 0.0
    reliable_from = history[0].day + timedelta(days=_WARM_UP_DAYS)
    series = []

    for i, day in enumerate(history):
        load = float(day.points)
        fitness += (load - fitness) * _ALPHA_FITNESS
        fatigue += (load - fatigue) * _ALPHA_FATIGUE

        if date_range.start <= day.day <= date_range.end:
            series.append(DailyTrainingMetrics(
                day.day,
                fitness,
                fatigue,
                day.day >= reliable_from,
                _basis_over(history, i, _FITNESS_WINDOW_DAYS),
                _basis_over(history, i, _FATIGUE_WINDOW_DAYS),
            ))

    return series


def _basis_over(history: Sequence[DailyTrainingLoad], last: int, window_days: int) -> LoadBasis:
    """The combined basis of the window ending at `last`, truncated at the history's start (003 FR-019)."""
    window = history[max(0, last - window_days + 1):last + 1]
    return _combine(
        any(d.basis in (LoadBasis.MEASURED, LoadBasis.MIXED) for d in window),
        any(d.basis in (LoadBasis.ESTIMATED, LoadBasis.MIXED) for d in window),
    )


def _combine(any_measured: bool, any_estimated: bool) -> LoadBasis:
    """002's rule, reused unchanged and kept local (004 research R12)."""
    if any_measured and any_estimated:
        return LoadBasis.MIXED
    if any_measured:
        return LoadBasis.MEASURED
    if any_estimated:
        return LoadBasis.ESTIMATED
    return LoadBasis.NONE
