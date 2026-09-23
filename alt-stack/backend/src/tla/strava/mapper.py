import enum
from dataclasses import dataclass


class SkipReason(enum.Enum):
    SPORT_OUT_OF_SCOPE = "SportOutOfScope"
    UNUSABLE_BY_DOMAIN = "UnusableByDomain"


@dataclass(frozen=True, slots=True)
class MappedActivity:
    activity: object
    discarded_samples: int


@dataclass(frozen=True, slots=True)
class SkippedActivity:
    external_id: str
    reason: SkipReason


def map_activity(summary, heart_rate): raise NotImplementedError


def to_series(streams): raise NotImplementedError
