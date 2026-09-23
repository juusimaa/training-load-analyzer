"""GET /api/dashboard (http-api.md §2; 009 FR-010, FR-013)."""

from datetime import date, datetime, timezone

from fastapi.testclient import TestClient

from tests.api.conftest import refusing_transport
from tests.golden import history_fixture
from tests.parity.histories import activities
from tla.dashboard.reader import read_dashboard
from tla.dashboard.view_json import to_json
from tla.main import create_app
from tla.persistence.activity_store import ActivityStore, ConnectionStore
from tla.persistence.rows import ConnectionRow
from tla.persistence.schema import open_database


def seed_h3f(settings):
    conn = open_database(settings.database_path)
    for a in activities(history_fixture("H3f")):
        ActivityStore(conn).upsert(a, False)
    now = datetime(2026, 9, 18, 4, tzinfo=timezone.utc)
    ConnectionStore(conn).save(ConnectionRow(900001, "test-access-stored", "test-refresh-stored", now, "read,activity:read_all", now))
    conn.close()


def test_the_body_is_the_read_view_as_json(settings, clock):
    seed_h3f(settings)
    client = TestClient(create_app(settings, clock, refusing_transport()))

    response = client.get("/api/dashboard")

    assert response.status_code == 200
    assert response.json() == to_json(read_dashboard(settings.database_path, clock, 190))
    assert response.json()["asOf"] == "2026-09-18"


def test_a_corrupt_row_is_a_200_with_the_unavailable_flag_never_a_5xx(settings, clock):
    seed_h3f(settings)
    conn = open_database(settings.database_path)
    conn.execute("UPDATE activity SET heart_rate_json = 'not json' WHERE heart_rate_json IS NOT NULL")
    conn.commit()

    response = TestClient(create_app(settings, clock, refusing_transport())).get("/api/dashboard")

    assert response.status_code == 200
    assert response.json()["isUnavailable"] is True


def test_the_body_never_contains_a_stored_token(settings, clock):
    seed_h3f(settings)

    text = TestClient(create_app(settings, clock, refusing_transport())).get("/api/dashboard").text

    assert "test-access-stored" not in text and "test-refresh-stored" not in text


def test_an_empty_store_reads_as_no_activities(settings, clock):
    body = TestClient(create_app(settings, clock, refusing_transport())).get("/api/dashboard").json()

    assert (body["hasActivities"], body["isStravaConnected"], body["current"]) == (False, False, None)
