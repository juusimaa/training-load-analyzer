"""One Strava activity into a session, or the reason it could not be (005 FR-009 – FR-020).

Pure, ported from StravaActivityMapper.cs and SkippedActivity.cs.
"""

import enum
from dataclasses import dataclass
from datetime import timedelta, timezone

from tla.domain.activity import ActivityType, TrainingActivity
from tla.domain.heart_rate import HeartRateSample, HeartRateSeries
from tla.strava.shapes import StravaActivitySummary, StravaStreamSet

# Fixed by 005 FR-010, never extended, inferred or defaulted. EBikeRide is excluded deliberately.
_IN_SCOPE = {
    "Run": ActivityType.RUNNING,
    "TrailRun": ActivityType.RUNNING,
    "VirtualRun": ActivityType.RUNNING,
    "Ride": ActivityType.CYCLING,
    "GravelRide": ActivityType.CYCLING,
    "MountainBikeRide": ActivityType.CYCLING,
    "VirtualRide": ActivityType.CYCLING,
}

# The range the session model accepts, mirrored so it is never offered a sample it would refuse.
_MINIMUM_PLAUSIBLE_BPM = 20
_MAXIMUM_PLAUSIBLE_BPM = 250

# The offsets a start can carry, as the reference's DateTimeOffset allows them.
_MAXIMUM_OFFSET_SECONDS = 14 * 3600


class SkipReason(enum.Enum):
    SPORT_OUT_OF_SCOPE = "SportOutOfScope"
    UNUSABLE_BY_DOMAIN = "UnusableByDomain"


@dataclass(frozen=True, slots=True)
class MappedActivity:
    activity: TrainingActivity
    discarded_samples: int


@dataclass(frozen=True, slots=True)
class SkippedActivity:
    external_id: str
    reason: SkipReason


def map_activity(summary: StravaActivitySummary, heart_rate: HeartRateSeries | None) -> MappedActivity | SkippedActivity:
    activity_type = _IN_SCOPE.get(summary.sport_type)
    if activity_type is None:
        return SkippedActivity(summary.external_id, SkipReason.SPORT_OUT_OF_SCOPE)

    try:
        return MappedActivity(
            TrainingActivity(
                summary.external_id,
                _started_at(summary),
                # 005 FR-016: time spent moving, never elapsed time.
                timedelta(seconds=summary.moving_time),
                activity_type,
                heart_rate,
            ),
            discarded_samples=0,
        )
    except ValueError:
        # One unusable activity is a fact about that activity, never a reason to abort the walk.
        return SkippedActivity(summary.external_id, SkipReason.UNUSABLE_BY_DOMAIN)


def _started_at(summary: StravaActivitySummary):
    """The instant with the athlete's offset (005 FR-015). Offsets must be whole minutes within ±14 h."""
    if summary.start_date is None:
        return None
    offset = summary.utc_offset
    if offset % 60 != 0 or abs(offset) > _MAXIMUM_OFFSET_SECONDS:
        raise ValueError("Offset must be specified in whole minutes, within 14 hours of UTC.")
    return summary.start_date.astimezone(timezone(timedelta(seconds=offset)))


def to_series(streams: StravaStreamSet) -> tuple[HeartRateSeries | None, int]:
    """The heart-rate series, with implausible and repeated-time samples discarded and counted
    (005 FR-017f, FR-017g). None when fewer than two survive."""
    if streams.heartrate is None or streams.time is None:
        return None, 0

    times, beats = streams.time, streams.heartrate
    usable = min(len(times), len(beats))
    samples: list[HeartRateSample] = []
    discarded = 0
    last_time: timedelta | None = None

    for i in range(usable):
        if not _MINIMUM_PLAUSIBLE_BPM <= beats[i] <= _MAXIMUM_PLAUSIBLE_BPM:
            discarded += 1
            continue
        at = timedelta(seconds=times[i])
        if last_time is not None and at <= last_time:
            discarded += 1
            continue
        samples.append(HeartRateSample(at, beats[i]))
        last_time = at

    discarded += len(beats) - usable
    return (None if len(samples) < 2 else HeartRateSeries(samples)), discarded
