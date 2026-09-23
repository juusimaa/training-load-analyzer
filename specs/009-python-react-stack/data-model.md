# Data Model: Re-implementation on an Alternative Stack

**Feature**: 009-python-react-stack | **Date**: 2026-09-23 | **Plan**: [plan.md](./plan.md)

The entities and their rules are those of features 001–005 and the dashboard read model of 006
and 008, unchanged (009 FR-002). This document fixes **how each is represented in the new
stack**, and the few places where the representation itself carries a rule. The rules are cited,
not restated. Paths are relative to `alt-stack/backend/src/tla/`.

## 1. Domain (`domain/`), pure and with no I/O

All types are frozen dataclasses (`@dataclass(frozen=True, slots=True)`) or `enum.Enum`. Nothing
in `domain/` imports from any other package in `tla`, from `httpx`, `sqlite3` or `fastapi`, or
names Strava (Principle II, 001 FR-025, 002 FR-025, 003 FR-030, 004 FR-034). A test in
`tests/domain/test_independence.py` asserts this by walking the package's imports.

| Type | Fields | Rules carried by the representation | Source |
| --- | --- | --- | --- |
| `ActivityType` | `RUNNING`, `CYCLING` | Nothing else can be constructed. Display name `Running`/`Cycling` (the reference prints the enum name). | 001 FR-005 |
| `LoadProvenance` | `MEASURED`, `ESTIMATED` | — | 001 FR-014 |
| `LoadBasis` | `NONE`, `MEASURED`, `ESTIMATED`, `MIXED` | One `combine(any_measured, any_estimated)` helper per module where the reference has one (three sites, per 004 research R12, not extracted) | 002 FR-014 |
| `HeartRateSample` | `time_from_start: timedelta`, `bpm: int` | Validates nothing; the series does | 001 FR-007 |
| `HeartRateSeries` | `samples: tuple[HeartRateSample, ...]` | Non-empty, strictly ascending, each 20–250 bpm, else `ValueError` naming the rule and the index or value. Copied to a tuple on construction. | 001 FR-021, FR-022 |
| `TrainingLoad` | `points: Decimal`, `provenance: LoadProvenance` | Inseparable: nothing yields the points without the provenance | 001 FR-008, SC-007 |
| `TrainingActivity` | `external_id: str`, `started_at: datetime` (aware), `moving_time: timedelta`, `type: ActivityType`, `heart_rate: HeartRateSeries \| None` | Refuses a blank id, a naive or missing start, moving time ≤ 0, and a non-member type, each with its own message. Frozen, so a correction is a new record. | 001 FR-017 – FR-024 |
| `IsoWeek` | `year`, `week`, `monday: date`; `sunday` derived | From `date.isocalendar()`. Ordered and compared by `monday`, never by `(year, week)`. | 002 FR-002, 004 research R9 |
| `DateRange` | `start: date`, `end: date`; `days()` | Refuses a missing bound before an inverted range (the order is load-bearing: 002 FR-021) | 002 FR-019, FR-020 |
| `DailyTrainingLoad` | `day`, `points: Decimal`, `activity_count`, `basis` | — | 002 |
| `WeeklyTrainingLoad` | `week: IsoWeek`, `points: Decimal`, `activity_count`, `basis` | Built by chunking the extended daily series into sevens | 002 FR-013, FR-017 |
| `DailyTrainingMetrics` | `day`, `fitness: float`, `fatigue: float`, `is_reliable`, `fitness_basis`, `fatigue_basis`; `form` and `form_basis` are **properties** | Form is never stored (003 FR-003); `form_basis` is `fitness_basis` (003 FR-019b) | 003 |
| `WeeklyLoadTrend` | `week`, `points`, `previous_points`, `is_complete`, `basis`; `absolute_change`, `relative_change: Decimal \| None`, `classification` are **properties** | Thresholds `Decimal("50")` and `Decimal("0.15")` are private module constants. Completeness is read first, then the floor, then the relative test. | 004 FR-007 – FR-019 |
| `TrendClassification` | `INDETERMINATE`, `STEADY`, `SIGNIFICANT_INCREASE`, `SIGNIFICANT_DECREASE` | — | 004 FR-007 |

**Arithmetic.**
- **Loads.** All load arithmetic runs under one module-level `decimal.Context(prec=34,
  rounding=ROUND_HALF_EVEN)` (research R1). Minutes are
  `Decimal(timedelta // timedelta(microseconds=1)) / Decimal(60_000_000)`.
