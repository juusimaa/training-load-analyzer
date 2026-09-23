"""Reads and writes the stored sessions, connection and sync state (005 FR-022 – FR-030)."""

import sqlite3
from datetime import datetime

from tla.domain.activity import TrainingActivity
from tla.persistence.rows import ActivityRow, ConnectionRow, SyncStateRow, from_utc_us, to_utc_us

# The only provider imported. Recorded, not abstracted over.
STRAVA = "Strava"

_COLUMNS = "provider, external_id, started_at_utc_us, started_at_offset_min, moving_time_us, type, heart_rate_json, heart_rate_outstanding"


class ActivityStore:
    def __init__(self, conn: sqlite3.Connection):
        self._conn = conn

    def upsert(self, activity: TrainingActivity, heart_rate_outstanding: bool) -> None:
        """One row per (provider, external_id), updated in place. A held series is never replaced or
        cleared (005 FR-017b). Committed at once, so a sync that stops later keeps what it stored."""
        row = ActivityRow.from_domain(activity, STRAVA, heart_rate_outstanding)
        with self._conn:
            self._conn.execute(
                f"INSERT INTO activity ({_COLUMNS}) VALUES (?, ?, ?, ?, ?, ?, ?, ?) "
                "ON CONFLICT (provider, external_id) DO UPDATE SET "
                "started_at_utc_us = excluded.started_at_utc_us, "
                "started_at_offset_min = excluded.started_at_offset_min, "
                "moving_time_us = excluded.moving_time_us, "
                "type = excluded.type, "
                "heart_rate_outstanding = excluded.heart_rate_outstanding, "
                "heart_rate_json = COALESCE(activity.heart_rate_json, excluded.heart_rate_json)",
                (row.provider, row.external_id, row.started_at_utc_us, row.started_at_offset_min,
                 row.moving_time_us, row.type, row.heart_rate_json, int(row.heart_rate_outstanding)),
            )

    def exists(self, external_id: str) -> bool:
        return self._conn.execute(
            "SELECT 1 FROM activity WHERE provider = ? AND external_id = ?", (STRAVA, external_id)).fetchone() is not None

    def has_series(self, external_id: str) -> bool:
        return self._conn.execute(
            "SELECT 1 FROM activity WHERE provider = ? AND external_id = ? AND heart_rate_json IS NOT NULL",
            (STRAVA, external_id)).fetchone() is not None

    def between(self, start: datetime, end: datetime) -> list[TrainingActivity]:
        """Sessions whose start falls inside the bounds, ascending by start."""
        rows = self._conn.execute(
            f"SELECT {_COLUMNS} FROM activity WHERE started_at_utc_us >= ? AND started_at_utc_us <= ? "
            "ORDER BY started_at_utc_us, rowid",
            (to_utc_us(start), to_utc_us(end)),
        ).fetchall()
        return [ActivityRow(*r[:7], bool(r[7])).to_domain() for r in rows]

    def latest_start(self) -> datetime | None:
        value = self._conn.execute("SELECT MAX(started_at_utc_us) FROM activity").fetchone()[0]
        return None if value is None else from_utc_us(value)

    def earliest_outstanding_start(self, since: datetime) -> datetime | None:
        value = self._conn.execute(
            "SELECT MIN(started_at_utc_us) FROM activity WHERE heart_rate_outstanding = 1 AND started_at_utc_us >= ?",
            (to_utc_us(since),)).fetchone()[0]
        return None if value is None else from_utc_us(value)

    def ids_since(self, start: datetime) -> list[str]:
        """The stored ids a span read from `start` should have seen (reconciliation's candidates)."""
        return [r[0] for r in self._conn.execute(
            "SELECT external_id FROM activity WHERE provider = ? AND started_at_utc_us >= ? ORDER BY started_at_utc_us, rowid",
            (STRAVA, to_utc_us(start)))]

    def remove(self, external_ids: list[str]) -> None:
        with self._conn:
            self._conn.executemany("DELETE FROM activity WHERE provider = ? AND external_id = ?", [(STRAVA, i) for i in external_ids])


class ConnectionStore:
    """The one connection and its sync state. At most one athlete is held (005 FR-008)."""

    def __init__(self, conn: sqlite3.Connection):
        self._conn = conn

    def get(self) -> ConnectionRow | None:
        row = self._conn.execute(
            "SELECT athlete_id, access_token, refresh_token, expires_at_utc_us, granted_scopes, connected_at_utc_us "
            "FROM connection ORDER BY athlete_id LIMIT 1").fetchone()
        if row is None:
            return None
        return ConnectionRow(row[0], row[1], row[2], from_utc_us(row[3]), row[4], from_utc_us(row[5]))

    def save(self, connection: ConnectionRow) -> None:
        """Both tokens are written together, always (005 C48)."""
        with self._conn:
            self._conn.execute(
                "INSERT INTO connection (athlete_id, access_token, refresh_token, expires_at_utc_us, granted_scopes, connected_at_utc_us) "
                "VALUES (?, ?, ?, ?, ?, ?) ON CONFLICT (athlete_id) DO UPDATE SET "
                "access_token = excluded.access_token, refresh_token = excluded.refresh_token, "
                "expires_at_utc_us = excluded.expires_at_utc_us, granted_scopes = excluded.granted_scopes, "
                "connected_at_utc_us = excluded.connected_at_utc_us",
                (connection.athlete_id, connection.access_token, connection.refresh_token,
                 to_utc_us(connection.expires_at), connection.granted_scopes, to_utc_us(connection.connected_at)),
            )

    def get_sync_state(self, athlete_id: int) -> SyncStateRow | None:
        row = self._conn.execute(
            "SELECT athlete_id, resume_point_utc_us, last_sync_started_at_utc_us, last_outcome FROM sync_state WHERE athlete_id = ?",
            (athlete_id,)).fetchone()
        if row is None:
            return None
        return SyncStateRow(row[0], from_utc_us(row[1]), None if row[2] is None else from_utc_us(row[2]), row[3])

    def save_sync_state(self, state: SyncStateRow) -> None:
        with self._conn:
            self._conn.execute(
                "INSERT INTO sync_state (athlete_id, resume_point_utc_us, last_sync_started_at_utc_us, last_outcome) "
                "VALUES (?, ?, ?, ?) ON CONFLICT (athlete_id) DO UPDATE SET "
                "resume_point_utc_us = excluded.resume_point_utc_us, "
                "last_sync_started_at_utc_us = excluded.last_sync_started_at_utc_us, last_outcome = excluded.last_outcome",
                (state.athlete_id, to_utc_us(state.resume_point),
                 None if state.last_sync_started_at is None else to_utc_us(state.last_sync_started_at), state.last_outcome),
            )
