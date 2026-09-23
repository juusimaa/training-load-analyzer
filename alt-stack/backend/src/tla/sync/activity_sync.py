"""Walks Strava's activity list and keeps the local copy in step (005 FR-027 – FR-040).

A port of StravaActivitySync.cs, method for method (`run` ↔ SyncAsync, `_walk` ↔ WalkAsync,
`_read_all_pages` ↔ ReadAllPagesAsync, `_series_for` ↔ SeriesForAsync, `_record_state` ↔
RecordStateAsync, `_reconcile` ↔ ReconcileAsync), with two specified departures from the reference
(Amendment 1(b)): the token is renewed before the first request (research R10), and a read limit
reached on a stream request leaves that series owed instead of escaping the sync (005 FR-017d).
"""

import logging
import sqlite3
from datetime import UTC, datetime, timedelta

from tla.persistence.activity_store import ActivityStore, ConnectionStore
from tla.persistence.rows import SyncStateRow
from tla.strava.client import StravaApiClient
from tla.strava.errors import ReconnectionRequired, StravaRateLimited, StravaRequestFailed
from tla.strava.mapper import MappedActivity, SkippedActivity, map_activity, to_series
from tla.strava.shapes import StravaActivitySummary
from tla.sync.authorization import StravaAuthorization
from tla.sync.results import DiscardedSamples, RemovalReason, RemovedActivity, SyncOutcome, SyncResult

_log = logging.getLogger("tla.sync")

_EPOCH = datetime(1970, 1, 1, tzinfo=UTC)

# How far back a heart-rate series is worth the request it costs (005 FR-017a).
_MEASURED_WINDOW = timedelta(days=180)

# How far behind already-stored ground each sync re-reads, to catch late uploads (005 FR-028).
_LOOK_BACK_WINDOW = timedelta(days=7)


class NotConnected(Exception):
    """The one failure signalled by raising: there is no account to sync."""

    def __init__(self) -> None:
        super().__init__("No Strava account is connected. Authorize one before synchronising.")


