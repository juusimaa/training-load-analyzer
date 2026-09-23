"""Ported from the client cases of RateLimitTests.cs, ImportedHistoryTests.cs and StravaJsonQuirksTests.cs (005 FR-034 – FR-037)."""

from datetime import UTC, datetime

import httpx
import pytest

from tests.fakes import json_transport
from tla.strava.client import StravaApiClient
from tla.strava.errors import StravaRateLimited, StravaRequestFailed

EXHAUSTED = {"X-ReadRateLimit-Limit": "100,1000", "X-ReadRateLimit-Usage": "100,412"}
ROOM = {"X-ReadRateLimit-Limit": "100,1000", "X-ReadRateLimit-Usage": "5,50"}
ONE = [{"id": 11000000001, "sport_type": "Run", "start_date": "2026-09-10T04:30:00Z", "utc_offset": 10800, "moving_time": 3120}]
AFTER = datetime(2026, 9, 5, 5, 0, tzinfo=UTC)


def client(routes):
    transport, requests = json_transport(routes)
    delays: list[float] = []
    return StravaApiClient(httpx.Client(transport=transport), sleep=delays.append), requests, delays


def test_the_activities_list_is_requested_with_after_page_and_the_page_size():
    api, requests, _ = client([("GET", "athlete/activities", 200, ONE, ROOM)])

    [activity] = api.list_activities("test-access-1", AFTER, 2)

    assert str(requests[0].url) == "https://www.strava.com/api/v3/athlete/activities?after=1788584400&page=2&per_page=200"
    assert requests[0].headers["Authorization"] == "Bearer test-access-1"
    assert activity.external_id == "11000000001"


def test_the_streams_are_requested_at_full_resolution_keyed_by_type():
    api, requests, _ = client([("GET", "streams", 200, {"time": {"data": [0]}, "heartrate": {"data": [150]}}, ROOM)])

    streams = api.get_streams("test-access-1", "fixture-1001")

    assert str(requests[0].url) == "https://www.strava.com/api/v3/activities/fixture-1001/streams?keys=time,heartrate&key_by_type=true"
    assert streams.heartrate == [150]


def test_a_server_error_is_retried_and_succeeds_on_the_third_attempt_with_backoff():
    api, requests, delays = client([
        ("GET", "athlete/activities", 503, {}, None),
        ("GET", "athlete/activities", 503, {}, None),
        ("GET", "athlete/activities", 200, ONE, ROOM),
    ])

    assert len(api.list_activities("t", AFTER, 1)) == 1
    assert len(requests) == 3
    assert delays == [0.01, 0.02]


def test_three_server_errors_fail_with_the_last_status():
    api, requests, _ = client([("GET", "athlete/activities", 503, {}, None)] * 3)

    with pytest.raises(StravaRequestFailed) as failure:
        api.list_activities("t", AFTER, 1)

    assert failure.value.status == 503
    assert str(failure.value) == "Strava answered 503 ServiceUnavailable."
    assert len(requests) == 3


def test_a_transport_error_is_retried_like_a_server_error():
    dropped = httpx.ConnectError("dropped")
    api, requests, _ = client([("GET", "athlete/activities", 0, dropped, None), ("GET", "athlete/activities", 200, ONE, ROOM)])

    assert len(api.list_activities("t", AFTER, 1)) == 1
    assert len(requests) == 2


def test_a_transport_error_on_every_attempt_fails_with_no_status():
    dropped = httpx.ConnectError("dropped")
    api, _, _ = client([("GET", "athlete/activities", 0, dropped, None)] * 3)

    with pytest.raises(StravaRequestFailed) as failure:
        api.list_activities("t", AFTER, 1)

    assert failure.value.status is None
    assert str(failure.value) == "The connection to Strava failed and did not recover."


@pytest.mark.parametrize("status", [400, 401, 403])
def test_a_client_error_is_not_retried(status):
    api, requests, _ = client([("GET", "athlete/activities", status, {}, None)])

    with pytest.raises(StravaRequestFailed) as failure:
        api.list_activities("t", AFTER, 1)

    assert failure.value.status == status
    assert len(requests) == 1


def test_a_429_is_a_rate_limit_and_is_never_retried():
    api, requests, _ = client([("GET", "athlete/activities", 429, {"message": "Rate Limit Exceeded"}, EXHAUSTED)])

    with pytest.raises(StravaRateLimited) as limited:
        api.list_activities("t", AFTER, 1)

    assert limited.value.status.is_exhausted
    assert str(limited.value) == "Strava's read limit has been reached; the sync stopped rather than exceeding it."
    assert len(requests) == 1


def test_a_successful_page_whose_budget_is_exhausted_is_discarded_and_stops_the_sync():
    api, _, _ = client([("GET", "athlete/activities", 200, ONE, EXHAUSTED)])

    with pytest.raises(StravaRateLimited):
        api.list_activities("t", AFTER, 1)


def test_a_404_on_streams_means_no_streams():
    api, _, _ = client([("GET", "streams", 404, {"message": "Record Not Found"}, ROOM)])

    assert api.get_streams("t", "fixture-1") is None


def test_a_404_on_streams_whose_budget_is_exhausted_still_stops_the_sync():
    api, _, _ = client([("GET", "streams", 404, {}, EXHAUSTED)])

    with pytest.raises(StravaRateLimited):
        api.get_streams("t", "fixture-1")


def test_the_bearer_token_never_appears_in_a_failure_message():
    api, _, _ = client([("GET", "athlete/activities", 401, {}, None)])

    with pytest.raises(StravaRequestFailed) as failure:
        api.list_activities("test-access-secret", AFTER, 1)

    assert "test-access-secret" not in str(failure.value) and "test-access-secret" not in repr(failure.value)
