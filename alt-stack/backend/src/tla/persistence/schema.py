"""The new implementation's own SQLite file, versioned with PRAGMA user_version (research R6, 009 FR-022)."""

import sqlite3
from pathlib import Path

# Ordered; migration N brings user_version from N-1 to N. Verbatim from data-model.md §3.
MIGRATIONS = [
    """
    CREATE TABLE connection (
        athlete_id          INTEGER PRIMARY KEY,
        access_token        TEXT    NOT NULL,
        refresh_token       TEXT    NOT NULL,
        expires_at_utc_us   INTEGER NOT NULL,
        granted_scopes      TEXT    NOT NULL,
        connected_at_utc_us INTEGER NOT NULL
    );

    CREATE TABLE activity (
        provider             TEXT    NOT NULL,
        external_id          TEXT    NOT NULL,
        started_at_utc_us    INTEGER NOT NULL,
        started_at_offset_min INTEGER NOT NULL,
        moving_time_us       INTEGER NOT NULL,
        type                 TEXT    NOT NULL,
        heart_rate_json      TEXT,
        heart_rate_outstanding INTEGER NOT NULL DEFAULT 0,
        PRIMARY KEY (provider, external_id)
    );
    CREATE INDEX activity_started ON activity (started_at_utc_us);

    CREATE TABLE sync_state (
        athlete_id                 INTEGER PRIMARY KEY,
        resume_point_utc_us        INTEGER NOT NULL,
        last_sync_started_at_utc_us INTEGER,
        last_outcome               TEXT    NOT NULL
    );
    """,
]


def open_database(path: str | Path) -> sqlite3.Connection:
    """Opens the file, applying any migration it has not had yet. One connection per unit of work."""
    conn = sqlite3.connect(path)
    version = conn.execute("PRAGMA user_version").fetchone()[0]
    for number, script in enumerate(MIGRATIONS[version:], start=version + 1):
        with conn:
            conn.executescript(f"BEGIN; {script} PRAGMA user_version = {number}; COMMIT;")
    return conn
