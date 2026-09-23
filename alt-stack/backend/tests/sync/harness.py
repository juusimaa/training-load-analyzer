"""A connected account over a temp-file database, ready to sync against a stubbed Strava.

The equivalent of the reference's SyncHarness: a real store, a real client, a transport stub.
"""

from datetime import UTC, datetime, timedelta

import httpx

from tests.fakes import FixedClock, json_transport
from tla.persistence.activity_store import ActivityStore, ConnectionStore
from tla.persistence.rows import ConnectionRow
from tla.persistence.schema import open_database
from tla.strava.client import StravaApiClient
from tla.strava.oauth import StravaOAuthClient
from tla.sync.activity_sync import ActivitySync
from tla.sync.authorization import StravaAuthorization

NOW = datetime(2026, 9, 17, 10, 7, 33, tzinfo=UTC)
ROOM = {"X-ReadRateLimit-Limit": "100,1000", "X-ReadRateLimit-Usage": "5,50"}
EXHAUSTED = {"X-ReadRateLimit-Limit": "100,1000", "X-ReadRateLimit-Usage": "100,412"}
CLEAN_STREAM = {"time": {"data": [0, 300, 600]}, "heartrate": {"data": [140, 150, 160]}}


def activity(id: str, start: str, sport: str = "Run", moving: int = 3120, hr: bool = False, manual: bool = False) -> dict:
    return {"id": id, "sport_type": sport, "start_date": start, "utc_offset": 10800, "moving_time": moving,
            "elapsed_time": moving, "has_heartrate": hr, "manual": manual, "private": False}


def page(*activities: dict, headers: dict | None = None) -> tuple:
    return ("GET", "athlete/activities", 200, list(activities), headers or ROOM)


def empty_page() -> tuple:
    return page()


class SyncHarness:
    def __init__(self, tmp_path, clock: FixedClock | None = None, connected: bool = True):
        self.path = tmp_path / "sync.db"
        self.conn = open_database(self.path)
        self.clock = clock or FixedClock(NOW)
        if connected:
            ConnectionStore(self.conn).save(ConnectionRow(
                900001, "test-access-1", "test-refresh-1", NOW + timedelta(hours=6), "read,activity:read_all", NOW))

    def sync(self, routes: list[tuple]):
        transport, requests = json_transport(routes)
        http = httpx.Client(transport=transport)
        authorization = StravaAuthorization(ConnectionStore(self.conn), StravaOAuthClient(http, "12345", "test-client-secret"), self.clock)
        result = ActivitySync(self.conn, StravaApiClient(http, sleep=lambda _: None), authorization, self.clock).run()
        return result, [str(r.url) for r in requests]

    def count(self) -> int:
        return self.conn.execute("SELECT COUNT(*) FROM activity").fetchone()[0]

    def stored(self):
        return ActivityStore(self.conn).between(datetime(1970, 1, 1, tzinfo=UTC), datetime(9999, 1, 1, tzinfo=UTC))

    def state(self):
        return ConnectionStore(self.conn).get_sync_state(900001)


def after_of(urls: list[str]) -> int:
    first = next(u for u in urls if "athlete/activities" in u)
    return int(first.split("after=")[1].split("&")[0])
