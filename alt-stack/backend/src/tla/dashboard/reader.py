"""Reads the stored history once and hands it to the builder (DashboardReader.cs; 006 C87)."""

import json
import logging
from datetime import UTC, datetime
from pathlib import Path

from tla.dashboard.view_builder import DashboardView, build_dashboard_view, designation
from tla.persistence.activity_store import ActivityStore, ConnectionStore
from tla.persistence.schema import open_database

_log = logging.getLogger("tla.dashboard")

_EARLIEST = datetime(1970, 1, 1, tzinfo=UTC)
_LATEST = datetime(9999, 12, 31, 23, 59, 59, 999999, tzinfo=UTC)


def read_dashboard(database_path: str | Path, clock, maximum_heart_rate: int) -> DashboardView:
    """The whole history, once: fitness accumulates from the first recorded day (research R15)."""
    # The athlete's local day, not UTC: an evening session belongs to the local week (research R22).
    today = clock.now_local().date()

    conn = open_database(database_path)
    try:
        connected = ConnectionStore(conn).get() is not None
        try:
            activities = ActivityStore(conn).between(_EARLIEST, _LATEST)
            return build_dashboard_view(activities, today, maximum_heart_rate, connected)
        except (ValueError, json.JSONDecodeError, OverflowError) as failure:
            # Named classes, and logged: the athlete sees "Data unavailable", and the log says why.
            _log.warning(
                "The stored training history could not be read (%s: %s), so the dashboard is "
                "showing its unavailable state.", type(failure).__name__, failure)
            return DashboardView(
                as_of=today,
                is_strava_connected=connected,
                maximum_heart_rate=maximum_heart_rate,
                iso_week=designation(today),
                is_unavailable=True,
            )
    finally:
        conn.close()
