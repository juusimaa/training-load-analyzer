from dataclasses import dataclass
from datetime import date

from tla.domain.aggregation import LoadBasis


@dataclass(frozen=True, slots=True)
class DailyTrainingMetrics:
    day: date
    fitness: float
    fatigue: float
    is_reliable: bool
    fitness_basis: LoadBasis
    fatigue_basis: LoadBasis


def calculate_metrics(history, date_range):
    raise NotImplementedError
