"""The stored connection (005 FR-003, FR-004, FR-005, FR-008; C48)."""

from datetime import UTC, datetime

from tla.persistence.activity_store import ConnectionStore
from tla.persistence.rows import ConnectionRow, SyncStateRow
from tla.persistence.schema import open_database


def row(access="test-access-1", refresh="test-refresh-1", athlete=900001) -> ConnectionRow:
    return ConnectionRow(athlete_id=athlete, access_token=access, refresh_token=refresh,
                         expires_at=datetime(2026, 9, 17, 16, 7, 33, tzinfo=UTC), granted_scopes="read,activity:read_all",
                         connected_at=datetime(2026, 9, 17, 10, 7, 33, tzinfo=UTC))


def test_with_no_connection_get_returns_none(tmp_path):
    assert ConnectionStore(open_database(tmp_path / "t.db")).get() is None


def test_a_saved_connection_survives_reopening_the_database(tmp_path):
    ConnectionStore(open_database(tmp_path / "t.db")).save(row())

    assert ConnectionStore(open_database(tmp_path / "t.db")).get() == row()


def test_a_new_token_pair_replaces_both_tokens_together(tmp_path):
    store = ConnectionStore(open_database(tmp_path / "t.db"))
    store.save(row())

    store.save(row(access="test-access-2", refresh="test-refresh-2"))

    stored = store.get()
    assert (stored.access_token, stored.refresh_token) == ("test-access-2", "test-refresh-2")
    assert store._conn.execute("SELECT COUNT(*) FROM connection").fetchone()[0] == 1


def test_the_repr_of_a_connection_carries_no_token():
    text = repr(row(access="test-access-secret", refresh="test-refresh-secret"))

    assert "test-access-secret" not in text and "test-refresh-secret" not in text


def test_the_sync_state_is_saved_and_read_back(tmp_path):
    store = ConnectionStore(open_database(tmp_path / "t.db"))
    state = SyncStateRow(athlete_id=900001, resume_point=datetime(2026, 9, 3, 5, tzinfo=UTC),
                         last_sync_started_at=datetime(2026, 9, 17, 10, 7, 33, tzinfo=UTC), last_outcome="Completed")

    assert store.get_sync_state(900001) is None
    store.save_sync_state(state)
    store.save_sync_state(state)

    assert store.get_sync_state(900001) == state