class ActivitySync:
    def __init__(self, conn: sqlite3.Connection, client: StravaApiClient, authorization: StravaAuthorization, clock):
        self._connections = ConnectionStore(conn)
        self._store = ActivityStore(conn)
        self._client = client
        self._authorization = authorization
        self._clock = clock

    def run(self) -> SyncResult:
        """Reads from the stored resume point onward (005 FR-027). Every failure is an outcome."""
        connection = self._connections.get()
        if connection is None:
            raise NotConnected()

        state = self._connections.get_sync_state(connection.athlete_id)
        resume_point = _EPOCH if state is None else state.resume_point

        try:
            connection = self._authorization.ensure_fresh()
        except ReconnectionRequired:
            result = SyncResult(outcome=SyncOutcome.RECONNECTION_REQUIRED)
        else:
            result = self._walk(connection.access_token, resume_point)

        self._record_state(connection.athlete_id, result.outcome)
        _log.info("Sync ended %s: %d imported, %d updated, %d skipped, %d removed.",
                  result.outcome.value, result.imported, result.updated, len(result.skipped), len(result.removed))
        return result

    def _walk(self, access_token: str, start: datetime) -> SyncResult:
        imported = updated = outstanding = 0
        skipped: list[SkippedActivity] = []
        discarded: list[DiscardedSamples] = []
        measured_from = self._clock.now_utc() - _MEASURED_WINDOW
        self._stopped = SyncOutcome.COMPLETED
        self._retry_after: datetime | None = None

        # Set in exactly one place, after the last page, because a second assignment is how the
        # guarantee in 005 FR-031c erodes.
        span_read_to_completion = False
        seen: set[str] = set()
        summaries: list[StravaActivitySummary] = []

        try:
            self._read_all_pages(access_token, start, summaries)
            span_read_to_completion = True
        except StravaRateLimited as limited:
            self._limit_reached(limited)
        except StravaRequestFailed as failed:
            self._stopped = SyncOutcome.RECONNECTION_REQUIRED if failed.status == 401 else SyncOutcome.INTERRUPTED

        # Strava's order is undocumented, so the walk deduplicates by id and sorts by start.
        for summary in sorted(_distinct_by_id(summaries), key=lambda s: s.start_date or datetime.min.replace(tzinfo=UTC)):
            series, series_missing, dropped = self._series_for(access_token, summary, measured_from)

            if dropped > 0:
                discarded.append(DiscardedSamples(summary.external_id, dropped))

            mapped = map_activity(summary, series)
            if isinstance(mapped, SkippedActivity):
                skipped.append(mapped)
                continue

            assert isinstance(mapped, MappedActivity)
            seen.add(summary.external_id)
            existed = self._store.exists(summary.external_id)
            self._store.upsert(mapped.activity, series_missing)
            if existed:
                updated += 1
            else:
                imported += 1
            if series_missing:
                outstanding += 1

        # Only a complete span that also ended Completed may remove anything (005 FR-031c, C61).
        removed = self._reconcile(span_read_to_completion and self._stopped is SyncOutcome.COMPLETED, start, seen)

        return SyncResult(
            imported=imported,
            updated=updated,
            skipped=skipped,
            removed=removed,
            series_outstanding=outstanding,
            discarded=discarded,
            outcome=self._stopped,
            retry_after=self._retry_after,
        )

    def _limit_reached(self, limited: StravaRateLimited) -> None:
        self._stopped = SyncOutcome.RATE_LIMITED
        if self._retry_after is None and limited.status is not None:
            self._retry_after = limited.status.retry_after(self._clock.now_utc())

    def _read_all_pages(self, access_token: str, start: datetime, summaries: list[StravaActivitySummary]) -> None:
        """Accumulates into the caller's list, so the pages already read survive a failure partway."""
        page = 1
        while True:
            batch = self._client.list_activities(access_token, start, page)
            if not batch:
                return
            summaries.extend(batch)
            page += 1

    def _series_for(self, access_token: str, summary: StravaActivitySummary, measured_from: datetime):
        """(series, missing, dropped). Fetched only when Strava reports heart rate, the activity is inside
        the measured window, and no series is held yet (005 FR-017)."""
        if not summary.has_heartrate or summary.start_date is None or summary.start_date < measured_from:
            return None, False, 0

        if self._store.has_series(summary.external_id):
            return None, False, 0

        # A limit already reached is not asked again (005 FR-034, C68): the series is simply owed.
        if self._stopped is SyncOutcome.RATE_LIMITED:
            return None, True, 0

        try:
            streams = self._client.get_streams(access_token, summary.external_id)
        except StravaRequestFailed:
            return None, True, 0
        except StravaRateLimited as limited:
            self._limit_reached(limited)
            return None, True, 0

        if streams is None:
            return None, True, 0

        series, dropped = to_series(streams)
        return series, series is None and dropped == 0, dropped

    def _record_state(self, athlete_id: int, outcome: SyncOutcome) -> None:
        """Resume point = max(epoch, min(latest stored start, earliest owed start inside the window) − 7 days)
        (005 FR-017e, FR-025, FR-028, FR-029)."""
        now = self._clock.now_utc()
        latest = self._store.latest_start()
        earliest_owed = self._store.earliest_outstanding_start(now - _MEASURED_WINDOW)

        anchor = _EPOCH if latest is None else min(latest, earliest_owed or latest)
        self._connections.save_sync_state(SyncStateRow(
            athlete_id=athlete_id,
            resume_point=max(_EPOCH, anchor - _LOOK_BACK_WINDOW),
            last_sync_started_at=now,
            last_outcome=outcome.value,
        ))

    def _reconcile(self, span_read_to_completion: bool, start: datetime, seen: set[str]) -> list[RemovedActivity]:
        """Removes stored sessions absent from a span read to completion (005 FR-031). A sport type
        changed out of scope is never seen, so it leaves by the same path (FR-031e)."""
        if not span_read_to_completion:
            return []
        gone = [external_id for external_id in self._store.ids_since(start) if external_id not in seen]
        if gone:
            self._store.remove(gone)
        return [RemovedActivity(external_id, RemovalReason.DELETED_AT_SOURCE) for external_id in gone]


def _distinct_by_id(summaries: list[StravaActivitySummary]) -> list[StravaActivitySummary]:
    first: dict[str, StravaActivitySummary] = {}
    for summary in summaries:
        first.setdefault(summary.external_id, summary)
    return list(first.values())
