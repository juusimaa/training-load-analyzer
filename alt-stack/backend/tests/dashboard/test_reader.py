"""Ported from DashboardReaderTests.cs (006 C87; data-model.md §5)."""

import logging
from datetime import date, datetime, timedelta, timezone

import pytest

from tests.fakes import FixedClock
from tla.dashboard.reader import read_dashboard
from tla.dashboard.view_builder import build_dashboard_view
from tla.domain.activity import ActivityType, TrainingActivity
from tla.persistence.activity_store import ActivityStore, ConnectionStore
from tla.persistence.rows import ConnectionRow
from tla.persistence.schema import open_database

PLUS_THREE = timezone(timedelta(hours=3))


def seed(path, *days: int, connected: bool = False):
    conn = open_database(path)
    activities = [TrainingActivity(f"a{d}", datetime(2026, 9, d, 7, tzinfo=PLUS_THREE), timedelta(minutes=60), ActivityType.RUNNING) for d in days]
    for a in activities:
        ActivityStore(conn).upsert(a, False)
    if connected:
        now = datetime(2026, 9, 18, 4, tzinfo=timezone.utc)
        ConnectionStore(conn).save(ConnectionRow(900001, "test-access-secret", "test-refresh-secret", now, "read,activity:read_all", now))
    return conn, activities


def test_the_view_is_the_builder_over_the_stored_history_on_the_athletes_day(tmp_path):
    _, activities = seed(tmp_path / "t.db", 14, 16, 18)

    view = read_dashboard(tmp_path / "t.db", FixedClock(), 190)

    assert view == build_dashboard_view(activities, date(2026, 9, 18), 190, False)


def test_a_held_connection_is_reported(tmp_path):
    seed(tmp_path / "t.db", 18, connected=True)

    assert read_dashboard(tmp_path / "t.db", FixedClock(), 190).is_strava_connected


@pytest.mark.parametrize(
    ("column", "value", "failure"),
    [("heart_rate_json", "[[0,300],[60000000,150]]", "ValueError"),
     ("heart_rate_json", "not json", "JSONDecodeError"),
     ("heart_rate_json", "[[1e30,150],[2e30,150]]", "OverflowError")],
)
def test_a_corrupt_row_makes_the_view_unavailable_and_logs_why(tmp_path, caplog, column, value, failure):
    conn, _ = seed(tmp_path / "t.db", 18, connected=True)
    conn.execute(f"UPDATE activity SET {column} = ?", (value,))
    conn.commit()

    with caplog.at_level(logging.WARNING):
        view = read_dashboard(tmp_path / "t.db", FixedClock(), 190)

    assert view.is_unavailable
    assert (view.metrics, view.recent, view.current) == ([], [], None)
    assert (view.as_of, view.iso_week, view.maximum_heart_rate, view.is_strava_connected) == (date(2026, 9, 18), "2026-W38", 190, True)
    [record] = [r for r in caplog.records if r.levelno >= logging.WARNING]
    assert failure in record.getMessage()
    assert "test-access-secret" not in record.getMessage()
