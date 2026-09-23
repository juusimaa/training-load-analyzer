from dataclasses import dataclass
from datetime import timedelta
from decimal import Decimal


@dataclass(frozen=True, slots=True)
class HeartRateSample:
    time_from_start: timedelta
    bpm: int


class HeartRateSeries:
    def __init__(self, samples):
        self.samples = samples

    def trimp_points(self, maximum_heart_rate: int) -> Decimal:
        raise NotImplementedError


def heart_rate_zone_weight(bpm: int, maximum_heart_rate: int) -> int:
    raise NotImplementedError
