"""SC-003: every sync golden, replayed through the Python sync, matches the reference exactly.

Stored rows, sync state, SyncResult, the message and the request sequence. Two goldens are
deliberately not matched, each asserting the specified behaviour instead (Amendment 1(b)):
`expired-token` (research R10) and `rate-limited-on-streams` (005 FR-017d).
"""

from datetime import UTC, datetime, timedelta

import httpx
import pytest

from tests.fakes import scenario_clock, scenario_transport
from tests.golden import load_sync, sync_names, sync_scenario
from tests.parity.histories import activity as fixture_activity
from tla.dashboard.sync_message import for_status
from tla.persistence.activity_store import ActivityStore, ConnectionStore
from tla.persistence.rows import ConnectionRow, SyncStateRow
from tla.persistence.schema import open_database
from tla.strava.client import StravaApiClient
from tla.strava.oauth import StravaOAuthClient
from tla.sync.activity_sync import ActivitySync
from tla.sync.authorization import StravaAuthorization
from tla.sync.coordinator import SyncStatus
from tla.sync.results import SyncOutcome

DEVIATING = {"expired-token", "rate-limited-on-streams"}
MATCHED = [name for name in sync_names() if name not in DEVIATING]


def instant(text: str | None) -> datetime | None:
    return None if text is None else datetime.fromisoformat(text)


def seeded(tmp_path, scenario: dict):
    conn = open_database(tmp_path / "sync.db")
    c = scenario["connection"]
    ConnectionStore(conn).save(ConnectionRow(c["athleteId"], c["accessToken"], c["refreshToken"], instant(c["expiresAt"]), c["grantedScopes"], instant(c["connectedAt"])))
    for entry in scenario["activities"]:
        ActivityStore(conn).upsert(fixture_activity(entry), entry["heartRateOutstanding"])
    if (state := scenario["syncState"]) is not None:
        ConnectionStore(conn).save_sync_state(SyncStateRow(c["athleteId"], instant(state["resumePoint"]), instant(state["lastSyncStartedAt"]), state["lastOutcome"]))
    return conn


def run(tmp_path, name: str, exchanges=None):
    scenario = sync_scenario(name)
    conn = seeded(tmp_path, scenario)
    clock = scenario_clock(scenario)
    transport, requests = scenario_transport(scenario, exchanges)
    http = httpx.Client(transport=transport)
    authorization = StravaAuthorization(ConnectionStore(conn), StravaOAuthClient(http, "12345", "test-client-secret"), clock)
    result = ActivitySync(conn, StravaApiClient(http, sleep=lambda _: None), authorization, clock).run()
    status = SyncStatus(result=result, finished_at=clock.now_local(),
                        retry_after_local=None if result.retry_after is None else clock.to_local(result.retry_after))
    return conn, result, for_status(status), requests


def stored(conn) -> list[dict]:
    rows = conn.execute("SELECT external_id, heart_rate_outstanding FROM activity").fetchall()
    outstanding = dict(rows)
    activities = ActivityStore(conn).between(datetime(1, 1, 1, tzinfo=UTC), datetime(9999, 1, 1, tzinfo=UTC))
    activities.sort(key=lambda a: (a.started_at, a.external_id))
    return [{
        "id": a.external_id,
        "start": a.started_at,
        "movingSeconds": a.moving_time.total_seconds(),
        "type": a.type.value,
        "heartRate": None if a.heart_rate is None else [[s.time_from_start.total_seconds(), s.bpm] for s in a.heart_rate.samples],
        "heartRateOutstanding": bool(outstanding[a.external_id]),
    } for a in activities]


def expected_activities(golden: dict) -> list[dict]:
    return [{**a, "start": datetime.fromisoformat(a["start"]), "movingSeconds": float(a["movingSeconds"]),
             "heartRate": None if a["heartRate"] is None else [[float(t), bpm] for t, bpm in a["heartRate"]]}
            for a in golden["activities"]]


def as_golden(result) -> dict:
    return {
        "imported": result.imported,
        "updated": result.updated,
        "seriesOutstanding": result.series_outstanding,
        "skipped": [{"id": s.external_id, "reason": s.reason.value} for s in result.skipped],
        "removed": [{"id": r.external_id, "reason": r.reason.value} for r in result.removed],
        "discarded": [{"id": d.external_id, "count": d.count} for d in result.discarded],
        "outcome": result.outcome.value,
        "retryAfter": result.retry_after,
    }


@pytest.mark.parametrize("name", MATCHED)
def test_the_sync_matches_the_reference_exactly(tmp_path, name):
    golden = load_sync(name)

    conn, result, message, requests = run(tmp_path, name)

    assert requests == [(r["method"], r["url"]) for r in golden["requests"]]
    assert as_golden(result) == {**golden["result"], "retryAfter": instant(golden["result"]["retryAfter"])}
    assert message == golden["message"]
    assert stored(conn) == expected_activities(golden)
    state = ConnectionStore(conn).get_sync_state(sync_scenario(name)["connection"]["athleteId"])
    assert (state.resume_point, state.last_sync_started_at, state.last_outcome) == (
        instant(golden["syncState"]["resumePoint"]), instant(golden["syncState"]["lastSyncStartedAt"]), golden["syncState"]["lastOutcome"])


def test_expired_token_renews_first_and_completes(tmp_path):
    # Amendment 1(b)1, research R10: the reference sends the expired token and ends
    # ReconnectionRequired (its golden); the specification renews it and completes.
    golden = load_sync("expired-token")
    scenario = sync_scenario("expired-token")

    conn, result, message, requests = run(tmp_path, "expired-token", scenario["renewal"])

    assert golden["result"]["outcome"] == "ReconnectionRequired"
    assert requests[0] == ("POST", "https://www.strava.com/oauth/token")
    assert [r for r in requests if r[0] == "POST"] == [("POST", "https://www.strava.com/oauth/token")]
    assert (result.outcome, result.imported) == (SyncOutcome.COMPLETED, 1)
    assert message == "1 activities imported."
    held = ConnectionStore(conn).get()
    assert (held.access_token, held.refresh_token) == ("test-access-expired-token-renewed", "test-refresh-expired-token-renewed")


def test_a_limit_reached_on_a_stream_request_is_an_outcome_not_an_escape(tmp_path):
    # Amendment 1(b), 005 FR-017d: the reference lets the limit escape the sync (its golden records
    # it under `escaped`, with two activities never stored). The specification stores every
    # activity, leaves the unfetched series owed, requests nothing further, and reports the limit.
    golden = load_sync("rate-limited-on-streams")

    conn, result, message, requests = run(tmp_path, "rate-limited-on-streams")

    assert golden["escaped"]["type"] == "StravaRateLimitedException" and golden["result"] is None
    assert requests == [(r["method"], r["url"]) for r in golden["requests"]]
    assert (result.outcome, result.imported, result.series_outstanding, result.removed) == (SyncOutcome.RATE_LIMITED, 3, 2, [])
    assert result.retry_after == datetime(2026, 9, 17, 10, 15, tzinfo=UTC)
    assert message == "Rate limited by Strava. Available again at 13:15."
    assert [(a["id"], a["heartRateOutstanding"], a["heartRate"] is not None) for a in stored(conn)] == [
        ("fixture-3951", False, True), ("fixture-3952", True, False), ("fixture-3953", True, False)]
    assert ConnectionStore(conn).get_sync_state(900001).resume_point == datetime(2026, 9, 4, 4, 30, tzinfo=UTC)
