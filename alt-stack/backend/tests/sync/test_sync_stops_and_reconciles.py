"""Ported from RateLimitTests.cs (sync level) and ReconciliationTests.cs (005 FR-031 – FR-039; 009 US2 scenarios 4, 6)."""

from datetime import UTC, datetime

import httpx

from tests.sync.harness import CLEAN_STREAM, EXHAUSTED, ROOM, SyncHarness, activity, empty_page, page
from tla.sync.results import DiscardedSamples, RemovalReason, RemovedActivity, SyncOutcome

TWO = (activity("1", "2026-09-10T04:30:00Z"), activity("2", "2026-09-12T05:00:00Z", "Ride"))


def test_a_429_on_page_two_keeps_page_one_and_reports_the_next_quarter_hour(tmp_path):
    h = SyncHarness(tmp_path)

    result, _ = h.sync([page(*TWO), ("GET", "athlete/activities", 429, {"message": "Rate Limit Exceeded"}, EXHAUSTED)])

    assert (result.outcome, result.imported, result.removed) == (SyncOutcome.RATE_LIMITED, 2, [])
    assert result.retry_after == datetime(2026, 9, 17, 10, 15, tzinfo=UTC)
    assert h.count() == 2
    assert h.state().last_outcome == "RateLimited"
    assert h.state().resume_point == datetime(2026, 9, 5, 5, tzinfo=UTC)


def test_an_exhausted_budget_on_a_successful_page_stops_the_same_way(tmp_path):
    h = SyncHarness(tmp_path)

    result, _ = h.sync([page(*TWO, headers=EXHAUSTED)])

    assert (result.outcome, result.imported) == (SyncOutcome.RATE_LIMITED, 0)
    assert result.retry_after == datetime(2026, 9, 17, 10, 15, tzinfo=UTC)


def test_a_401_asks_for_reconnection_and_leaves_stored_activities_alone(tmp_path):
    h = SyncHarness(tmp_path)
    h.sync([page(*TWO), empty_page()])

    result, _ = h.sync([("GET", "athlete/activities", 401, {"message": "Authorization Error"}, None)])

    assert (result.outcome, result.removed) == (SyncOutcome.RECONNECTION_REQUIRED, [])
    assert h.count() == 2


def test_a_transport_failure_after_the_retries_is_interrupted_and_changes_nothing(tmp_path):
    h = SyncHarness(tmp_path)
    h.sync([page(*TWO), empty_page()])
    dropped = httpx.ConnectError("dropped")

    result, urls = h.sync([("GET", "athlete/activities", 0, dropped, None)] * 3)

    assert (result.outcome, result.imported, result.removed) == (SyncOutcome.INTERRUPTED, 0, [])
    assert len(urls) == 3 and h.count() == 2


def test_a_span_read_to_completion_removes_what_is_no_longer_there(tmp_path):
    h = SyncHarness(tmp_path)
    h.sync([page(*TWO, activity("3", "2026-09-14T05:00:00Z")), empty_page()])

    result, _ = h.sync([page(TWO[0], activity("3", "2026-09-14T05:00:00Z")), empty_page()])

    assert result.removed == [RemovedActivity("2", RemovalReason.DELETED_AT_SOURCE)]
    assert sorted(a.external_id for a in h.stored()) == ["1", "3"]


def test_a_span_read_only_partially_removes_nothing(tmp_path):
    h = SyncHarness(tmp_path)
    h.sync([page(*TWO, activity("3", "2026-09-14T05:00:00Z")), empty_page()])

    result, _ = h.sync([page(TWO[0]), ("GET", "athlete/activities", 429, {}, EXHAUSTED)])

    assert result.removed == [] and h.count() == 3


def test_a_session_before_the_span_is_never_a_candidate_for_removal(tmp_path):
    h = SyncHarness(tmp_path)
    h.sync([page(activity("old", "2026-06-01T05:00:00Z"), *TWO), empty_page()])

    result, _ = h.sync([page(*TWO), empty_page()])

    assert result.removed == [] and h.count() == 3


def test_a_stored_ride_whose_sport_type_changed_to_ebike_is_skipped_and_removed(tmp_path):
    h = SyncHarness(tmp_path)
    h.sync([page(*TWO), empty_page()])

    result, _ = h.sync([page(TWO[0], activity("2", "2026-09-12T05:00:00Z", "EBikeRide")), empty_page()])

    assert result.removed == [RemovedActivity("2", RemovalReason.DELETED_AT_SOURCE)]
    assert [a.external_id for a in h.stored()] == ["1"]


def test_discarded_samples_are_reported_per_activity(tmp_path):
    h = SyncHarness(tmp_path)

    result, _ = h.sync([
        page(activity("1", "2026-09-10T04:30:00Z", hr=True)), empty_page(),
        ("GET", "activities/1/streams", 200, {"time": {"data": [0, 1, 2, 2]}, "heartrate": {"data": [0, 140, 150, 160]}}, ROOM),
    ])

    assert result.discarded == [DiscardedSamples("1", 2)]


def test_a_rate_limit_on_a_stream_request_stores_the_activity_with_its_series_owed(tmp_path):
    # 005 FR-017d over the reference (Amendment 1(b)): the activity is kept, its series owed, the
    # sync not failed; no further stream is requested (FR-034), and the outcome reports the limit.
    h = SyncHarness(tmp_path)

    result, urls = h.sync([
        page(activity("1", "2026-09-10T04:30:00Z", hr=True), activity("2", "2026-09-11T04:30:00Z", hr=True), activity("3", "2026-09-12T04:30:00Z", hr=True)),
        empty_page(),
        ("GET", "activities/1/streams", 200, CLEAN_STREAM, ROOM),
        ("GET", "activities/2/streams", 429, {}, EXHAUSTED),
    ])

    assert (result.outcome, result.imported, result.series_outstanding) == (SyncOutcome.RATE_LIMITED, 3, 2)
    assert result.retry_after == datetime(2026, 9, 17, 10, 15, tzinfo=UTC)
    assert not any("activities/3/streams" in u for u in urls)
    assert result.removed == []
    assert h.state().resume_point == datetime(2026, 9, 4, 4, 30, tzinfo=UTC)


def test_no_outcome_carries_a_credential(tmp_path):
    h = SyncHarness(tmp_path)

    result, _ = h.sync([("GET", "athlete/activities", 401, {}, None)])

    assert "test-access-1" not in repr(result) and "test-refresh-1" not in repr(result)
