"""The application's remaining read budget, from Strava's X-ReadRateLimit-* headers (005 FR-034, FR-035)."""

from collections.abc import Mapping
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta

_LIMIT_HEADER = "x-readratelimit-limit"
_USAGE_HEADER = "x-readratelimit-usage"


@dataclass(frozen=True, slots=True)
class RateLimitStatus:
    short_term_usage: int
    short_term_limit: int
    daily_usage: int
    daily_limit: int

    @property
    def is_exhausted(self) -> bool:
        return self.short_term_usage >= self.short_term_limit or self._daily_exhausted

    @property
    def _daily_exhausted(self) -> bool:
        return self.daily_usage >= self.daily_limit

    @classmethod
    def from_headers(cls, headers: Mapping[str, str]) -> "RateLimitStatus | None":
        """The budget, or None when the headers are absent or unreadable. Names match in any case."""
        short_limit, day_limit = _pair(headers, _LIMIT_HEADER)
        short_usage, day_usage = _pair(headers, _USAGE_HEADER)
        if short_limit is None or short_usage is None:
            return None
        return cls(short_usage, short_limit, day_usage or 0, day_limit or 0)

    def retry_after(self, now: datetime) -> datetime:
        """The next window boundary: a quarter hour, or UTC midnight once the daily budget is spent."""
        utc = now.astimezone(UTC)
        if self._daily_exhausted:
            return datetime(utc.year, utc.month, utc.day, tzinfo=UTC) + timedelta(days=1)
        quarters = utc.minute // 15 + 1
        return datetime(utc.year, utc.month, utc.day, utc.hour, tzinfo=UTC) + timedelta(minutes=15 * quarters)


def _pair(headers: Mapping[str, str], name: str) -> tuple[int | None, int | None]:
    value = next((v for k, v in headers.items() if k.lower() == name), None)
    if value is None:
        return None, None
    parts = [part.strip() for part in value.split(",")]
    return (_int(parts[0]) if parts else None, _int(parts[1]) if len(parts) > 1 else None)


def _int(text: str) -> int | None:
    try:
        return int(text)
    except ValueError:
        return None
