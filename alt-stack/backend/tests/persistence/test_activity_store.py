"""Ported from PersistenceRoundTripTests.cs (005 FR-017b, FR-022 – FR-024, FR-030; data-model.md §3)."""

import json
import sqlite3
from datetime import UTC, datetime, timedelta, timezone

import pytest

from tla.domain.activity import ActivityType, TrainingActivity
from tla.domain.heart_rate import HeartRateSample, HeartRateSeries
from tla.persistence.activity_store import ActivityStore
from tla.persistence.rows import ActivityRow
from tla.persistence.schema import open_database

EPOCH = datetime(1970, 1, 1, tzinfo=UTC)


def us(instant: datetime) -> int:
    return (instant - EPOCH) // timedelta(microseconds=1)


@pytest.fixture
def db(tmp_path):
    conn = open_database(tmp_path / "t.db")
    yield conn
    conn.close()


def activity(id="11000000001", start=datetime(2026, 9, 10, 7, 30, tzinfo=timezone(timedelta(hours=3))), minutes=52,
             type=ActivityType.RUNNING, series=None) -> TrainingActivity:
    return TrainingActivity(id, start, timedelta(minutes=minutes), type, series)


def one_hz(n: int = 3600) -> HeartRateSeries:
    return HeartRateSeries([HeartRateSample(timedelta(seconds=i), 100 + i % 100) for i in range(n)])


def test_a_fresh_file_gets_version_1_and_exactly_the_three_tables(tmp_path):
    conn = open_database(tmp_path / "fresh.db")

    assert conn.execute("PRAGMA user_version").fetchone()[0] == 1
    tables = {r[0] for r in conn.execute("SELECT name FROM sqlite_master WHERE type = 'table'")}
    assert tables == {"connection", "activity", "sync_state"}
    columns = [(r[1], r[2], r[3], r[5]) for r in conn.execute("PRAGMA table_info(activity)")]
    assert columns == [
        ("provider", "TEXT", 1, 1), ("external_id", "TEXT", 1, 2), ("started_at_utc_us", "INTEGER", 1, 0),
        ("started_at_offset_min", "INTEGER", 1, 0), ("moving_time_us", "INTEGER", 1, 0), ("type", "TEXT", 1, 0),
        ("heart_rate_json", "TEXT", 0, 0), ("heart_rate_outstanding", "INTEGER", 1, 0)]
    indexes = {r[1] for r in conn.execute("PRAGMA index_list(activity)")}
    assert "activity_started" in indexes


def test_opening_twice_is_idempotent(tmp_path):
    open_database(tmp_path / "t.db").close()
    conn = open_database(tmp_path / "t.db")

    assert conn.execute("PRAGMA user_version").fetchone()[0] == 1


def test_a_session_read_back_equals_the_session_that_was_stored(db):
    original = activity(start=datetime(2026, 9, 10, 23, 50, tzinfo=timezone(timedelta(hours=5, minutes=45))), series=one_hz())

    ActivityStore(db).upsert(original, heart_rate_outstanding=True)

    [stored] = ActivityStore(db).between(EPOCH, datetime(9999, 1, 1, tzinfo=UTC))
    assert stored == original
    assert stored.started_at.utcoffset() == timedelta(hours=5, minutes=45)


def test_the_stored_columns_hold_utc_microseconds_and_offset_minutes(db):
    original = activity(series=HeartRateSeries([HeartRateSample(timedelta(0), 140), HeartRateSample(timedelta(seconds=300), 150)]))

    ActivityStore(db).upsert(original, heart_rate_outstanding=False)

    row = db.execute("SELECT provider, external_id, started_at_utc_us, started_at_offset_min, moving_time_us, type, heart_rate_json, heart_rate_outstanding FROM activity").fetchone()
    assert row == ("Strava", "11000000001", us(original.started_at), 180, 52 * 60_000_000, "Running", "[[0,140],[300000000,150]]", 0)


