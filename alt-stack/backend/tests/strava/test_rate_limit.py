"""Ported from the header and retry-time cases of RateLimitTests.cs (005 FR-034, FR-035)."""

from datetime import UTC, datetime

import pytest

from tla.strava.rate_limit import RateLimitStatus


def headers(limit: str, usage: str) -> dict[str, str]:
    return {"X-ReadRateLimit-Limit": limit, "X-ReadRateLimit-Usage": usage}


def z(text: str) -> datetime:
    return datetime.fromisoformat(text)


def test_the_read_rate_limit_headers_are_parsed_into_both_windows():
    status = RateLimitStatus.from_headers(headers("100,1000", "98,412"))

    assert status == RateLimitStatus(short_term_usage=98, short_term_limit=100, daily_usage=412, daily_limit=1000)


def test_header_names_are_matched_without_regard_to_case():
    status = RateLimitStatus.from_headers({"x-readratelimit-limit": "100,1000", "X-READRATELIMIT-USAGE": "5,50"})

    assert status.short_term_usage == 5


def test_a_response_without_the_headers_yields_no_status():
    assert RateLimitStatus.from_headers({}) is None


def test_malformed_headers_yield_no_status():
    assert RateLimitStatus.from_headers(headers("lots", "some")) is None


@pytest.mark.parametrize(
    ("now", "expected"),
    [
        ("2026-09-17T10:07:33+00:00", "2026-09-17T10:15:00+00:00"),
        ("2026-09-17T10:15:00+00:00", "2026-09-17T10:30:00+00:00"),
        ("2026-09-17T10:46:01+00:00", "2026-09-17T11:00:00+00:00"),
        ("2026-09-17T23:52:00+00:00", "2026-09-18T00:00:00+00:00"),
    ],
)
def test_the_short_term_retry_time_is_the_next_quarter_hour_boundary(now, expected):
    status = RateLimitStatus.from_headers(headers("100,1000", "100,412"))

    assert status.retry_after(z(now)) == z(expected)


def test_the_daily_retry_time_is_the_next_midnight_utc():
    status = RateLimitStatus.from_headers(headers("100,1000", "5,1000"))

    assert status.retry_after(datetime(2026, 9, 17, 10, 7, 33, tzinfo=UTC)) == datetime(2026, 9, 18, tzinfo=UTC)


def test_a_budget_with_room_left_is_not_exhausted():
    assert not RateLimitStatus.from_headers(headers("100,1000", "98,412")).is_exhausted
    assert RateLimitStatus.from_headers(headers("100,1000", "100,412")).is_exhausted
    assert RateLimitStatus.from_headers(headers("100,1000", "5,1000")).is_exhausted
