"""Ported from SyncCoordinatorTests.cs (006 FR-009, FR-010, FR-012, FR-012a; 009 US2 scenario 7)."""

import json
import threading
import time
from datetime import UTC, datetime, timedelta, timezone

import httpx

from tests.fakes import FixedClock
from tests.sync.harness import EXHAUSTED, ROOM, SyncHarness, activity
from tla.persistence.activity_store import ConnectionStore
from tla.persistence.schema import open_database
from tla.strava.client import StravaApiClient
from tla.strava.oauth import StravaOAuthClient
from tla.sync.activity_sync import ActivitySync
from tla.sync.authorization import StravaAuthorization
from tla.sync.coordinator import SyncCoordinator, SyncStatus
from tla.sync.results import SyncOutcome

NOW = datetime(2026, 9, 17, 10, 7, 33, tzinfo=UTC)
PAGE = [activity("1", "2026-09-10T04:30:00Z"), activity("2", "2026-09-12T05:00:00Z", "Ride")]


def factory(h: SyncHarness, handler):
    def make_sync() -> ActivitySync:
        # One connection per unit of work, opened on the thread that runs the sync, as in production.
        conn = open_database(h.path)
        http = httpx.Client(transport=httpx.MockTransport(handler))
        auth = StravaAuthorization(ConnectionStore(conn), StravaOAuthClient(http, "12345", "s"), h.clock)
        return ActivitySync(conn, StravaApiClient(http, sleep=lambda _: None), auth, h.clock)
    return make_sync


def serving(pages, headers=ROOM, gate: threading.Event | None = None, requests: list | None = None):
    remaining = list(pages)

    def handle(request: httpx.Request) -> httpx.Response:
        if requests is not None:
            requests.append(str(request.url))
        if gate is not None:
            gate.wait(timeout=10)
        body = remaining.pop(0) if remaining else []
        return httpx.Response(200, headers=headers, content=json.dumps(body).encode())
    return handle


def test_before_any_sync_the_status_is_never(tmp_path):
    coordinator = SyncCoordinator(FixedClock(NOW))

    assert coordinator.status == SyncStatus()
    assert (coordinator.status.is_running, coordinator.status.result) == (False, None)


def test_a_sync_imports_what_strava_returns_and_records_when_it_finished(tmp_path):
    h = SyncHarness(tmp_path, clock=FixedClock(NOW, timezone(timedelta(hours=3))))
    coordinator = SyncCoordinator(h.clock)

    status = coordinator.run(factory(h, serving([PAGE, []])))

    assert (status.result.outcome, status.result.imported) == (SyncOutcome.COMPLETED, 2)
    assert status.finished_at == datetime(2026, 9, 17, 13, 7, 33, tzinfo=timezone(timedelta(hours=3)))
    assert coordinator.status == status


def test_with_no_connection_the_status_reports_a_failure_rather_than_raising(tmp_path):
    h = SyncHarness(tmp_path, connected=False)
    requests: list[str] = []
    coordinator = SyncCoordinator(h.clock)

    status = coordinator.run(factory(h, serving([], requests=requests)))

    assert (status.failure, status.is_running, status.result) == ("Strava connection required.", False, None)
    assert status.finished_at is not None
    assert requests == []


def test_a_rate_limited_sync_records_the_retry_time_in_the_athletes_zone(tmp_path):
    h = SyncHarness(tmp_path, clock=FixedClock(NOW, timezone(timedelta(hours=3))))

    status = SyncCoordinator(h.clock).run(factory(h, serving([PAGE], headers=EXHAUSTED)))

    assert status.result.outcome is SyncOutcome.RATE_LIMITED
    assert status.retry_after_local == datetime(2026, 9, 17, 13, 15, tzinfo=timezone(timedelta(hours=3)))


def test_a_second_run_while_one_is_in_flight_returns_the_running_status_and_one_walk_runs(tmp_path):
    h = SyncHarness(tmp_path)
    gate = threading.Event()
    requests: list[str] = []
    coordinator = SyncCoordinator(h.clock)
    make_sync = factory(h, serving([PAGE, []], gate=gate, requests=requests))

    first = threading.Thread(target=coordinator.run, args=(make_sync,), daemon=True)
    first.start()
    deadline = time.monotonic() + 5
    while not requests:
        assert time.monotonic() < deadline, "the first sync never reached Strava"
        time.sleep(0.005)

    refused = coordinator.run(make_sync)

    assert refused.is_running
    gate.set()
    first.join(timeout=10)
    assert coordinator.status.result.outcome is SyncOutcome.COMPLETED
    assert sum("page=1&" in url for url in requests) == 1