def test_storing_the_same_activity_twice_updates_one_row_in_place(db):
    store = ActivityStore(db)

    store.upsert(activity(minutes=52), heart_rate_outstanding=False)
    store.upsert(activity(minutes=60, type=ActivityType.CYCLING), heart_rate_outstanding=True)

    assert db.execute("SELECT COUNT(*), MAX(moving_time_us), MAX(type), MAX(heart_rate_outstanding) FROM activity").fetchone() == (1, 60 * 60_000_000, "Cycling", 1)


def test_two_activities_alike_in_every_value_but_their_id_are_two_rows(db):
    store = ActivityStore(db)

    store.upsert(activity(id="a"), heart_rate_outstanding=False)
    store.upsert(activity(id="b"), heart_rate_outstanding=False)

    assert db.execute("SELECT COUNT(*) FROM activity").fetchone()[0] == 2


def test_a_held_series_is_kept_when_the_activity_is_imported_again_without_one(db):
    store = ActivityStore(db)
    series = HeartRateSeries([HeartRateSample(timedelta(0), 140), HeartRateSample(timedelta(seconds=60), 150)])

    store.upsert(activity(series=series), heart_rate_outstanding=False)
    store.upsert(activity(series=None), heart_rate_outstanding=False)

    [stored] = store.between(EPOCH, datetime(9999, 1, 1, tzinfo=UTC))
    assert stored.heart_rate == series


def test_a_session_with_no_series_stores_sql_null(db):
    ActivityStore(db).upsert(activity(), heart_rate_outstanding=False)

    assert db.execute("SELECT COUNT(*) FROM activity WHERE heart_rate_json IS NULL").fetchone()[0] == 1


def test_no_load_value_is_stored_anywhere_in_the_row(db):
    columns = [r[1].lower() for r in db.execute("PRAGMA table_info(activity)")]

    assert not [c for c in columns if "load" in c or "trimp" in c or "points" in c]


@pytest.mark.parametrize(("corruption", "failure"), [("[[0,300],[60000000,150]]", ValueError), ("not json", json.JSONDecodeError)])
def test_a_corrupted_row_fails_at_the_boundary_when_read(db, corruption, failure):
    ActivityStore(db).upsert(activity(), heart_rate_outstanding=False)
    db.execute("UPDATE activity SET heart_rate_json = ?", (corruption,))

    with pytest.raises(failure):
        ActivityStore(db).between(EPOCH, datetime(9999, 1, 1, tzinfo=UTC))


def test_a_row_converts_to_and_from_the_domain():
    original = activity(series=one_hz(10))

    assert ActivityRow.from_domain(original, "Strava", False).to_domain() == original


def test_between_returns_sessions_in_start_order_inside_the_bounds(db):
    store = ActivityStore(db)
    for day in (12, 10, 11, 14):
        store.upsert(activity(id=f"d{day}", start=datetime(2026, 9, day, 8, tzinfo=UTC)), heart_rate_outstanding=False)

    found = store.between(datetime(2026, 9, 10, 8, tzinfo=UTC), datetime(2026, 9, 12, 8, tzinfo=UTC))

    assert [a.external_id for a in found] == ["d10", "d11", "d12"]


def test_the_latest_start_and_the_earliest_outstanding_start_since_an_instant(db):
    store = ActivityStore(db)
    assert store.latest_start() is None
    store.upsert(activity(id="a", start=datetime(2026, 3, 1, tzinfo=UTC)), heart_rate_outstanding=True)
    store.upsert(activity(id="b", start=datetime(2026, 9, 1, tzinfo=UTC)), heart_rate_outstanding=True)
    store.upsert(activity(id="c", start=datetime(2026, 9, 10, tzinfo=UTC)), heart_rate_outstanding=False)

    assert store.latest_start() == datetime(2026, 9, 10, tzinfo=UTC)
    assert store.earliest_outstanding_start(datetime(2026, 4, 1, tzinfo=UTC)) == datetime(2026, 9, 1, tzinfo=UTC)
    assert store.earliest_outstanding_start(datetime(2026, 9, 2, tzinfo=UTC)) is None
