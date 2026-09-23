"""The measured heart-rate record of one session (001 FR-007, FR-009, FR-021)."""

from dataclasses import dataclass
from datetime import timedelta
from decimal import Decimal

from tla.domain._decimal import LOAD, minutes

_MINIMUM_PLAUSIBLE_BPM = 20
_MAXIMUM_PLAUSIBLE_BPM = 250


@dataclass(frozen=True, slots=True)
class HeartRateSample:
    """One measurement, timed from the session's start. Validates nothing; the series does."""

    time_from_start: timedelta
    bpm: int


@dataclass(frozen=True, slots=True)
class HeartRateSeries:
    """Non-empty, strictly ascending in time, every sample 20–250 bpm. Copied on construction."""

    samples: tuple[HeartRateSample, ...]

    def __post_init__(self) -> None:
        if not self.samples:
            raise ValueError(
                "A heart-rate series must contain at least one sample; a session either has "
                "heart-rate data or has none."
            )

        # Copied before validation, so the samples checked are exactly the ones stored.
        copied = tuple(self.samples)

        for i in range(1, len(copied)):
            if copied[i].time_from_start <= copied[i - 1].time_from_start:
                raise ValueError(
                    "Heart-rate sample times must be in ascending order; the sample at index "
                    f"{i} ({_timespan(copied[i].time_from_start)}) does not follow the one "
                    f"before it ({_timespan(copied[i - 1].time_from_start)})."
                )

        for sample in copied:
            if not _MINIMUM_PLAUSIBLE_BPM <= sample.bpm <= _MAXIMUM_PLAUSIBLE_BPM:
                raise ValueError(
                    f"A heart-rate sample of {sample.bpm} bpm is outside the plausible range of "
                    f"{_MINIMUM_PLAUSIBLE_BPM}-{_MAXIMUM_PLAUSIBLE_BPM} bpm."
                )

        object.__setattr__(self, "samples", copied)

    def trimp_points(self, maximum_heart_rate: int) -> Decimal:
        """Edwards TRIMP: each sample's zone weight times the minutes until the next sample."""
        points = Decimal(0)
        for earlier, later in zip(self.samples, self.samples[1:]):
            weight = heart_rate_zone_weight(earlier.bpm, maximum_heart_rate)
            points = LOAD.add(points, LOAD.multiply(weight, minutes(later.time_from_start - earlier.time_from_start)))
        return points


def heart_rate_zone_weight(bpm: int, maximum_heart_rate: int) -> int:
    """The Edwards weight 0–5, by integer cross-multiplication. Lower bound inclusive."""
    scaled = bpm * 100
    for weight, percent in ((5, 90), (4, 80), (3, 70), (2, 60), (1, 50)):
        if scaled >= percent * maximum_heart_rate:
            return weight
    return 0


def _timespan(span: timedelta) -> str:
    """A span as .NET prints a TimeSpan ("c"): [-][d.]hh:mm:ss[.fffffff], for the reference's wording."""
    sign = "-" if span < timedelta(0) else ""
    span = abs(span)
    ticks = span // timedelta(microseconds=1) * 10
    days, rest = divmod(ticks, 864_000_000_000)
    hours, rest = divmod(rest, 36_000_000_000)
    mins, rest = divmod(rest, 600_000_000)
    seconds, fraction = divmod(rest, 10_000_000)
    text = f"{sign}{f'{days}.' if days else ''}{hours:02d}:{mins:02d}:{seconds:02d}"
    return text + (f".{fraction:07d}" if fraction else "")
