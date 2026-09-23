"""The application factory, and the one place the object graph is wired (research R5, R15).

Run with exactly one worker: the sync guard and the last sync status live in this process.
    uv run --env-file .env uvicorn tla.main:app --factory --workers 1
"""

import logging
import os
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path

import httpx
from fastapi import FastAPI

from tla.api import connect, dashboard, static, sync
from tla.clock import SystemClock
from tla.persistence.activity_store import ConnectionStore
from tla.persistence.schema import open_database
from tla.settings import ConfigurationError, Settings
from tla.strava.client import StravaApiClient
from tla.strava.oauth import StravaOAuthClient
from tla.sync.activity_sync import ActivitySync
from tla.sync.authorization import StravaAuthorization
from tla.sync.coordinator import SyncCoordinator
from tla.sync.results import SyncResult

_FRONTEND_DIST = Path(__file__).resolve().parents[3] / "frontend" / "dist"


def create_app(settings: Settings, clock, transport: httpx.BaseTransport | None = None, frontend_dist: Path | None = None) -> FastAPI:
    """`transport` replaces the network in tests (httpx.MockTransport); None is the real one."""
    app = FastAPI(docs_url=None, redoc_url=None, openapi_url=None)

    @contextmanager
    def authorization() -> Iterator[StravaAuthorization]:
        # One connection and one client per unit of work, closed when it ends.
        conn = open_database(settings.database_path)
        http = httpx.Client(transport=transport)
        try:
            oauth = StravaOAuthClient(http, settings.strava_client_id, settings.strava_client_secret)
            yield StravaAuthorization(ConnectionStore(conn), oauth, clock)
        finally:
            http.close()
            conn.close()

    def sync_once() -> SyncResult:
        conn = open_database(settings.database_path)
        http = httpx.Client(transport=transport)
        try:
            oauth = StravaOAuthClient(http, settings.strava_client_id, settings.strava_client_secret)
            authorization_ = StravaAuthorization(ConnectionStore(conn), oauth, clock)
            return ActivitySync(conn, StravaApiClient(http), authorization_, clock).run()
        finally:
            http.close()
            conn.close()

    app.state.settings = settings
    app.state.clock = clock
    app.state.coordinator = SyncCoordinator(clock)
    app.state.authorization = authorization
    app.state.sync_once = sync_once

    app.include_router(dashboard.router)
    app.include_router(sync.router)
    app.include_router(connect.router)
    static.mount_spa(app, frontend_dist)
    return app


def app() -> FastAPI:
    """The `--factory` entry. Refuses before the server binds when the configuration is unusable."""
    try:
        settings = Settings.from_env(os.environ)
    except ConfigurationError as refusal:
        # Logging is not configured yet, so the last-resort handler writes the message to stderr.
        logging.getLogger("tla").critical("%s", refusal)
        raise SystemExit(1) from None
    logging.basicConfig(level=logging.INFO)
    return create_app(settings, SystemClock(), frontend_dist=_FRONTEND_DIST)
