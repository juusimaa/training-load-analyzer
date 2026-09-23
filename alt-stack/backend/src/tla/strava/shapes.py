"""Strava's own shapes. They stay inside the integration and are never stored in this form (005 FR-018)."""

from dataclasses import dataclass, field
from datetime import datetime


@dataclass(frozen=True, slots=True)
class StravaActivitySummary:
    """One activity as the list endpoint describes it.

    `start_date_local` is deliberately never read: Strava renders local wall-clock time with a
    trailing Z. Nor is `type`, which is lossy; `sport_type` is what the mapper branches on.
    """

    external_id: str
    sport_type: str
    start_date: datetime | None
    utc_offset: int
    moving_time: int
    has_heartrate: bool
    manual: bool
    private: bool
    trainer: bool

    @classmethod
    def from_json(cls, body: dict) -> "StravaActivitySummary":
        raw_id = body.get("id")
        start = body.get("start_date")
        return cls(
            # A JSON number becomes its verbatim decimal text; the analyzer keeps ids opaque.
            external_id=str(raw_id) if raw_id is not None else "",
            sport_type=body.get("sport_type") or "",
            start_date=datetime.fromisoformat(start) if start else None,
            # A non-integer offset is truncated toward zero, as the reference's converter casts it.
            utc_offset=int(body.get("utc_offset") or 0),
            moving_time=int(body.get("moving_time") or 0),
            has_heartrate=bool(body.get("has_heartrate", False)),
            manual=bool(body.get("manual", False)),
            private=bool(body.get("private", False)),
            trainer=bool(body.get("trainer", False)),
        )


@dataclass(frozen=True, slots=True)
class StravaStreamSet:
    """A missing stream is an absent key, not a null entry (005 research R20)."""

    time: list[int] | None
    heartrate: list[int] | None

    @classmethod
    def from_json(cls, body: dict) -> "StravaStreamSet":
        def data(key: str) -> list[int] | None:
            stream = body.get(key)
            return None if stream is None else list(stream.get("data") or [])

        return cls(time=data("time"), heartrate=data("heartrate"))


@dataclass(frozen=True, slots=True)
class StravaTokens:
    """A token response, from an exchange or a renewal. The tokens never appear in its repr."""

    access_token: str = field(repr=False)
    refresh_token: str = field(repr=False)
    expires_at: int
    scope: str | None
    athlete_id: int | None

    @classmethod
    def from_json(cls, body: dict) -> "StravaTokens":
        athlete = body.get("athlete")
        return cls(
            access_token=body.get("access_token") or "",
            refresh_token=body.get("refresh_token") or "",
            expires_at=int(body.get("expires_at") or 0),
            scope=body.get("scope"),
            athlete_id=int(athlete["id"]) if athlete and athlete.get("id") is not None else None,
        )
