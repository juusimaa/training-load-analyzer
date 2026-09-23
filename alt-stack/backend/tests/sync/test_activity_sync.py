"""Ported from IncrementalSyncTests.cs and ImportedHistoryTests.cs (005 FR-017 – FR-030; 009 US2 scenarios 3, 5)."""

from datetime import UTC, datetime, timedelta

import pytest

from tests.sync.harness import CLEAN_STREAM, ROOM, SyncHarness, activity, after_of, empty_page, page
from tla.strava.mapper import SkippedActivity, SkipReason
from tla.sync.activity_sync import NotConnected
from tla.sync.results import SyncOutcome


def test_a_first_sync_reads_until_an_empty_page_and_stores_only_running_and_cycling(tmp_path):
    h = SyncHarness(tmp_path)

    result, urls = h.sync([
        page(activity("1", "2026-09-10T04:30:00Z"), activity("2", "2026-09-12T05:00:00Z", "Ride"), activity("3", "2026-09-13T05:00:00Z", "Swim")),
        page(activity("4", "2026-09-14T05:00:00Z", "Walk")),
        empty_page(),
    ])

    assert [u.split("page=")[1].split("&")[0] for u in urls] == ["1", "2", "3"]
    assert after_of(urls) == 0
    assert (result.outcome, result.imported, result.updated) == (SyncOutcome.COMPLETED, 2, 0)
    assert result.skipped == [SkippedActivity("3", SkipReason.SPORT_OUT_OF_SCOPE), SkippedActivity("4", SkipReason.SPORT_OUT_OF_SCOPE)]
    assert h.count() == 2


def test_the_stored_session_keeps_the_athletes_offset_and_the_moving_time(tmp_path):
    h = SyncHarness(tmp_path)

    h.sync([page(activity("1", "2026-09-10T04:30:00Z")), empty_page()])

    [stored] = h.stored()
    assert stored.started_at.utcoffset() == timedelta(hours=3)
    assert stored.moving_time == timedelta(seconds=3120)


def test_streams_are_requested_inside_the_180_day_window_and_not_before_it(tmp_path):
    h = SyncHarness(tmp_path)

    result, urls = h.sync([
        page(activity("old", "2026-03-20T10:00:00Z", hr=True), activity("new", "2026-03-22T10:00:00Z", hr=True)),
        empty_page(),
        ("GET", "activities/new/streams", 200, CLEAN_STREAM, ROOM),
    ])

    assert not any("activities/old/streams" in u for u in urls)
    assert any("activities/new/streams" in u for u in urls)
    assert {a.external_id: a.heart_rate is not None for a in h.stored()} == {"old": False, "new": True}


def test_an_activity_without_heart_rate_makes_no_stream_request(tmp_path):
    h = SyncHarness(tmp_path)

    _, urls = h.sync([page(activity("m", "2026-09-10T04:30:00Z", hr=False, manual=True)), empty_page()])

    assert not any("streams" in u for u in urls)


def test_a_held_series_is_never_fetched_again(tmp_path):
    h = SyncHarness(tmp_path)
    h.sync([page(activity("1", "2026-09-10T04:30:00Z", hr=True)), empty_page(), ("GET", "activities/1/streams", 200, CLEAN_STREAM, ROOM)])

    _, urls = h.sync([page(activity("1", "2026-09-10T04:30:00Z", hr=True)), empty_page()])

    assert not any("streams" in u for u in urls)


def test_a_later_sync_asks_only_from_the_latest_start_minus_seven_days_and_duplicates_nothing(tmp_path):
    h = SyncHarness(tmp_path)
    h.sync([page(activity("1", "2026-09-10T04:30:00Z"), activity("2", "2026-09-12T05:00:00Z", "Ride")), empty_page()])

    result, urls = h.sync([page(activity("1", "2026-09-10T04:30:00Z"), activity("2", "2026-09-12T05:00:00Z", "Ride"), activity("3", "2026-09-15T05:00:00Z", "Ride")), empty_page()])

    assert after_of(urls) == int(datetime(2026, 9, 5, 5, tzinfo=UTC).timestamp())
    assert (result.imported, result.updated) == (1, 2)
    assert h.count() == 3


def test_an_activity_uploaded_late_is_still_picked_up(tmp_path):
    h = SyncHarness(tmp_path)
    h.sync([page(activity("2", "2026-09-12T05:00:00Z", "Ride")), empty_page()])

    result, _ = h.sync([page(activity("9", "2026-09-09T05:00:00Z"), activity("2", "2026-09-12T05:00:00Z", "Ride")), empty_page()])

    assert result.imported == 1


def test_an_outstanding_series_inside_the_window_holds_the_resume_point_behind_itself(tmp_path):
    h = SyncHarness(tmp_path)

    result, _ = h.sync([
        page(activity("owed", "2026-09-01T05:00:00Z", hr=True), activity("later", "2026-09-12T05:00:00Z")),
        empty_page(),
        ("GET", "activities/owed/streams", 404, {}, ROOM),
    ])

    assert result.series_outstanding == 1
    assert h.state().resume_point == datetime(2026, 8, 25, 5, tzinfo=UTC)


def test_an_outstanding_series_that_has_aged_out_of_the_window_no_longer_holds_anything_back(tmp_path):
    h = SyncHarness(tmp_path)
    h.sync([page(activity("owed", "2026-03-22T10:00:00Z", hr=True), activity("later", "2026-09-12T05:00:00Z")), empty_page(),
            ("GET", "activities/owed/streams", 404, {}, ROOM)])
    h.clock.set(h.clock.now_utc() + timedelta(days=10))

    h.sync([page(activity("later", "2026-09-12T05:00:00Z")), empty_page()])

    assert h.state().resume_point == datetime(2026, 9, 5, 5, tzinfo=UTC)


def test_the_resume_point_is_never_before_the_epoch(tmp_path):
    h = SyncHarness(tmp_path)

    h.sync([page(activity("1", "1970-01-03T00:00:00Z")), empty_page()])

    assert h.state().resume_point == datetime(1970, 1, 1, tzinfo=UTC)


def test_an_empty_history_resumes_from_the_epoch_and_records_the_outcome(tmp_path):
    h = SyncHarness(tmp_path)

    result, _ = h.sync([empty_page()])

    assert result.outcome is SyncOutcome.COMPLETED
    state = h.state()
    assert (state.resume_point, state.last_sync_started_at, state.last_outcome) == (datetime(1970, 1, 1, tzinfo=UTC), h.clock.now_utc(), "Completed")


def test_with_no_connection_the_sync_refuses_to_start(tmp_path):
    h = SyncHarness(tmp_path, connected=False)

    with pytest.raises(NotConnected) as refusal:
        h.sync([])

    assert str(refusal.value) == "No Strava account is connected. Authorize one before synchronising."
