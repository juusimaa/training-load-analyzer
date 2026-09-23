"""The stored forms. They convert to and from domain values at the boundary and are never returned."""

import json
from dataclasses import dataclass, field
from datetime import UTC, datetime, timedelta, timezone

from tla.domain.activity import ActivityType, TrainingActivity
from tla.domain.heart_rate import HeartRateSample, HeartRateSeries

_EPOCH = datetime(1970, 1, 1, tzinfo=UTC)
_MICROSECOND = timedelta(microseconds=1)


def to_utc_us(instant: datetime) -> int:
    return (instant - _EPOCH) // _MICROSECOND


def from_utc_us(value: int) -> datetime:
    return _EPOCH + timedelta(microseconds=value)


@dataclass(frozen=True, slots=True)
class ActivityRow:
    """Instants as integer UTC microseconds plus an offset in minutes, so SQL can order and range them."""

    provider: str
    external_id: str
    started_at_utc_us: int
    started_at_offset_min: int
    moving_time_us: int
    type: str
    heart_rate_json: str | None
    heart_rate_outstanding: bool

    @classmethod
    def from_domain(cls, activity: TrainingActivity, provider: str, heart_rate_outstanding: bool) -> "ActivityRow":
        series = activity.heart_rate
        return cls(
            provider=provider,
            external_id=activity.external_id,
            started_at_utc_us=to_utc_us(activity.started_at),
            started_at_offset_min=activity.started_at.utcoffset() // timedelta(minutes=1),
            moving_time_us=activity.moving_time // _MICROSECOND,
            type=activity.type.value,
            heart_rate_json=None if series is None else json.dumps(
                [[s.time_from_start // _MICROSECOND, s.bpm] for s in series.samples], separators=(",", ":")),
            heart_rate_outstanding=heart_rate_outstanding,
        )

    def to_domain(self) -> TrainingActivity:
        """Runs the real constructors, so a corrupt row fails here: ValueError, JSONDecodeError, OverflowError."""
        offset = timezone(timedelta(minutes=self.started_at_offset_min))
        series = None
        if self.heart_rate_json is not None:
            series = HeartRateSeries([HeartRateSample(timedelta(microseconds=t), int(bpm)) for t, bpm in json.loads(self.heart_rate_json)])
        return TrainingActivity(
            self.external_id,
            from_utc_us(self.started_at_utc_us).astimezone(offset),
            timedelta(microseconds=self.moving_time_us),
            ActivityType(self.type),
            series,
        )


@dataclass(frozen=True, slots=True)
class ConnectionRow:
    """The standing permission to read one athlete's data. Its repr never prints a token (005 FR-005)."""

    athlete_id: int
    access_token: str = field(repr=False)
    refresh_token: str = field(repr=False)
    expires_at: datetime
    granted_scopes: str
    connected_at: datetime


@dataclass(frozen=True, slots=True)
class SyncStateRow:
    athlete_id: int
    resume_point: datetime
    last_sync_started_at: datetime | None
    last_outcome: str
