"""GET /api/sync/status and POST /api/sync (http-api.md §3; research R9; 009 US2 scenario 7)."""

import threading
import time
from datetime import datetime

import httpx
from fastapi.testclient import TestClient

from tests.api.conftest import refusing_transport
from tests.fakes import scenario_clock, scenario_transport
from tests.golden import sync_scenario
from tests.parity.histories import activity as fixture_activity
from tla.main import create_app
from tla.persistence.activity_store import ActivityStore, ConnectionStore
from tla.persistence.rows import ConnectionRow, SyncStateRow
from tla.persistence.schema import open_database


def seed(settings, name: str):
    scenario = sync_scenario(name)
    conn = open_database(settings.database_path)
    c = scenario["connection"]
    ConnectionStore(conn).save(ConnectionRow(c["athleteId"], c["accessToken"], c["refreshToken"], datetime.fromisoformat(c["expiresAt"]), c["grantedScopes"], datetime.fromisoformat(c["connectedAt"])))
    for entry in scenario["activities"]:
        ActivityStore(conn).upsert(fixture_activity(entry), entry["heartRateOutstanding"])
    if (state := scenario["syncState"]) is not None:
        started = state["lastSyncStartedAt"]
        ConnectionStore(conn).save_sync_state(SyncStateRow(
            c["athleteId"], datetime.fromisoformat(state["resumePoint"]), started and datetime.fromisoformat(started), state["lastOutcome"]))
    conn.close()
    return scenario


def test_before_any_sync_the_status_has_nothing_to_say(settings, clock):
    body = TestClient(create_app(settings, clock, refusing_transport())).get("/api/sync/status").json()

    assert body == {"isRunning": False, "message": "", "needsConnection": False, "lastChecked": None}


def test_a_sync_with_no_connection_asks_to_connect(settings, clock):
    client = TestClient(create_app(settings, clock, refusing_transport()))

    body = client.post("/api/sync").json()

    assert body == {"isRunning": False, "message": "Strava connection required.", "needsConnection": True, "lastChecked": "2026-09-18 07:00"}
    assert client.get("/api/sync/status").json() == body


def test_a_first_import_blocks_until_done_and_reports_the_count(settings):
    scenario = seed(settings, "first-import")
    transport, _ = scenario_transport(scenario)
    client = TestClient(create_app(settings, scenario_clock(scenario), transport))

    body = client.post("/api/sync").json()

    assert body == {"isRunning": False, "message": "4 activities imported.", "needsConnection": False, "lastChecked": "2026-09-17 13:07"}


def test_a_rejected_credential_asks_to_connect(settings):
    scenario = seed(settings, "unauthorized-401")
    transport, _ = scenario_transport(scenario)

    body = TestClient(create_app(settings, scenario_clock(scenario), transport)).post("/api/sync").json()

    assert (body["needsConnection"], body["message"]) == (True, "Strava connection required.")


def test_a_second_sync_while_one_runs_returns_the_running_status_at_once(settings):
    scenario = seed(settings, "first-import")
    replay, requests = scenario_transport(scenario)
    gate = threading.Event()

    def gated(request: httpx.Request) -> httpx.Response:
        gate.wait(timeout=10)
        return replay.handle_request(request)

    client = TestClient(create_app(settings, scenario_clock(scenario), httpx.MockTransport(gated)))
    first = threading.Thread(target=client.post, args=("/api/sync",), daemon=True)
    first.start()
    deadline = time.monotonic() + 5
    while not client.get("/api/sync/status").json()["isRunning"]:
        assert time.monotonic() < deadline, "the first sync never started"
        time.sleep(0.01)

    second = client.post("/api/sync")

    assert (second.status_code, second.json()) == (200, {"isRunning": True, "message": "Syncing activities…", "needsConnection": False, "lastChecked": None})
    gate.set()
    first.join(timeout=10)
    assert sum("page=1&" in url for _, url in requests) == 1


def test_no_sync_response_contains_a_token(settings):
    scenario = seed(settings, "first-import")
    transport, _ = scenario_transport(scenario)
    client = TestClient(create_app(settings, scenario_clock(scenario), transport))

    text = client.post("/api/sync").text + client.get("/api/sync/status").text

    assert scenario["connection"]["accessToken"] not in text and scenario["connection"]["refreshToken"] not in text