- **Zone weights.** These are integer cross-multiplication: `bpm * 100 >= 90 * max_hr`, and so
  on (001 FR-009).
- **Smoothing.** The factors are `1 - math.exp(-1 / 42)` and `1 - math.exp(-1 / 7)`, as doubles
  (003 FR-005, FR-029a). The daily load enters the smoothing as `float(points)`.

**Functions** (module-level, pure, one per reference static method):
- `aggregate_daily(activities, range, max_hr)` and `aggregate_weekly(...)`;
- `calculate_metrics(history, range)`;
- `calculate_trends(history, range)`.

Each refusal is a `ValueError` whose message matches the reference's wording, with the dates
formatted `YYYY-MM-DD`.

## 2. Integration (`strava/`), where Strava's shapes stay

These are Strava's shapes, never imported by `domain/`, `api/` or `dashboard/`.

| Type | Notes | Source |
| --- | --- | --- |
| `StravaActivitySummary` | Parsed from a dict. Reads `id` (number → decimal string, verbatim), `sport_type`, `start_date`, `utc_offset` (int, or a float that is whole; see research R17), `moving_time`, `has_heartrate`, `manual`, `private`, `trainer`. Missing optional keys take the reference's defaults. Unknown keys are ignored. `start_date_local` and `type` are never read. | 005 FR-010a, FR-020, R15 |
| `StravaStreamSet` | `time: list[int] \| None`, `heartrate: list[int] \| None`. A missing stream is an absent key. | 005 R20 |
| `StravaTokens` | `access_token`, `refresh_token`, `expires_at` (epoch seconds), `scope: str \| None`, `athlete_id: int \| None` | 005 FR-002a |
| `RateLimitStatus` | From the `X-ReadRateLimit-Limit`/`-Usage` headers, matched case-insensitively. `is_exhausted`; `retry_after(now_utc)` returns the next quarter-hour boundary, or the next UTC midnight when the daily budget is spent. | 005 FR-034, FR-035 |
| Failures | `StravaRateLimited(status)`, `StravaRequestFailed(http_status \| None)`, `StravaTokenRejected`, `InsufficientScope(requested, granted)`, `ReconnectionRequired(athlete_id)`. Messages match the reference and carry no credential. | 005 FR-005, FR-006 |

The **mapper** (`strava/mapper.py`) has the fixed sport-type table (005 FR-010), `map_activity`
(which returns `MappedActivity | SkippedActivity`), and `to_series` (which discards implausible or
repeated-time samples, counts them, and returns `None` below 2 survivors, per 005 FR-017f and
FR-017g). It is pure.

The **client** (`strava/client.py`) wraps an `httpx.Client`:
- page size 200;
- three attempts with exponential backoff (10 ms, 20 ms);
- a retry only on a transport error or a 5xx;
- 429 raises `StravaRateLimited`;
- an exhausted budget on a successful response also raises (it stops before the limit);
- 404 on streams returns `None`.

This is 005 FR-034 – FR-037, as the reference implements them.

## 3. Persistence (`persistence/`)

SQLite through stdlib `sqlite3`, versioned by `PRAGMA user_version` (research R6). This file is
the new implementation's own (009 FR-022).

```sql
-- migration 1
CREATE TABLE connection (
    athlete_id          INTEGER PRIMARY KEY,
    access_token        TEXT    NOT NULL,   -- secret: never logged, never serialised to the API
    refresh_token       TEXT    NOT NULL,   -- secret, replaced on every token response
    expires_at_utc_us   INTEGER NOT NULL,
    granted_scopes      TEXT    NOT NULL,
    connected_at_utc_us INTEGER NOT NULL
);

CREATE TABLE activity (
    provider             TEXT    NOT NULL,          -- always 'Strava'
    external_id          TEXT    NOT NULL,          -- verbatim
    started_at_utc_us    INTEGER NOT NULL,
    started_at_offset_min INTEGER NOT NULL,
    moving_time_us       INTEGER NOT NULL,
    type                 TEXT    NOT NULL,          -- 'Running' | 'Cycling'
    heart_rate_json      TEXT,                      -- [[us_from_start, bpm], ...] or NULL
    heart_rate_outstanding INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (provider, external_id)
);
CREATE INDEX activity_started ON activity (started_at_utc_us);

CREATE TABLE sync_state (
    athlete_id                 INTEGER PRIMARY KEY,
    resume_point_utc_us        INTEGER NOT NULL,
    last_sync_started_at_utc_us INTEGER,
    last_outcome               TEXT    NOT NULL   -- 'Completed' | 'RateLimited' | ...
);
```

| Rule | Where it is enforced | Source |
| --- | --- | --- |
| Identity is `(provider, external_id)`; a re-import updates in place | `ActivityStore.upsert` (`INSERT … ON CONFLICT DO UPDATE`) | 005 FR-023, FR-030 |
| A held heart-rate series is never replaced or cleared | the upsert sets `heart_rate_json = COALESCE(activity.heart_rate_json, excluded.heart_rate_json)` | 005 FR-017b |
| A stored session reads back equal, and corrupt rows fail at the boundary | `ActivityRow.to_domain()` runs the domain constructors | 005 FR-024, C57 |
| At most one connection; a different athlete is refused while one is held | `StravaAuthorization.exchange` | 005 FR-008 |
| Resume point = `max(epoch, min(latest start, earliest outstanding start inside 180 days) − 7 days)` | `ActivitySync._record_state` | 005 FR-017e, FR-028, FR-029 |
| Reconciliation only over a span read to completion | `ActivitySync._reconcile` | 005 FR-031 – FR-031e |

Each unit of work (one dashboard read, one sync, one callback) opens its own connection and
closes it. There is no connection shared across threads.

## 4. Application state (`sync/coordinator.py`)

`SyncCoordinator` is one instance per process, held on `app.state` (research R5, R9):
- a `threading.Lock`, acquired with `blocking=False`, so a second request is refused rather than
  queued;
- `status: SyncStatus`, never `None`.

`SyncStatus` has these fields:
- `is_running: bool`;
- `result: SyncResult | None`;
- `failure: str | None` (only ever the literal "Strava connection required.");
- `finished_at: datetime | None` (local);
- `retry_after_local: datetime | None`.

`SyncResult` has:
- `imported`, `updated`, `series_outstanding`;
- `skipped: list[(external_id, reason)]`, `removed: list[(external_id, reason)]`,
  `discarded: list[(external_id, count)]`;
- `outcome` (`Completed`, `RateLimited`, `Interrupted`, `ReconnectionRequired` or `Refused`);
- `retry_after`.

It carries no credential (005 FR-005, C66).

## 5. Dashboard read model (`dashboard/`), what crosses to the browser

`build_dashboard_view(activities, today, max_hr, connected)` is pure and ports
`DashboardViewBuilder.Build` (180-day chart range, recent 7, trend precondition, a future-dated
session extending the range). `read_dashboard(db, clock, settings)` ports `DashboardReader` and
catches the same failure classes (`ValueError`, `json.JSONDecodeError`, `OverflowError`) into
`is_unavailable`, logging the reason.

The HTTP shape is fixed in [contracts/http-api.md §2](./contracts/http-api.md#2-get-apidashboard).
Every string in it is produced by `dashboard/display.py` (research R2), so the frontend formats no
figure.

## 6. Frontend (`alt-stack/frontend/src/`)

| Module | Role |
| --- | --- |
| `api.ts` | `fetchDashboard()`, `fetchSyncStatus()`, `postSync()`: typed wrappers over `fetch`, returning the contract types or throwing `Unavailable`. It is passed to `App` as a prop (the `DashboardApi` interface), so tests pass a plain object. |
| `types.ts` | TypeScript types mirroring [contracts/http-api.md](./contracts/http-api.md) exactly |
| `chart/geometry.ts` | Pure port of `MetricsChart`: `viewBox`, `plot`, `loadBars`, `zeroRule`, `axisTicks`, `hoverSlots`. Input is the windowed day list; output is strings, already rounded (research R8). |
| `window.ts` | `windowed(days, n)`, the trailing slice; `WINDOWS = [30, 90, 180]`, default 180 |
| Components | `Dashboard`, `Rail`, `SyncPanel`, `MetricRow`, `MetricsChart`, `RecentActivities`, `NotFound`, `ErrorPage`, `ErrorBoundary`. Each is a pure function of its props except `Dashboard`, which owns the fetch lifecycle, the chosen window and the sync-in-flight flag. |
