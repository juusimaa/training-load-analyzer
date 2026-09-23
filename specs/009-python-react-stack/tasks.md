---

description: "Task list for feature 009: re-implementation on an alternative stack (Python/FastAPI + React/TypeScript)"
---

# Tasks: Re-implementation on an Alternative Stack

**Input**: Design documents from `/specs/009-python-react-stack/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md) (with Amendment 1), [research.md](./research.md) (R1–R17), [data-model.md](./data-model.md), [contracts/](./contracts/) ([http-api.md](./contracts/http-api.md), [dashboard-ui.md](./contracts/dashboard-ui.md), [parity.md](./contracts/parity.md)), [quickstart.md](./quickstart.md)

**Tests**: **Required.** Constitution Principle I (strict TDD, non-negotiable), 009 FR-021 and the Development Workflow ("tasks MUST be expressed as RED/GREEN/REFACTOR/VERIFY steps tied to concrete example scenarios") make every behaviour task test-first. Each slice is written as:

- **RED**: write the named failing tests (given/when/then). Run them and confirm they fail *for the stated reason*, not an import error or a typo.
- **GREEN**: the minimum production code that turns exactly those tests green.
- **REFACTOR**: tidy with the suite green. No new behaviour.
- **VERIFY**: run the wider suite, parity goldens and the named checks.

**Organization**: Tasks are grouped by user story so each story can be implemented and checked as a unit. US1–US3 are all P1 and run in the plan's dependency order (domain → import → dashboard).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: The user story this task belongs to (US1–US5)
- Every task names its exact file path(s)

## Path Conventions

- Backend: `alt-stack/backend/src/tla/…`, tests `alt-stack/backend/tests/…`
- Frontend: `alt-stack/frontend/src/…`, tests `alt-stack/frontend/tests/…`
- Parity reference: `parity/fixtures/…`, `parity/golden/…`, `parity/generator/…`
- The .NET reference under `src/` and `tests/` is **read-only** for this whole feature (009 FR-003). No task edits a file there, and nothing is added to `TrainingLoadAnalyzer.sln`.

## Standing rules for every task

These apply to every task below. They are listed once rather than repeated.

1. **No mocking.** No `unittest.mock`, `vi.fn` or `vi.mock` (Principle IV as amended). The clock, database path, Strava transport (`httpx.MockTransport`) and the frontend `DashboardApi` are passed in as inputs.
2. **No new dependency.** Backend runtime: `fastapi`, `uvicorn`, `httpx` only; dev: `pytest`. Frontend runtime: `react`, `react-dom` only. Anything else needs a Complexity Tracking entry in plan.md *first* (Principle III).
3. **No invented wording.** Every athlete-facing string comes from the reference or [dashboard-ui.md §5](./contracts/dashboard-ui.md#5-strings-the-reference-renders-parity-checklist). If a needed behaviour is not in the spec, stop and amend the spec (Principle VII). Do not decide in code.
4. **No credential anywhere.** No token or client secret in a log line, exception message, API response, `SyncResult` or tracked file (009 FR-010). Fake tokens look like `test-access-…` / `test-refresh-…`.
5. **No ambient locale.** Never use `locale`, `toLocaleString`, `Intl.NumberFormat` or `toFixed` for a displayed figure (009 FR-017, research R2).
6. **Tolerances** are those in [parity.md §5](./contracts/parity.md#5-tolerances) and nothing else.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create both projects and the parity tool as empty, runnable skeletons.

- [ ] T001 Create the directory skeleton from plan.md "Source Code": `alt-stack/backend/src/tla/{domain,strava,persistence,sync,dashboard,api}/__init__.py`, `alt-stack/backend/src/tla/__init__.py`, `alt-stack/backend/tests/{domain,strava,sync,persistence,dashboard,api,parity}/` (with `__init__.py` in each tests directory so pytest module names do not collide), `alt-stack/frontend/src/{chart,components,theme}/`, `alt-stack/frontend/tests/`, `parity/fixtures/{histories,sync,probes}/`, `parity/golden/{histories,sync,probes}/`, `parity/generator/`
- [ ] T002 Create `alt-stack/backend/pyproject.toml` as a uv project: `requires-python = ">=3.13"`, dependencies `fastapi`, `uvicorn`, `httpx` (nothing else); `[dependency-groups] dev = ["pytest"]`; a src layout with package `tla`; `[tool.pytest.ini_options] testpaths = ["tests"]`, `pythonpath = ["src"]`, `addopts = "-ra --strict-markers"`. Add `alt-stack/backend/.python-version` containing `3.13`. Run `uv lock` and commit `alt-stack/backend/uv.lock`
- [ ] T003 [P] Create `alt-stack/frontend/package.json` (`"type": "module"`, `"private": true`, `engines.node ">=22"`): dependencies `react@^19`, `react-dom@^19`; devDependencies `typescript@^5`, `vite`, `@vitejs/plugin-react`, `vitest`, `jsdom`, `@testing-library/react`, `@testing-library/dom`, `@types/react`, `@types/react-dom`, `@types/node`; scripts `dev: vite`, `build: tsc --noEmit && vite build`, `test: vitest run`. Run `npm install` and commit `alt-stack/frontend/package-lock.json`
- [ ] T004 [P] Create `alt-stack/frontend/tsconfig.json` (`strict: true`, `noUncheckedIndexedAccess: true`, `jsx: react-jsx`, `moduleResolution: bundler`, `include: ["src", "tests"]`) and `alt-stack/frontend/vite.config.ts`: the React plugin; `server.port 5173`; `server.proxy` for `/api`, `/connect` and `/strava/callback` → `http://localhost:8000` with `changeOrigin: false` (research R15); `test.environment = "jsdom"`, `test.include = ["tests/**/*.test.{ts,tsx}"]`
- [ ] T005 [P] Create `alt-stack/frontend/index.html`: `<html lang="en">`, `<title>Training Load</title>`, the Google Fonts link for Source Serif 4 at weights 400/600, italic 400 and `display=swap` (copy the exact `<link>` from `src/TrainingLoadAnalyzer.Web/Components/App.razor`), `<div id="root">`, and `<script type="module" src="/src/main.tsx">`
- [ ] T006 [P] Append to the root `.gitignore`, under a `# Alternative stack (feature 009)` heading: `node_modules/`, `alt-stack/frontend/dist/`, `.venv/`, `.pytest_cache/`. Confirm with `git check-ignore alt-stack/backend/.env alt-stack/backend/training-load.db` that the existing `.env` and `*.db` rules already cover the backend's secrets and store (research R6, R14)
- [ ] T007 [P] Create `parity/generator/ParityGenerator.csproj`: an `Exe` targeting `net10.0`, `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, and `ProjectReference`s to `../../src/TrainingLoadAnalyzer.Domain`, `../../src/TrainingLoadAnalyzer.Infrastructure` and `../../src/TrainingLoadAnalyzer.Web`, plus `Microsoft.EntityFrameworkCore.Sqlite` at the version the reference pins (`10.0.12`). Add `parity/generator/Program.cs` that prints `usage` and exits 0. Do **not** add it to `TrainingLoadAnalyzer.sln`. Verify with `dotnet build parity/generator` and `dotnet test` at the repository root (reference still green, 009 FR-003)
- [ ] T008 Smoke-check the three toolchains: `cd alt-stack/backend && uv sync && uv run pytest` (exits 5, "no tests ran"), `cd alt-stack/frontend && npm test -- --passWithNoTests`, and `dotnet run --project parity/generator`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The parity reference (plan Phase 2 outline, step 1), plus the shared test harnesses. Nothing can go red against the reference without the goldens.

**⚠️ CRITICAL**: No user-story work begins until this phase is complete. The goldens are **expectations** generated before any Python or TypeScript production code exists. That way every parity test starts red (Principle I).

### Fixtures (purpose-built, anonymized; constitution "Strava MCP and test data")

- [ ] T009 [P] Write the ported history fixtures in the shape of [parity.md §2](./contracts/parity.md#2-history-fixture): `parity/fixtures/histories/H1.json`, `H2.json`, `H3b.json`, `H3f.json`, `H3g.json`, `consecutive-35.json` and `consecutive-200.json`. Transcribe them from `tests/TrainingLoadAnalyzer.Web.Tests/Fixtures.cs` (`Session`, `Measured`, `H1`, `H2`, `H3b`, `H3f`, `H3g`, `ConsecutiveDays(n)`): the same ids, starts, offsets, moving times, types and heart-rate samples, with `today` `2026-09-18`, `offset` `+03:00` and `maximumHeartRate` as in that file. Include `isStravaConnected`
- [ ] T010 [P] Write the edge history fixtures listed in [parity.md §2](./contracts/parity.md#2-history-fixture):
  - `parity/fixtures/histories/year-boundary-w53.json`: activities from 2026-12-28 to 2027-01-04, `today` 2027-01-04; 2026 has ISO week 53;
  - `fractional-offset.json`: +05:30 and +05:45 starts between 23:00 and 00:59 local;
  - `future-dated.json`: one session dated after `today`;
  - `negative-form.json`: 21 heavy days, then 21 rest days;
  - `display-ties.json`: loads chosen so that at least one Fitness/Fatigue/Form value, printed to 15 significant digits, ends in `…x5`. Search candidate values with a throwaway script in the session scratchpad, not in the repo;
  - `one-second-hr.json`: one 60-minute session with a 3,600-sample 1 Hz series (research R1);
  - `empty.json` (connected) and `empty-unconnected.json`: no activities;
  - `short-history.json`: fewer than 30 days, for the "Not enough data" state.
- [ ] T011 [P] Write `parity/fixtures/probes/display.json`: the 13 probe values from research R2 (at least 45.25, 0.15, 12.349999999999999, −0.25, −13.25, −0.04 as double and as decimal, 0.05, 2.675), percentage probes (−0.004, 0.005, 0.0049, −0.005, 0.3333), week-change probes (−0.04, 0, 120), durations (0 s, 59 s, 45 min, 1 h 5 min, 10 h) and days
- [ ] T012 [P] Write the sync scenario fixtures in the shape of [parity.md §4](./contracts/parity.md#4-sync-fixture-and-golden), one directory each under `parity/fixtures/sync/<name>/`, with `scenario.json` plus `responses/*.json`:
  - `first-import`, `incremental-lookback`, `rate-limited-midway`, `budget-exhausted-on-success`, `transient-5xx-then-ok`, `unauthorized-401`;
  - `reconcile-deletion`, `sport-type-changed-to-ebike`, `streams-404-manual`, `hr-dropouts`;
  - `utc-offset-float`, `utc-offset-not-whole-minute`, `expired-token`.

  Each scenario fixes the UTC and local clock, the connection row, the initial activity rows and sync state, and the ordered expected requests with their status, `X-ReadRateLimit-Limit`/`-Usage` headers and body file. Every id is `fixture-…`, and every token is `test-access-…`/`test-refresh-…`. Every scenario except `expired-token` MUST set the connection's `expires_at` to at least the scenario's UTC clock + 1 h. Then the new implementation's renewal (research R10) issues no extra request, and the request sequences stay comparable (T065). Base the response shapes on those in `tests/TrainingLoadAnalyzer.Infrastructure.Tests/` (`SyncHarness`, `StubHttpMessageHandler` and the JSON in `StravaJsonQuirksTests`, `IncrementalSyncTests`, `RateLimitTests`, `ReconciliationTests`).

### Golden generator (.NET, reads `src/` read-only)

- [ ] T013 Implement fixture loading in `parity/generator/Fixtures.cs`: parse a history fixture into `TrainingActivity` values through the reference's own public constructors, so a fixture the reference refuses fails loudly. Parse a sync `scenario.json` into a stub `HttpMessageHandler` that replays responses in order and records each request's method and URL, plus seed rows for an in-memory SQLite `ImportDbContext` (mirror `tests/TrainingLoadAnalyzer.Infrastructure.Tests/Fakes/SqliteFixture.cs` and `StubHttpMessageHandler.cs`; copy them, do not reference the test project)
- [ ] T014 Implement `parity/generator/HistoryGolden.cs`, which writes `parity/golden/histories/<name>.json` in the shape of [parity.md §3](./contracts/parity.md#3-history-golden):
  - `loads`, `daily` and `weekly` from `TrainingLoadAggregator`, over the view's 180-day range;
  - `metrics` from `TrainingMetricsCalculator`;
  - `trend` from `TrainingLoadTrendCalculator`;
  - `view`, which is `DashboardViewBuilder.Build`, projected to exactly [http-api.md §2](./contracts/http-api.md#2-get-apidashboard), camelCase, through `Display`, including the qualifier rule;
  - `geometry` for windows 180, 90 and 30, from `MetricsChart.Plot/LoadBars/ZeroRule/AxisTicks(…, 6)/HoverSlots`;
  - `renderedText`, from `HtmlRenderer` rendering `MetricRow`, `RecentActivityList` and `MetricsChartView` per window, as whitespace-normalised text;
  - `renderedText.rail` and `renderedText.content`, from rendering `Pages/Dashboard.razor` with a `DashboardReader` over the fixture seeded into in-memory SQLite (copy `tests/TrainingLoadAnalyzer.Web.Tests/Fakes/FixtureContextFactory.cs`): the `<aside class="rail">` text, and the main column's populated, empty or unavailable state as the fixture produces it (009 SC-002, "every surface listed in 008 SC-003").

  Write decimals with `ToString(CultureInfo.InvariantCulture)` and doubles with `ToString("R", InvariantCulture)`. Set `CultureInfo.DefaultThreadCurrentCulture = InvariantCulture` at startup.
- [ ] T015 Implement `parity/generator/ProbeGolden.cs`, which writes `parity/golden/probes/display.json`: each probe through `Display.Metric`, `Display.Points`, `Display.Percent`, `Display.Duration` and `Display.Day`, plus the week-change caption rule (`(change >= 0 ? "+" : "") + Display.Points(change)`), plus `SyncMessage.For` for every row of the [http-api.md §3](./contracts/http-api.md#3-get-apisyncstatus-and-post-apisync) message table, using the fixed local clock `2026-09-18T…+03:00`. Also write `parity/golden/probes/surfaces.json` (whitespace-normalised text, [parity.md §3](./contracts/parity.md#3-history-golden)):
  - `loading`: `Pages/Dashboard.razor` with a reader that never completes (copy `Fakes/PendingContextFactory.cs`);
  - `syncPanel.<row>`: `SyncPanel` rendered with each message-table `SyncStatus`;
  - `notFound`: `Pages/NotFound.razor`;
  - `error`: `Pages/Error.razor` with no request ID, and with the "Development Mode" block stripped before writing, since it is not ported (Amendment 1(c)).
- [ ] T016 Implement `parity/generator/SyncGolden.cs`, which runs each sync scenario through the reference's `StravaAuthorization` + `StravaActivitySync` (the same wiring as `Program.cs`), with the stub handler and an in-memory SQLite database. It writes `parity/golden/sync/<name>.json`: the final activity rows as domain values, the sync state, the `SyncResult`, the `SyncMessage` text and the issued request sequence. Make sure no token value is written into the golden
- [ ] T017 Wire `parity/generator/Program.cs` to regenerate all goldens with no arguments. It deletes and rewrites `parity/golden/` only, and exits non-zero if any fixture fails to load. Then run `dotnet run --project parity/generator` and commit `parity/golden/**`
- [ ] T018 VERIFY the goldens against the reference's own hand-written expectations. Spot-check that:
  - `golden/histories/H1.json` `view` agrees with the assertions in `tests/TrainingLoadAnalyzer.Web.Tests/DashboardViewBuilderTests.cs`;
  - `golden/probes/display.json` reproduces the research R2 table (45.25 → `45.3`, −0.04 double → `-0.0`, −0.04 decimal → `0.0`, −0.4 % → `0%`, 0.5 % → `+1%`);
  - `golden/sync/expired-token.json` has outcome `ReconnectionRequired` (research R3(1)).

  Also confirm `grep -r "test-access\|test-refresh" parity/golden` finds nothing, and record the golden count in `parity/README.md` (purpose, layout, regenerate command, "never hand-edit").

### Shared test harnesses (test code only, no production behaviour)

- [ ] T019 [P] Create `alt-stack/backend/tests/golden.py`: `load_history(name)`, `load_sync(name)`, `load_probes()`, `history_fixture(name)` and `sync_scenario(name)`, which resolve `../../parity/{golden,fixtures}` from the file's own path; plus `assert_decimal_close(a, b, tol=Decimal("1e-20"))` and `assert_float_close(a, b, tol=1e-4)`, per [parity.md §5](./contracts/parity.md#5-tolerances)
- [ ] T020 [P] Create `alt-stack/backend/tests/fakes.py`: `FixedClock(now_local, now_utc)`, defaulting to 2026-09-18 in UTC+03:00 as the reference's `FixedLocalClock` does (research R16), with a `set(…)` for tests that advance time; and `scenario_transport(scenario)`, which returns `(httpx.MockTransport, requests_log)`. The transport replays a sync scenario's responses in order and fails the test on an unexpected request. It is a transport stub, not a mock (plan, Principle IV)
- [ ] T021 [P] Create `alt-stack/frontend/tests/golden.ts`: typed readers for `../../parity/golden/histories/*.json` via `node:fs` and `new URL(…, import.meta.url)`; plus `expectCoordsClose(actual, expected, 0.01)`, which parses `"x,y x,y …"` and number strings and compares them element-wise (research R8)

**Checkpoint**: The goldens are committed and reviewed, and the harnesses exist. Every parity test written from here on starts red.

---

## Phase 3: User Story 1 — The same figures from the same training (Priority: P1) 🎯 MVP

**Goal**: Pure Python domain modules compute loads, daily and weekly totals, Fitness/Fatigue/Form and weekly trends identically to the reference (009 FR-005 – FR-008), plus the `display` formatter the dashboard will need (research R2).

**Independent Test**: `cd alt-stack/backend && uv run pytest tests/domain tests/parity/test_history_domain.py tests/dashboard/test_display.py`. Every hand-computed case ported from `tests/TrainingLoadAnalyzer.Domain.Tests/` passes, every history golden's `loads/daily/weekly/metrics/trend` matches within tolerance, and every refusal names the same rule. No UI, no Strava and no storage are involved.

### Activity, heart rate and load (001)

- [ ] T022 [P] [US1] RED: port every case of `HeartRateSeriesTests.cs` into `alt-stack/backend/tests/domain/test_heart_rate.py`. Given an empty sample list, then non-ascending times, then a sample of 19 or 251 bpm, when a `HeartRateSeries` is built, then a `ValueError` is raised whose message names the rule and the offending index/value, worded as the reference's (001 FR-021, FR-022). Given samples 20 and 250 bpm, then the series is accepted. Given a list mutated after construction, then the series is unchanged
- [ ] T023 [P] [US1] RED: port `TrainingActivityCreationTests.cs` and `TrainingActivityValidationTests.cs` into `alt-stack/backend/tests/domain/test_activity.py`. Given a blank or whitespace id, a naive `datetime`, `None` start, moving time of 0 or negative, or a type that is not `ActivityType`, when a `TrainingActivity` is built, then each raises a `ValueError` with its own reference message (001 FR-017 – FR-024). Given valid inputs, then all fields read back and the object is frozen (assigning raises). Given `ActivityType`, then only `RUNNING` and `CYCLING` exist, displayed as `Running`/`Cycling`
- [ ] T024 [P] [US1] RED: port `HeartRateZoneTests.cs`, `MeasuredTrainingLoadTests.cs` and `EstimatedTrainingLoadTests.cs` into `alt-stack/backend/tests/domain/test_training_load.py`. Given each zone boundary (50/60/70/80/90 % of max HR, compared as `bpm * 100 >= pct * max_hr`), then the Edwards weight is 1–5, and 0 below 50 %. Given samples 60 s apart, then TRIMP equals the hand-computed value from the reference test **exactly** (`Decimal` equality). Given no series and 45 min moving, then points are `Decimal("90")` with provenance `ESTIMATED`. Given a series, then provenance is `MEASURED`. Given a `TrainingLoad`, then there is no accessor for points without provenance (001 SC-007)
- [ ] T025 [US1] GREEN: implement the minimum to pass T022–T024:
  - `alt-stack/backend/src/tla/domain/heart_rate.py`: `HeartRateSample`, `HeartRateSeries` (a copy to tuple), `heart_rate_zone_weight`;
  - `alt-stack/backend/src/tla/domain/activity.py`: `ActivityType`, `LoadProvenance`, `TrainingLoad`, `TrainingActivity`, `training_load(activity, max_hr)`;
  - `alt-stack/backend/src/tla/domain/_decimal.py`: the one module-level `decimal.Context(prec=34, rounding=ROUND_HALF_EVEN)`, and `minutes(td) = Decimal(td // timedelta(microseconds=1)) / Decimal(60_000_000)` under that context (research R1).

  All are `@dataclass(frozen=True, slots=True)`.
- [ ] T026 [US1] REFACTOR: align names with data-model.md §1, and remove any duplication between the measured and estimated paths in `alt-stack/backend/src/tla/domain/activity.py` without changing behaviour. Run `uv run pytest tests/domain`

### Ranges, ISO weeks and aggregation (002)

- [ ] T027 [P] [US1] RED: port `DateRangeTests.cs` and `IsoWeekTests.cs` into `alt-stack/backend/tests/domain/test_date_range.py` and `alt-stack/backend/tests/domain/test_iso_week.py`:
  - given a `None` start *and* an end before any start, then the "missing bound" error wins; the order is load-bearing (002 FR-021);
  - given end < start, then a `ValueError` names the rule with dates as `YYYY-MM-DD`;
  - given 2026-12-31, then `IsoWeek` is 2026-W53 with Monday 2026-12-28;
  - given 2027-01-01, then it is 2026-W53;
  - given 2021-01-03, then it is 2020-W53;
  - weeks are ordered by `monday`, never by `(year, week)` (004 research R9);
  - the designation format is `2026-W38`.
- [ ] T028 [P] [US1] RED: port `DailyAggregationTests.cs`, `WeeklyAggregationTests.cs` and `LoadBasisTests.cs` into `alt-stack/backend/tests/domain/test_aggregation.py`:
  - given sessions at +05:30 and +05:45 near local midnight, then each lands on its **local** calendar day (the `fix/strava-utc-offset-decimal` case);
  - given a range with empty days, then each empty day has points `0`, count 0 and basis `NONE`;
  - given a range starting mid-week, then weekly output extends to whole ISO weeks (002 FR-013, FR-017);
  - given measured plus estimated sessions on a day, then the basis is `MIXED` (the `combine(any_measured, any_estimated)` rule, 002 FR-014);
  - given a session outside the range, then it is excluded.
- [ ] T029 [US1] GREEN: implement:
  - `alt-stack/backend/src/tla/domain/date_range.py` (`DateRange` with `days()`);
  - `alt-stack/backend/src/tla/domain/iso_week.py` (`IsoWeek` from `date.isocalendar()`, a `sunday` property, a `designation` property);
  - `alt-stack/backend/src/tla/domain/aggregation.py` (`LoadBasis`, `DailyTrainingLoad`, `WeeklyTrainingLoad`, `aggregate_daily(activities, range, max_hr)`, `aggregate_weekly(…)`, which chunks the extended daily series into sevens).

  Keep a local `combine` helper in this module; do not extract it (004 research R12, plan Constitution Check III).
- [ ] T030 [US1] REFACTOR `alt-stack/backend/src/tla/domain/aggregation.py`, and run `uv run pytest tests/domain`

### Fitness, Fatigue and Form (003)

- [ ] T031 [P] [US1] RED: port `TrainingMetricsTests.cs`, `MetricsSeriesTests.cs`, `MetricsBasisTests.cs` and `MetricsInputTests.cs` into `alt-stack/backend/tests/domain/test_metrics.py`:
  - given a constant daily load, then Fitness and Fatigue follow `x += (load − x) × (1 − exp(−1/k))` with k = 42 and 7, seeded at 0, to within 1e-4 of the reference's hand-computed values;
  - `form == fitness − fatigue` is a property, never stored (003 FR-003);
  - `is_reliable` is false for the first 42 days and true from day 43 (003 warm-up);
  - `form_basis == fitness_basis` (003 FR-019b);
  - given a history with a gap, or one not covering the range, then the refusal names the rule with `YYYY-MM-DD` dates, as the reference does.
- [ ] T032 [US1] GREEN: implement `alt-stack/backend/src/tla/domain/metrics.py`:
  - `DailyTrainingMetrics`, with `form` and `form_basis` as properties;
  - `calculate_metrics(history, range)`, with smoothing factors `1 - math.exp(-1 / 42)` and `1 - math.exp(-1 / 7)` as doubles, and the daily load entering as `float(points)` (data-model.md §1 "Arithmetic");
  - its own local basis `combine` helper.
- [ ] T033 [US1] REFACTOR `alt-stack/backend/src/tla/domain/metrics.py`, and run `uv run pytest tests/domain`

### Weekly trends (004)

- [ ] T034 [P] [US1] RED: port `WeeklyLoadTrendTests.cs`, `TrendClassificationTests.cs`, `TrendInputTests.cs` and `TrendReliabilityTests.cs` into `alt-stack/backend/tests/domain/test_trends.py`:
  - given an incomplete (in-progress) week, then the classification is `INDETERMINATE` whatever the change; completeness is read first;
  - given |absolute change| < `Decimal("50")`, then it is `STEADY`, whatever the relative change (the floor is read second);
  - given a change ≥ 50 and relative ≥ `Decimal("0.15")`, then it is `SIGNIFICANT_INCREASE`, and symmetrically `SIGNIFICANT_DECREASE`;
  - given a previous week of 0, then `relative_change` is `None`;
  - `absolute_change`, `relative_change` and `classification` are properties (004 FR-007 – FR-019);
  - the basis combines this week's and the previous week's;
  - refusals match the reference's rules and wording.
- [ ] T035 [US1] GREEN: implement `alt-stack/backend/src/tla/domain/trends.py`: `TrendClassification`, `WeeklyLoadTrend`, the private constants `_ABSOLUTE_FLOOR = Decimal("50")` and `_RELATIVE_THRESHOLD = Decimal("0.15")`, `calculate_trends(history, range)` and a local basis `combine`
- [ ] T036 [US1] REFACTOR `alt-stack/backend/src/tla/domain/trends.py`, and run `uv run pytest tests/domain`

### Independence and parity against the goldens

- [ ] T037 [P] [US1] RED→GREEN: write `alt-stack/backend/tests/domain/test_independence.py`. Given every module under `tla/domain/`, when its imports are walked with `ast`, then none imports `tla.*` outside `tla.domain`, nor `httpx`, `sqlite3`, `fastapi`, `json`, `os` or `time`; the only stdlib imports are `decimal`, `datetime`, `math`, `enum`, `dataclasses`, `typing` and `collections.abc`; and no identifier or string contains `strava` in any casing (Principle II, 009 FR-008, FR-020). It should pass immediately. If it does not, fix the domain, not the test
- [ ] T038 [P] [US1] RED: write `alt-stack/backend/tests/parity/test_history_domain.py`, parametrized over every `parity/golden/histories/*.json`. Given the fixture, when `training_load`, `aggregate_daily`, `aggregate_weekly`, `calculate_metrics` and `calculate_trends` run, then:
  - `loads`, `daily` and `weekly` points and the trend's `absoluteChange` match within 1e-20;
  - counts, bases, provenance, ISO year/week/Monday, completeness and classification match exactly;
  - Fitness and Fatigue match within 1e-4;
  - `relativeChange` matches its `null`-ness exactly and its value within 1e-20.

  Cover `one-second-hr` explicitly (research R1), and SC-001. Also add `alt-stack/backend/tests/parity/test_refusals.py`: given each invalid input from 009 US1 scenario 6 (end before start, a 19 bpm sample, a history with a gap), then a `ValueError` is raised whose message equals the reference's message (copy the literal from the reference test).
- [ ] T039 [US1] GREEN: fix whatever the parity tests in T038 expose, in the owning `alt-stack/backend/src/tla/domain/*.py` module. Each fix must be justified by a spec rule or a golden, never by "matching the numbers". If a golden disagrees with the 001–004 specification, stop and raise a spec amendment (Principle VII); do not special-case it

### Display formatting (research R2; needed by US3, pure and domain-adjacent)

- [ ] T040 [P] [US1] RED: port `tests/TrainingLoadAnalyzer.Web.Tests/DisplayFormatTests.cs` into `alt-stack/backend/tests/dashboard/test_display.py`, and parametrize over `parity/golden/probes/display.json`:
  - given 45.25, 0.15, 12.349999999999999, −0.25 and −13.25, then `metric()` returns `45.3`, `0.2`, `12.4`, `-0.3`, `-13.3`;
  - `metric(-0.04)` is `-0.0` (the double keeps its sign);
  - `points(Decimal("-0.04"))` is `0.0` (the decimal drops the sign of zero);
  - `metric(None)` and `points(None)` are `—`;
  - `percent(Decimal("-0.004"))` is `0%`, and `percent(Decimal("0.005"))` is `+1%`;
  - `duration` and `day` match every probe;
  - the week-change caption for `Decimal("-0.04")` is `0.0` with no sign.

  Also write `alt-stack/backend/tests/dashboard/test_display_locale.py` (SC-007). A fixture calls `locale.setlocale(locale.LC_ALL, "fi_FI.UTF-8")` and restores the previous locale afterwards; the test is skipped, with the reason given, if that locale is not installed. Given the Finnish locale, every probe's output is unchanged. (US3's T071 extends this file to `to_json(view)` for H3f.) Setting `LC_ALL` in the environment alone proves nothing, because Python ignores it until `setlocale` is called.
- [ ] T041 [US1] GREEN: implement `alt-stack/backend/src/tla/dashboard/display.py`:
  - `metric(x: float | None)`: `format(x, ".15g")`, then `Decimal.quantize(Decimal("0.1"), ROUND_HALF_UP)`, keeping the sign;
  - `points(d: Decimal | None)`: `quantize(Decimal("0.1"), ROUND_HALF_UP)`, dropping the sign of a zero result;
  - `percent(fraction: Decimal | None)`: ×100, `quantize(Decimal("1"), ROUND_HALF_UP)`, `+` when > 0, then `%`, and `—` when `None`;
  - `duration(td)` and `day(date)`: as `Display.cs`;
  - `week_change(d)`.

  No `locale`, no f-string float formatting.
- [ ] T042 [US1] VERIFY: `uv run pytest` (whole backend) green under the default locale and under `LANG=fi_FI.UTF-8 LC_ALL=fi_FI.UTF-8`. `git diff --stat src tests` is empty (009 FR-003). Record in `specs/009-python-react-stack/parity-report.md` (create it with the headings "Tolerances", "Deviations", "Display ties found", "Requirement map", "Measurements") any display tie found by `display-ties` (Amendment 1(a))

**Checkpoint**: US1 is complete. The domain and the formatter are proven against the reference with no UI, network or storage.

---

## Phase 4: User Story 2 — Connect Strava and sync, with the same safeguards (Priority: P1)

**Goal**: The Strava connect flow, credential storage and renewal, mapping, heart-rate retrieval, incremental sync with look-back, reconciliation, rate-limit and retry handling, the single-sync guard and the summary, identical to feature 005 as the reference implements it, except token renewal, which follows the spec (Amendment 1(b)1, research R10). Tested at its boundary with recorded responses (Principle V).

**Independent Test**: `uv run pytest tests/strava tests/persistence tests/sync tests/parity/test_sync_parity.py`. Every sync golden except `expired-token` matches exactly: stored rows, sync state, `SyncResult`, message and the request sequence (SC-003). `expired-token` completes by renewing. There is no network and no real credential.

**Depends on**: US1's `TrainingActivity`/`HeartRateSeries` (T025). T043–T052 (Strava shapes, rate limit and client) do not need the domain, and can start in parallel with Phase 3.

### Strava shapes, rate limit and client (`tla/strava/`)

- [ ] T043 [P] [US2] RED: port `StravaJsonQuirksTests.cs` into `alt-stack/backend/tests/strava/test_shapes.py`:
  - given an `id` as a JSON number larger than 2^53, then `StravaActivitySummary.external_id` is its verbatim decimal string;
  - given `utc_offset` 10800 or `10800.0`, then it is accepted as 10800;
  - given missing `has_heartrate`, `manual`, `private` or `trainer`, then the reference's defaults apply;
  - unknown keys are ignored;
  - `start_date_local` and `type` are never read: a body lacking them parses, and changing them changes nothing;
  - given a stream body without `heartrate`, then `StravaStreamSet.heartrate is None`;
  - `StravaTokens` parses `access_token`, `refresh_token`, `expires_at`, an optional `scope` and `athlete.id`.
- [ ] T044 [P] [US2] RED: port the header and retry-time cases of `RateLimitTests.cs` into `alt-stack/backend/tests/strava/test_rate_limit.py`:
  - given `x-readratelimit-limit: 100,1000` and `-usage: 100,340` in any header casing, then `is_exhausted` is true and `retry_after(now_utc)` is the next quarter hour (e.g. 07:07Z → 07:15Z; exactly 07:15Z → 07:30Z, matching the reference);
  - given daily usage at 1000, then retry is the next UTC midnight;
  - given missing or malformed headers, then the status is `None`/not exhausted, as the reference does (005 FR-034, FR-035).
- [ ] T045 [P] [US2] RED: port the client cases from `RateLimitTests.cs`, `ImportedHistoryTests.cs` and `StravaJsonQuirksTests.cs` into `alt-stack/backend/tests/strava/test_client.py`, using `scenario_transport`:
  - the activities list is requested with `per_page=200`, `after` and `page`;
  - given 503, 503, 200, then the call succeeds after exactly three attempts, with 10 ms and 20 ms backoff (inject a `sleep` callable and record the delays, no real sleep);
  - given 503 three times, then `StravaRequestFailed(503)`;
  - given a transport error, then it is retried like a 5xx;
  - given 400 or 401, then there is no retry and `StravaRequestFailed(status)` is raised. The sync, not the client, treats a 401 as reconnection-required, as `StravaActivitySync.cs:129` does;
  - given 429, then `StravaRateLimited(status)`, with no retry;
  - given a 2xx, or a 404 from the streams endpoint, whose `X-ReadRateLimit-*` headers show the budget exhausted, then `StravaRateLimited(status)` is raised and **no page is returned**. The reference discards the response ("stop before the limit is exceeded, not after", `StravaApiClient.cs`);
  - given 404 on the streams endpoint, then `None`;
  - the `Authorization: Bearer …` header value never appears in any exception message.
- [ ] T046 [P] [US2] RED: port the OAuth cases of `ConnectionTests.cs` into `alt-stack/backend/tests/strava/test_oauth.py`:
  - the authorize URL has `client_id`, `redirect_uri`, `response_type=code`, `scope=read,activity:read_all` and `state`, in the reference's order;
  - code exchange and refresh POST `client_id`, `client_secret`, `code`/`refresh_token` and `grant_type` as form fields;
  - given a 400/401 on refresh, then `StravaTokenRejected`;
  - no exception message or `repr` of `StravaTokens` contains the token values (005 FR-005).
- [ ] T047 [US2] GREEN: implement:
  - `alt-stack/backend/src/tla/strava/shapes.py` (`StravaActivitySummary.from_json`, `StravaStreamSet.from_json`, `StravaTokens.from_json`, with `repr=False` on the token fields);
  - `alt-stack/backend/src/tla/strava/errors.py` (`StravaRateLimited`, `StravaRequestFailed`, `StravaTokenRejected`, `InsufficientScope(requested, granted)`, `ReconnectionRequired(athlete_id)`, with messages copied from `src/TrainingLoadAnalyzer.Infrastructure/Strava/*Exception.cs` and `StravaRequestFailures.cs`). `StravaTokenRejected` is raised only by `oauth.py` (a rejected exchange or refresh), never by the API client;
  - `alt-stack/backend/src/tla/strava/rate_limit.py` (`RateLimitStatus`).
- [ ] T048 [US2] GREEN: implement `alt-stack/backend/src/tla/strava/client.py` (`StravaApiClient(http: httpx.Client, sleep=time.sleep)`, with `list_activities(access_token, after, page)` and `get_streams(access_token, id)`) and `alt-stack/backend/src/tla/strava/oauth.py` (`StravaOAuthClient(http, client_id, client_secret)`, with `authorize_url`, `exchange`, `refresh`), porting `StravaApiClient.cs` and `StravaOAuthClient.cs`
- [ ] T049 [US2] REFACTOR `alt-stack/backend/src/tla/strava/`, and run `uv run pytest tests/strava`

### Mapper (pure; needs US1 domain types)

- [ ] T050 [P] [US2] RED: port `ActivityMappingTests.cs` into `alt-stack/backend/tests/strava/test_mapper.py`:
  - given each `sport_type` in the reference's fixed table, then the mapped `ActivityType` matches it (e.g. `Run`, `TrailRun`, `VirtualRun` → Running; `Ride`, `GravelRide`, `VirtualRide` → Cycling; copy the table from `StravaActivityMapper.cs`);
  - given `EBikeRide`, `Walk`, `Swim` or an unknown type, then `SkippedActivity(id, UNSUPPORTED_TYPE)`, with the reason named as the reference's `SkipReason`;
  - given `start_date` `…Z` with `utc_offset` 19800, then `started_at` is aware with an offset of +05:30;
  - given `utc_offset` 19800.5 or 50401 (outside ±14 h), then `SkippedActivity(id, UNUSABLE_BY_DOMAIN)` (research R17);
  - given `10800.0`, then it is accepted;
  - given moving time 0, then `UNUSABLE_BY_DOMAIN`;
  - `to_series`: given samples with bpm outside 20–250, or repeated time values, then they are discarded and counted, and fewer than 2 survivors return `None` (005 FR-017f, FR-017g).
- [ ] T051 [US2] GREEN: implement `alt-stack/backend/src/tla/strava/mapper.py`: the fixed sport-type table, `SkipReason`, `MappedActivity`, `SkippedActivity`, `map_activity(summary, series)` and `to_series(streams) -> (HeartRateSeries | None, discarded_count)`, porting `StravaActivityMapper.cs` and `src/TrainingLoadAnalyzer.Infrastructure/Sync/SkippedActivity.cs`. Catch the domain's `ValueError` into `UNUSABLE_BY_DOMAIN`, as the reference catches `ArgumentException`
- [ ] T052 [US2] REFACTOR `alt-stack/backend/src/tla/strava/mapper.py`, and run `uv run pytest tests/strava`

### Persistence (`tla/persistence/`)

- [ ] T053 [P] [US2] RED: port `PersistenceRoundTripTests.cs` into `alt-stack/backend/tests/persistence/test_activity_store.py`, using a temp-file database (`tmp_path / "t.db"`):
  - given a fresh file, when `open_database(path)` runs, then `PRAGMA user_version` is 1 and the three tables match data-model.md §3 exactly (column names, `NOT NULL`, `PRIMARY KEY (provider, external_id)`, index `activity_started`);
  - given it runs twice, then it is idempotent;
  - given an activity with a +05:45 start, a 1 Hz series and `heart_rate_outstanding`, when it is upserted then read, then it reads back equal as a domain `TrainingActivity` (005 FR-024);
  - given a re-import of the same `(provider, external_id)`, then there is one row, updated in place (005 FR-023, FR-030);
  - given a stored series and a re-import **without** a series, then the stored series is kept, via `COALESCE(activity.heart_rate_json, excluded.heart_rate_json)` (005 FR-017b);
  - given a row whose `heart_rate_json` violates the domain (e.g. 300 bpm) or is malformed JSON, when it is read, then `ActivityRow.to_domain()` raises at the boundary (`ValueError` or `json.JSONDecodeError`);
  - queries: `between(start_utc, end_utc)` ordered by start; `latest_start()`; `earliest_outstanding_start(since_utc)`.
- [ ] T054 [P] [US2] RED: write `alt-stack/backend/tests/persistence/test_connection_store.py`:
  - given no connection, then `get()` returns `None`;
  - given a saved connection, when the database is reopened from the same path, then it is still there (009 US2 scenario 1, "survives a restart");
  - given a new token pair, when it is saved, then both the access and refresh tokens are replaced together (005 C48);
  - `repr()` of the connection row contains no token.
- [ ] T055 [US2] GREEN: implement:
  - `alt-stack/backend/src/tla/persistence/schema.py`: an ordered `MIGRATIONS` list holding migration 1, verbatim from data-model.md §3; `open_database(path) -> sqlite3.Connection`, which applies the pending migrations under `user_version`;
  - `alt-stack/backend/src/tla/persistence/rows.py`: `ActivityRow` with `from_domain` and `to_domain`, instants as integer UTC microseconds plus offset minutes (research R6), the series as `[[us_from_start, bpm], …]`; `ConnectionRow`; `SyncStateRow`;
  - `alt-stack/backend/src/tla/persistence/activity_store.py`: `ActivityStore(conn)` and `ConnectionStore(conn)`.

  Use one connection per unit of work, with no module-level connection.
- [ ] T056 [US2] REFACTOR `alt-stack/backend/src/tla/persistence/`, and run `uv run pytest tests/persistence`

### Authorization (connect + renewal) (`tla/sync/authorization.py`)

- [ ] T057 [P] [US2] RED: port `ConnectionTests.cs` into `alt-stack/backend/tests/sync/test_authorization.py`:
  - given a token response granting `read,activity:read_all`, when `exchange(code)` runs, then the connection is stored with its expiry, scopes and `connected_at` from the clock;
  - given a grant missing `activity:read_all`, then `InsufficientScope(requested, granted)` is raised, nothing is stored, and the message names the missing scope (009 US2 scenario 2);
  - given a held connection for athlete 1 and a callback for athlete 2, then `ReconnectionRequired`/mismatch, and the held connection is unchanged (005 FR-008);
  - given the same athlete again, then the tokens are replaced.

  Add these cases for research R10:
  - given `expires_at` ≤ now + 60 s, when `ensure_fresh()` runs, then it POSTs a refresh and stores **both** new tokens;
  - given `expires_at` > now + 60 s, then no request is made;
  - given the refresh is rejected (400/401), then `ReconnectionRequired` and nothing is stored.
- [ ] T058 [US2] GREEN: implement `alt-stack/backend/src/tla/sync/authorization.py`: `StravaAuthorization(conn_store, oauth, clock)` with `exchange(code)`, `refresh()` and `ensure_fresh(margin=timedelta(seconds=60))`, porting `StravaAuthorization.cs` plus the specified renewal

### The sync walk (`tla/sync/activity_sync.py`)

- [ ] T059 [P] [US2] RED: port `IncrementalSyncTests.cs` and `ImportedHistoryTests.cs` into `alt-stack/backend/tests/sync/test_activity_sync.py`, using `scenario_transport` and a temp-file database:
  - given an empty store, when a sync runs, then pages are read until an empty page, only running and cycling activities are stored, and `SyncResult.imported` equals the count, with skipped activities listed with reasons (009 US2 scenario 3);
  - given activities older than 180 days before today, then no stream request is made for them; inside the window it is made (005 FR-017a);
  - given a manual activity, or one with `has_heartrate` false, then no stream request is made;
  - given a later sync, then `after` = resume point, which is `latest start − 7 days` (009 US2 scenario 5), and no stored session is duplicated;
  - given an outstanding series inside 180 days that is earlier than the latest start, then the resume point is its start − 7 days (005 FR-017e);
  - the resume point is never before the epoch (005 FR-028, FR-029).
- [ ] T060 [P] [US2] RED: port `RateLimitTests.cs` (sync level) and `ReconciliationTests.cs` into `alt-stack/backend/tests/sync/test_sync_stops_and_reconciles.py`:
  - given a 429 on page 2, then page-1 sessions are kept, nothing is removed, the resume point reflects only what was stored, the outcome is `RateLimited` and `retry_after` is the next quarter hour (009 US2 scenario 4);
  - given an exhausted budget on a successful response, then the same;
  - given a 401 on list, then `ReconnectionRequired` and stored activities are untouched (009 US2 scenario 6);
  - given a transport failure after retries, then `Interrupted`, with the store unchanged;
  - given a span read **to completion** in which a stored id no longer appears, then it is removed with `RemovalReason` as the reference names it (005 FR-031 – FR-031e);
  - given a span read only partially, then nothing is removed;
  - given a stored activity whose sport type changed to `EBikeRide`, then it is removed with the reference's reason;
  - given `hr-dropouts`, then the discarded sample counts are reported in `SyncResult.discarded`.
- [ ] T061 [US2] GREEN: implement:
  - `alt-stack/backend/src/tla/sync/results.py`: `SyncOutcome`, `RemovalReason`, `RemovedActivity`, `DiscardedSamples` and `SyncResult`, carrying no credential;
  - `alt-stack/backend/src/tla/sync/activity_sync.py`: `ActivitySync(conn, client, authorization, clock)` with `run() -> SyncResult`, calling `authorization.ensure_fresh()` before the first request (research R10), then the walk, `_reconcile`, `_record_state`, ported from `StravaActivitySync.cs`.

  Sync failures are **outcomes**, never exceptions escaping `run()` (Principle VI). Log each outcome with `logging.getLogger("tla.sync")`, with no token.
- [ ] T062 [US2] REFACTOR `alt-stack/backend/src/tla/sync/activity_sync.py`, keeping the method-to-method correspondence with `StravaActivitySync.cs` for review (plan, Structure Decision), and run `uv run pytest tests/sync`

### Coordinator (single-sync guard and process-held status)

- [ ] T063 [P] [US2] RED: port `SyncCoordinatorTests.cs` and `SyncMessageTests.cs` into `alt-stack/backend/tests/sync/test_coordinator.py`:
  - given no sync has run, then `status.is_running` is false and `result is None`;
  - given a sync blocked inside the walk (a transport that waits on a `threading.Event`), when a second `run()` is called from another thread, then it returns the current running status immediately, without queuing, and exactly one walk ran (009 US2 scenario 7, and the two-tab edge case);
  - given no connection, then the status `failure` is `"Strava connection required."` and no request is made;
  - after completion, then `finished_at` is local time from the clock, and `retry_after_local` is set for `RateLimited`.

  In `alt-stack/backend/tests/sync/test_sync_message.py`, parametrize every row of the [http-api.md §3](./contracts/http-api.md#3-get-apisyncstatus-and-post-apisync) message table against `parity/golden/probes/display.json`: `Syncing activities…`, `3 activities imported.`, `Already up to date.`, `Rate limited by Strava. Available again at 07:15.`, `Sync interrupted. Your stored history is unchanged — try again.`, and so on.
- [ ] T064 [US2] GREEN: implement `alt-stack/backend/src/tla/sync/coordinator.py` (`SyncStatus`, `SyncCoordinator` with a `threading.Lock` acquired `blocking=False`, and `run(make_sync)`, `status`) and `alt-stack/backend/src/tla/dashboard/sync_message.py` (`for_status(status) -> str`, verbatim from `SyncMessage.cs`, with times formatted `HH:mm` by hand, not via locale)

### Parity against the sync goldens

- [ ] T065 [US2] RED: write `alt-stack/backend/tests/parity/test_sync_parity.py`, parametrized over `parity/golden/sync/*.json`. Given the scenario's clock, initial rows and replayed responses, when `ActivitySync.run()` runs, then these match the golden **exactly**: the final activity rows (as domain values), the sync state (resume point, last outcome), the `SyncResult` (counts, skipped with reasons, removed with reasons, discarded counts, outcome, retry-after), `for_status` text and the issued request sequence (SC-003). Handle `expired-token` as a separate test with a comment naming Amendment 1(b)1: it asserts one refresh POST, then a completed sync, and that the golden's `ReconnectionRequired` outcome is *not* matched ([parity.md §4](./contracts/parity.md#4-sync-fixture-and-golden))
- [ ] T066 [US2] GREEN: fix whatever T065 exposes, in the owning module under `alt-stack/backend/src/tla/{strava,persistence,sync}/`. Each fix must cite a 005 requirement or a golden
- [ ] T067 [US2] VERIFY:
  - `uv run pytest` is green;
  - `grep -rniE "test-(access|refresh)" alt-stack/backend/src` is empty;
  - a test in `alt-stack/backend/tests/sync/test_no_credentials.py` runs every sync scenario with `caplog` at DEBUG and asserts that no captured log record, and no `repr(SyncResult)`, contains a token value from the scenario (009 FR-010);
  - `alt-stack/backend/tests/test_layering.py` walks imports and asserts that `tla.domain` imports only itself, that nothing outside `tla.strava` and `tla.sync` imports `tla.strava`, and that `tla.dashboard` and `tla.api` never import `httpx` (Principles II and V, 009 FR-020).

  Add the `expired-token` deviation to `specs/009-python-react-stack/parity-report.md` under "Deviations".

**Checkpoint**: US2 is complete. Import behaves as the reference does, proven on recorded responses, with renewal as specified.

---

## Phase 5: User Story 3 — The Broadsheet dashboard, unchanged (Priority: P1)

**Goal**: The backend builds the whole `DashboardView` with every displayed string formatted, and serves it with the sync and connect routes ([http-api.md](./contracts/http-api.md)). The React page renders the Broadsheet dashboard from it, with client-side windowing and geometry, a CSS-only hover readout, and the sync interaction ([dashboard-ui.md](./contracts/dashboard-ui.md)).

**Independent Test**:
- `uv run pytest tests/dashboard tests/api tests/parity/test_view_parity.py`: every history golden's `view` matches the API output exactly.
- `npm test`: every golden's `geometry` matches within 0.01, and its `renderedText` matches exactly (SC-002).
- Quickstart journeys 4–9 pass by hand.

**Depends on**: US1 (domain and `display`) and US2 (store, coordinator, authorization).

### View builder and reader (`tla/dashboard/`)

- [ ] T068 [P] [US3] RED: port `DashboardViewBuilderTests.cs`, `WeeklyLoadAndTrendTests.cs`, `RecentActivitiesTests.cs` and `EmptyAndPartialHistoryTests.cs` into `alt-stack/backend/tests/dashboard/test_view_builder.py`:
  - given H1 and today 2026-09-18, when `build_dashboard_view(activities, today, max_hr, connected)` runs, then the metrics span the 180 days ending today, gap-free and ascending, and `current` holds the last day;
  - given a future-dated session, then the range extends to include it, as the reference does;
  - `recent` holds at most 7, newest first;
  - `has_enough_history_for_chart` is `len(metrics) >= 30` over the 180-day series;
  - given no activities, then `has_activities` is false and `current` is `None`;
  - the trend precondition matches the reference (no trend when the previous week is missing);
  - the qualifier rule: when not reliable → `"still settling"`, then Mixed → `"partly estimated"`, Estimated → `"estimated"`, in that order.
- [ ] T069 [P] [US3] RED: port `DashboardReaderTests.cs` into `alt-stack/backend/tests/dashboard/test_reader.py`:
  - given a temp database with stored activities, when `read_dashboard(db_path, clock, settings)` runs, then the view equals `build_dashboard_view` on the same activities;
  - given a row whose series is corrupt (300 bpm), malformed JSON, or a value that overflows, then `is_unavailable` is true and nothing else is populated, and one `WARNING` log record names the failure class and no token (`ValueError`, `json.JSONDecodeError`, `OverflowError`, data-model.md §5);
  - given a held connection, then `is_strava_connected` is true.
- [ ] T070 [US3] GREEN: implement `alt-stack/backend/src/tla/dashboard/view_builder.py` (`DashboardView`, `RecentActivity`, `build_dashboard_view`, porting `DashboardViewBuilder.cs`, `DashboardView.cs`, `RecentActivity.cs` and `MetricRow.Qualifiers`) and `alt-stack/backend/src/tla/dashboard/reader.py` (`read_dashboard`, porting `DashboardReader.cs`)
- [ ] T071 [US3] RED: write `alt-stack/backend/tests/dashboard/test_view_json.py` and `alt-stack/backend/tests/parity/test_view_parity.py`:
  - given a view, when `to_json(view)` runs, then the dict has exactly the keys and nesting of [http-api.md §2](./contracts/http-api.md#2-get-apidashboard), camelCase;
  - every displayed value is a `str` produced by `display.py`, and `days[*].fitness/fatigue/form/load` are the only numbers;
  - `maximumHeartRate` is a string (e.g. `"190"`);
  - `week.trend` is `None` → `null`;
  - parametrized over every history golden, the JSON equals the golden's `view` **exactly** for every string and boolean, with the raw numbers within tolerance (SC-002);
  - in `alt-stack/backend/tests/dashboard/test_display_locale.py` (from T040), under the Finnish locale, `to_json(view)` for H3f equals the default-locale output (SC-007).
- [ ] T072 [US3] GREEN: implement `to_json(view)` in `alt-stack/backend/src/tla/dashboard/view_builder.py` (or `alt-stack/backend/src/tla/dashboard/view_json.py` if the builder grows past one screen), and fix the parity failures in the owning module
- [ ] T073 [US3] REFACTOR `alt-stack/backend/src/tla/dashboard/`, and run `uv run pytest tests/dashboard tests/parity`

### Settings, clock, app factory and routes (`tla/api/`, `tla/main.py`)

- [ ] T074 [P] [US3] RED: port `StartupTests.cs` into `alt-stack/backend/tests/api/test_settings.py`:
  - given `TLA_ATHLETE_MAXIMUM_HEART_RATE` unset or blank, when `Settings.from_env(env)` runs, then it raises with exactly the "is not configured" message from [http-api.md §5](./contracts/http-api.md#5-startup-refusal);
  - given `abc`, `0`, `-5` or `180.5`, then it raises the "is '{value}', which is not a positive whole number…" message;
  - given `190`, then `maximum_heart_rate == 190`;
  - `TLA_DATABASE_PATH` defaults to `training-load.db` in the backend directory, and a missing client id or secret does not refuse start-up;
  - a subprocess test runs `uv run uvicorn tla.main:app --factory --workers 1` with the variable unset, and asserts a non-zero exit, the message on stderr, and no port bound (quickstart §3, 009 FR-018).
- [ ] T075 [US3] GREEN: implement `alt-stack/backend/src/tla/settings.py` (`Settings.from_env(env: Mapping[str, str])`, no settings library, 009 FR-019), `alt-stack/backend/src/tla/clock.py` (`SystemClock` with `now_local()`/`now_utc()`, using the server's local zone, research R16), and the skeleton of `alt-stack/backend/src/tla/main.py` (`create_app(settings, clock, transport=None)`, and `app()` as the `--factory` entry that reads `os.environ` and refuses before binding)
- [ ] T076 [P] [US3] RED: write `alt-stack/backend/tests/api/test_dashboard_route.py` with `TestClient(create_app(settings, FixedClock(), transport))` and a temp database:
  - given a seeded H3f store, when `GET /api/dashboard` runs, then 200 and a body equal to `to_json(read_dashboard(…))`;
  - given a corrupt row, then 200 with `isUnavailable: true` (never a 5xx for a read failure);
  - the response body, as text, contains no stored token (009 FR-010);
  - it makes no request to the transport (009 FR-013).
- [ ] T077 [P] [US3] RED: write `alt-stack/backend/tests/api/test_sync_routes.py`:
  - given no sync ever, then `GET /api/sync/status` → `{"isRunning": false, "message": "", "needsConnection": false, "lastChecked": null}`, or `needsConnection` true when there is no connection;
  - given a `first-import` scenario transport, then `POST /api/sync` blocks until done and returns `message` `"{n} activities imported."` and `lastChecked` `"2026-09-18 HH:mm"`;
  - given a sync held open by an `Event`-gated transport, when a second `POST /api/sync` arrives from another thread, then it returns 200 immediately with `isRunning: true` and `"Syncing activities…"`, and exactly one walk ran;
  - given `unauthorized-401`, then `needsConnection` is true;
  - no response contains a token.
- [ ] T078 [P] [US3] RED: port `ConnectEndpointTests.cs` into `alt-stack/backend/tests/api/test_connect_routes.py`, following the order in [http-api.md §4](./contracts/http-api.md#4-get-connect-and-get-stravacallback):
  - `GET /connect` → 302 to Strava authorize, with `redirect_uri` built from the request's scheme and `Host`, and the cookie `tla.oauth.state` = 32 lowercase hex characters, `HttpOnly`, `SameSite=Lax`, `Max-Age=600`, with no `Secure` over http;
  - the callback with `error` → the cookie is deleted, then 302 `/?connect=declined`;
  - a missing or mismatched state → 400 `This sign-in could not be verified. Start again from the dashboard.`, and the transport receives **no** token request;
  - a missing `code` → 400 `Strava returned no authorization code.`;
  - a successful exchange → 302 `/`, and the connection is stored;
  - insufficient scope → 302 `/?connect=scope`;
  - a different athlete → 302 `/?connect=mismatch`.
- [ ] T079 [US3] GREEN: implement:
  - `alt-stack/backend/src/tla/api/dashboard.py` (`GET /api/dashboard`);
  - `alt-stack/backend/src/tla/api/sync.py` (`GET /api/sync/status`, `POST /api/sync`, both sync `def` endpoints; `SyncStatusView` per [http-api.md §3](./contracts/http-api.md#3-get-apisyncstatus-and-post-apisync), with `needsConnection = failure is not None or outcome == ReconnectionRequired` and `lastChecked` formatted `yyyy-MM-dd HH:mm` by hand);
  - `alt-stack/backend/src/tla/api/connect.py` (`/connect`, `/strava/callback`, with the state compared by `hmac.compare_digest`).

  Wire them in `create_app`, with one `SyncCoordinator` on `app.state`, and `httpx.Client(transport=transport)` built per sync.
- [ ] T080 [P] [US3] RED→GREEN: write `alt-stack/backend/tests/api/test_spa_fallback.py`:
  - given a `frontend_dist` directory containing `index.html` and `assets/x.js` passed to `create_app`, then `GET /` and `GET /nowhere` return `index.html`, `GET /assets/x.js` returns the file, and `GET /api/unknown` returns 404 JSON, not the SPA;
  - given no `dist` directory, then the API routes still work.

  Implement this in `alt-stack/backend/src/tla/api/static.py` (research R15, run mode).
- [ ] T081 [US3] REFACTOR `alt-stack/backend/src/tla/api/` and `alt-stack/backend/src/tla/main.py`, and run `uv run pytest`

### Frontend contract types, API client, windowing and geometry

- [ ] T082 [P] [US3] Write `alt-stack/frontend/src/types.ts`: `DashboardView`, `Day`, `Recent`, `Current`, `Week`, `Trend` and `SyncStatusView`, mirroring [http-api.md](./contracts/http-api.md) field for field; and the `DashboardApi` interface `{ fetchDashboard(): Promise<DashboardView>; fetchSyncStatus(): Promise<SyncStatusView>; postSync(): Promise<SyncStatusView> }` (data-model.md §6). This is types only, with no behaviour to test
- [ ] T083 [P] [US3] RED: write `alt-stack/frontend/tests/api.test.ts`. Given a `fetch` function passed in as a plain async function (not `vi.fn`), when `createApi(fetchImpl)` calls `/api/dashboard`, `/api/sync/status` and `POST /api/sync`, then it returns the parsed body. Given a rejected fetch or a non-2xx response, then it throws `Unavailable`
- [ ] T084 [P] [US3] RED: write `alt-stack/frontend/tests/window.test.ts`:
  - `WINDOWS` is `[30, 90, 180]`, with default 180;
  - given 200 days, `windowed(days, 30)` returns the trailing 30 in order;
  - given 20 days, `windowed(days, 90)` returns all 20.
- [ ] T085 [P] [US3] RED: port `MetricsChartTests.cs` into `alt-stack/frontend/tests/geometry.test.ts`, and parametrize over every history golden's `geometry` for windows 180, 90 and 30. Given the windowed `days`, when these run:
  - `viewBox(1000, 300)`;
  - `plot` (Fitness, Fatigue, Form polylines, with the metric band taken over the **windowed** series);
  - `loadBars`;
  - `zeroRule`;
  - `axisTicks(days, 6)`;
  - `hoverSlots` (left, width, barTop, barHeight, load, per-series label, value and top; `opens-left` past 55 %);

  then each coordinate string matches within 0.01 via `expectCoordsClose`, and tick labels, counts and `null`-ness match exactly (research R8, [parity.md §5](./contracts/parity.md#5-tolerances)).
- [ ] T086 [US3] GREEN: implement `alt-stack/frontend/src/api.ts` (`createApi(fetchImpl = fetch): DashboardApi`, `class Unavailable`), `alt-stack/frontend/src/window.ts` and `alt-stack/frontend/src/chart/geometry.ts`: a pure port of `src/TrainingLoadAnalyzer.Web/Features/Dashboard/MetricsChart.cs`, rounding with `Math.round(v * 100) / 100` and printing with `String()`. It must not use `toFixed` or `toLocaleString`
- [ ] T087 [US3] REFACTOR `alt-stack/frontend/src/chart/geometry.ts`, and run `npm test`

### Theme and components

- [ ] T088 [US3] Copy `src/TrainingLoadAnalyzer.Web/wwwroot/Theme/broadsheet.css` to `alt-stack/frontend/src/theme/broadsheet.css` byte for byte. Prepend a provenance comment naming the source path, the commit `git rev-parse HEAD`, and "copied, not referenced (009 FR-004); edit only by re-copying". Copy each `.razor.css` from `src/TrainingLoadAnalyzer.Web/Components/`, each with the same header:
  - `Pages/Dashboard.razor.css` → `alt-stack/frontend/src/components/Dashboard.css`. It holds the rail and the page frame (the rail is `<aside class="rail">` in `Pages/Dashboard.razor`), and is imported by `Dashboard.tsx` and `Rail.tsx`;
  - `Layout/MainLayout.razor.css` → `alt-stack/frontend/src/theme/layout.css`, imported once by `main.tsx`;
  - `Dashboard/MetricRow.razor.css` → `components/MetricRow.css`;
  - `Dashboard/MetricsChartView.razor.css` → `components/MetricsChart.css`;
  - `Dashboard/RecentActivityList.razor.css` → `components/RecentActivities.css`;
  - `Dashboard/SyncPanel.razor.css` → `components/SyncPanel.css`;
  - `Pages/NotFound.razor.css` → `components/NotFound.css`;
  - `Pages/Error.razor.css` → `components/ErrorPage.css`;
  - `Layout/ReconnectModal.razor.css` is **not** copied (Amendment 1(c)).

  In every copied file, replace Blazor's `::deep` with the plain descendant combinator only; change nothing else. Also copy the global rules from `wwwroot/app.css` that the page depends on (focus ring, `#blazor-error-ui` → `.error-ui`) into `alt-stack/frontend/src/theme/app.css`, with the same header
- [ ] T089 [P] [US3] RED: port the `MetricRow` cases of `DashboardComponentTests.cs` into `alt-stack/frontend/tests/MetricRow.test.tsx`:
  - given `current` and `week` from the H3f golden `view`, when `<MetricRow>` renders, then the labels "Fitness", "Fatigue", "Form", "This week", the values and the qualifiers ("still settling", "estimated", "partly estimated") appear in order;
  - the week caption reads `{change} ({percent})` and the judgement;
  - given `trend: null`, then `—`;
  - the element's whitespace-normalised `textContent` equals the golden's `renderedText.metricRow`.
- [ ] T090 [P] [US3] RED: port `RecentActivitiesTests.cs` into `alt-stack/frontend/tests/RecentActivities.test.tsx`:
  - the heading is "Recent activities", with the column headings "Day", "Type", "Moving time", "Basis", "Load";
  - each row's `provenance` renders as the text tag "measured"/"estimated", not colour only (008 non-colour rule);
  - given `[]`, then "Nothing recorded yet.";
  - `textContent` equals the golden's `renderedText.recent`.
- [ ] T091 [P] [US3] RED: port the chart cases of `DashboardComponentTests.cs` and `InformationPreservationTests.cs` into `alt-stack/frontend/tests/MetricsChart.test.tsx`:
  - given the 180-day H3f days and window 30, when `<MetricsChart>` renders, then the heading is "Daily load and metrics · last 30 days";
  - `svg.plot` has `viewBox="0 0 1000 300"`, `preserveAspectRatio="none"` and `aria-label="Daily training load with fitness, fatigue and form"`;
  - there are `g.bars > rect.load-bar`, and the polylines appear in DOM order Form, Fatigue, Fitness, with the reference's classes;
  - the legend shows "Fitness", "Fatigue", "Form";
  - `.hover-layer` holds one `.day` per day, and each slot already contains its readout text: the day as `YYYY-MM-DD` and the labels "Fitness", "Fatigue", "Form", "Load", with values from `days[*].display` (009 US3 scenario 3);
  - given the chart rendered inside `Dashboard` with a `DashboardApi` whose calls are counted (a plain object, not `vi.fn`), when `pointermove`, `mouseover` and `mouseenter` are fired on every `.day` slot, then the call count and `container.innerHTML` are unchanged: no state change and no request per pointer movement (009 FR-015). jsdom does not evaluate `:hover`, so the readout's visibility itself is checked in T097's manual pass;
  - given `hasEnoughHistoryForChart` false, then "Not enough data to show trends (30+ days required)";
  - `textContent` equals the golden's `renderedText.chart30`, `chart90` and `chart180` for the respective windows.
- [ ] T092 [P] [US3] RED: port the rail and `SyncPanel` cases of `DashboardComponentTests.cs` into `alt-stack/frontend/tests/Rail.test.tsx` and `alt-stack/frontend/tests/SyncPanel.test.tsx`:
  - the rail has the one `h1` "Training Load"; the region headings "As of", "Window", "Strava", "Max heart rate"; "ISO week 2026-W38"; "190 bpm"; and "from configuration";
  - the window control is `role="radiogroup"` `aria-label="Chart window"`, with three radios labelled "30 days", "90 days", "180 days" and 180 checked by default;
  - `SyncPanel`, given `isRunning: false`, shows an enabled "Sync Activities" button;
  - given `isRunning: true`, the button reads "Syncing…" and is `disabled`, with the message "Syncing activities…";
  - given `needsConnection`, it shows a "Connect Strava" `<a href="/connect">`;
  - given `lastChecked`, it shows "Last checked 2026-09-18 07:15";
  - the rail's whitespace-normalised `textContent` equals the golden's `renderedText.rail`, and each `SyncPanel` state equals `parity/golden/probes/surfaces.json` `syncPanel.<row>` (009 SC-002).
- [ ] T093 [US3] GREEN: implement `alt-stack/frontend/src/components/{MetricRow,RecentActivities,MetricsChart,Rail,SyncPanel}.tsx`, each a pure function of its props, with markup and classes ported from the matching `.razor` file so the copied CSS applies unchanged ([dashboard-ui.md §4](./contracts/dashboard-ui.md#4-chart), §6). The only inline `style`s carry the hover slots' `left`/`width`/`top` percentages. Each component imports its own `.css`

### Dashboard container, window switching and sync interaction

- [ ] T094 [P] [US3] RED: write `alt-stack/frontend/tests/Dashboard.test.tsx`, passing a plain-object `DashboardApi` whose methods return promises the test resolves by hand (a deferred helper in `alt-stack/frontend/tests/deferred.ts`, not `vi.fn`):
  - given `fetchDashboard` resolves with the H3f golden `view`, then the metric row, chart and recent list render;
  - when the athlete clicks "30 days", then the heading changes to "· last 30 days", the bar count equals 30, and the API received **no** further call (count calls in the plain object) (009 US3 scenarios 2–3);
  - when "Sync Activities" is clicked, then **before** `postSync` resolves, the button is disabled and reads "Syncing…" and the message is "Syncing activities…" (Amendment 1(b)2);
  - when `postSync` resolves, then the panel renders the returned status and `fetchDashboard` is called exactly once more, with its result replacing the view;
  - given `fetchSyncStatus` returns `isRunning: true` on load, then the running state shows and no polling call follows (research R9);
  - `document.title` is "Training Load".
- [ ] T095 [US3] GREEN: implement `alt-stack/frontend/src/components/Dashboard.tsx` (it owns the fetch lifecycle, the chosen window and the sync-in-flight flag; data-model.md §6), `alt-stack/frontend/src/App.tsx` (routing by `location.pathname`: `/` → `Dashboard`, anything else → the not-found placeholder filled in US4) and `alt-stack/frontend/src/main.tsx` (mounts `<App api={createApi()} />` and imports `theme/broadsheet.css` and `theme/app.css`)
- [ ] T096 [US3] REFACTOR `alt-stack/frontend/src/components/`, and run `npm test` and `npm run build`
- [ ] T097 [US3] VERIFY:
  - `uv run pytest` and `npm test` are green;
  - run mode (`npm run build`, then Uvicorn serving `dist`) and development mode (quickstart §4) both load;
  - walk quickstart §5 journeys 4, 5, 6, 8 and 9 against a live or seeded store, checking in the devtools Network tab that switching windows and hovering issue no request;
  - reload mid-sync in a second tab, and it shows the running state (009 US3 scenario 5).

  Record any string mismatch as a failing test first, then fix it.

**Checkpoint**: US3 is complete. The populated dashboard and sync interaction match the reference, string for string.

---

## Phase 6: User Story 4 — Every non-populated state, and every device and appearance (Priority: P2)

**Goal**: The loading, unavailable, empty (connected and unconnected), reconnection-required, rate-limited, not-found and error states, in the Broadsheet language, plus 008's responsive, contrast, focus, touch-target, non-colour and light/dark rules, and the locale runs (SC-006, SC-007).

**Independent Test**: `npm test` (state and theme tests) and `uv run pytest`, both green, and green again under `LANG=fi_FI.UTF-8 LC_ALL=fi_FI.UTF-8`. Quickstart §6 visual pass at 320, 768, 1280 and 2560 px in both appearances.

**Depends on**: US3's components and the copied stylesheet.

### States

- [ ] T098 [P] [US4] RED: port `EmptyAndPartialHistoryTests.cs` and the state cases of `DashboardComponentTests.cs` into `alt-stack/frontend/tests/DashboardStates.test.tsx`:
  - given `fetchDashboard` still pending, then the content column shows "Reading your training history…", the rail shows the browser's local date and its ISO week, and max heart rate `—`, **not** `0 bpm` (Amendment 1(c), [dashboard-ui.md §1](./contracts/dashboard-ui.md#1-page-lifecycle));
  - given `isUnavailable: true`, then "Data unavailable" and the exact reference paragraph;
  - given `fetchDashboard` rejects with `Unavailable`, then the same notice, no digit anywhere in the content column, and no stale figures (009 FR-016);
  - given a rejection after a good load, then the rail keeps the last good values and the content column shows the notice;
  - given `!hasActivities` and connected, then "No activities recorded" and "Your Strava account is connected, but nothing has been imported yet. Sync to bring your training in.";
  - given unconnected, then "Connect your Strava account to bring your training in." and a "Connect Strava" link to `/connect`;
  - given the `unauthorized-401` status, then "Strava connection required." and a Connect link;
  - given a rate-limited status, then "Rate limited by Strava. Available again at 07:15.";
  - each state's content column, as whitespace-normalised `textContent`, equals `renderedText.content` of the `empty` (connected), `empty-unconnected`, `short-history` and `H3f` goldens, and the loading state equals `surfaces.json` `loading`. The loading rail is compared with one substitution, `—` for `0 bpm` (Amendment 1(c)).
- [ ] T099 [P] [US4] RED: write `alt-stack/frontend/tests/isoWeek.test.ts`. Given 2026-12-31 and 2027-01-01, then `isoWeek(d)` designates `2026-W53`; given 2021-01-03, then `2020-W53`; given 2026-09-18, then `2026-W38` (the year-boundary cases from T027). The result must not depend on the process time zone: run it under `TZ=Pacific/Kiritimati` and `TZ=America/Adak` too
- [ ] T099a [US4] GREEN: implement `alt-stack/frontend/src/isoWeek.ts` (UTC-safe arithmetic, no `Intl`). Add the loading, unavailable and empty branches to `alt-stack/frontend/src/components/Dashboard.tsx`, and the loading-state rail (the browser's local date and `isoWeek` of it) to `alt-stack/frontend/src/components/Rail.tsx`
- [ ] T100 [P] [US4] RED: port `ReconnectModalTests.cs` *as an exclusion*, plus the not-found and error cases, into `alt-stack/frontend/tests/NotFoundAndErrors.test.tsx`:
  - given `location.pathname` `/nowhere` or `/not-found`, when `<App>` renders, then "Not Found" and "Sorry, the content you are looking for does not exist." appear in the broadsheet layout, with `document.title` "Not found";
  - given a child that throws during render, when it is wrapped in `<ErrorBoundary>`, then "An unhandled error has occurred.", a "Reload" control and a "🗙" dismiss control appear;
  - given `location.pathname` `/Error`, when `<App>` renders, then "Error." and "An error occurred while processing your request." appear, with `document.title` "Error", **no** "Request ID" line (the reference omits it when there is no ID; the SPA never has one, [dashboard-ui.md §1](./contracts/dashboard-ui.md#1-page-lifecycle)) and **no** "Development Mode" text;
  - the not-found and error pages' whitespace-normalised `textContent` equal `parity/golden/probes/surfaces.json` `notFound` and `error` (009 SC-002);
  - no rendered surface contains "Rejoining the server" (Amendment 1(c)).
- [ ] T101 [US4] GREEN: implement `alt-stack/frontend/src/components/NotFound.tsx`, `alt-stack/frontend/src/components/ErrorPage.tsx` and `alt-stack/frontend/src/components/ErrorBoundary.tsx` (a class component; `Reload` calls `location.reload()`, and 🗙 hides the notice). Route to them in `alt-stack/frontend/src/App.tsx` (`/Error` → `ErrorPage`, any other path except `/` → `NotFound`), and wrap `Dashboard` in the boundary in `alt-stack/frontend/src/main.tsx`. `ErrorPage` takes no request ID and renders no Request ID line. There is no backend request-ID mechanism (spec Amendment 1(c), as decided 2026-09-23)
- [ ] T102 [US4] REFACTOR `alt-stack/frontend/src/components/` and `alt-stack/frontend/src/App.tsx`, and run `npm test`

### Theme, layout and accessibility (008 FR-015 – FR-021, SC-004 – SC-010)

- [ ] T103 [P] [US4] RED: port `tests/TrainingLoadAnalyzer.Web.Tests/Theme/BroadsheetTokens.cs`, `ContrastRatio.cs`, `ContrastRatioTests.cs`, `PaletteContrastTests.cs` and `PaletteSlotTests.cs` into `alt-stack/frontend/tests/theme/tokens.ts`, `alt-stack/frontend/tests/theme/contrast.ts` and `alt-stack/frontend/tests/theme/palette.test.ts`:
  - parse `src/theme/broadsheet.css` for the light `:root` tokens and the `@media (prefers-color-scheme: dark)` tokens;
  - every text/background pair the reference checks meets the same WCAG ratio (4.5 : 1 for text, 3 : 1 for UI and graphics), in both appearances;
  - the palette slots exist, with the same names.
- [ ] T104 [P] [US4] RED: port `ColourDisciplineTests.cs` into `alt-stack/frontend/tests/theme/colourDiscipline.test.ts`: given every file matching `src/**/*.css` and `src/**/*.tsx` except `src/theme/broadsheet.css`, then none contains a hex colour, `rgb(`, `hsl(`, `oklch(` or a named CSS colour. Given every `style={…}` in `src/**/*.tsx`, then it sets only `left`, `width` or `top` ([dashboard-ui.md §6](./contracts/dashboard-ui.md#6-styling))
- [ ] T105 [P] [US4] RED: port `ResponsiveRulesTests.cs` and `InteractiveControlTests.cs` into `alt-stack/frontend/tests/theme/responsive.test.ts` and `alt-stack/frontend/tests/theme/interactive.test.ts`:
  - parsing the component CSS, the rail stacks below the reference's tablet breakpoint;
  - no rule sets a fixed width that would overflow 320 px;
  - every interactive control (button, the radio labels, the links) has a `min-height`/`min-width` ≥ 48 px, or padding reaching it, and a visible `:focus-visible` rule;
  - light/dark is a `prefers-color-scheme` media query with no script (008 FR-021);
  - `index.html` has no inline theme script, so the correct appearance applies from the first paint (009 US4 scenario 3).
- [ ] T106 [US4] GREEN: fix any failure from T103–T105 **by re-copying from the reference** or correcting a component's markup or class. Never edit the copied tokens, which would break 009 FR-004's provenance. If the reference itself fails a ported rule, record it in `specs/009-python-react-stack/parity-report.md` and raise it with the developer instead of diverging
- [ ] T107 [US4] VERIFY (SC-006, SC-007):
  - add an npm script `test:fi` = `LANG=fi_FI.UTF-8 LC_ALL=fi_FI.UTF-8 vitest run` to `alt-stack/frontend/package.json`. Node reads the locale from the environment at process start, so it cannot be switched from inside a test;
  - add `alt-stack/frontend/tests/locale.test.ts`, which records `new Intl.NumberFormat().format(1.5)`. Under `test:fi` it asserts `"1,5"`, so a run where the locale did not take effect fails loudly (select the branch with `process.env.LC_ALL`). In both runs it asserts that the `geometry` output for H3f equals the golden;
  - `LANG=fi_FI.UTF-8 LC_ALL=fi_FI.UTF-8 uv run pytest` (including `test_display_locale.py`, not skipped), `npm test` and `npm run test:fi` are all green, with pass counts identical to the default-locale runs;
  - walk quickstart §5 journeys 1, 2, 3, 10 and 11, and the whole §6 visual pass (320/768/1280/2560 px, 200 % zoom, light and dark switched live, tab focus visible, 48 × 48 targets), and note the results in `specs/009-python-react-stack/parity-report.md`.

**Checkpoint**: US4 is complete. Every state and device constraint holds.

---

## Phase 7: User Story 5 — A like-for-like comparison of the two stacks (Priority: P3)

**Goal**: The developer can judge the experiment: every functional requirement of features 001–008 is mapped to a test or excluded with a reason, the SC-008 measurements are recorded for both stacks, and every tolerance and deviation is listed.

**Independent Test**: Review `specs/009-python-react-stack/parity-report.md` against 009 US5 scenarios 1–2. Every FR row has a passing test or a reason, and both suites pass from clean checkouts.

**Depends on**: US1–US4.

- [ ] T108 [US5] Build the requirement map in `specs/009-python-react-stack/parity-report.md` § "Requirement map": one row per functional requirement in `specs/001-*/spec.md` to `specs/008-*/spec.md`, including their amendments, with the columns FR id, one-line summary, and the new implementation's test(s) (`path::test_name`), *or* "excluded" plus the reason. Exclusions are only those in 009 FR-001 (stack-named requirements) and Amendment 1(c). Write a throwaway script in the session scratchpad that lists every `FR-\d+[a-z]?` per spec, and confirm the map has none missing
- [ ] T109 [P] [US5] Fill § "Tolerances" and § "Deviations" in `specs/009-python-react-stack/parity-report.md`:
  - the six tolerance rows of [parity.md §5](./contracts/parity.md#5-tolerances);
  - Amendment 1(a) with any display ties found (T042);
  - 1(b)1 token renewal (`expired-token`);
  - 1(b)2 in-flight state in the clicking tab;
  - 1(b)3 the recorded `?connect=` gap;
  - 1(c) the four stack-specific surfaces, including the loading rail's `—` for `0 bpm` (the one substitution in the `surfaces.json` `loading` comparison) and the `/Error` page's Request ID line, which is never shown because the SPA has no server-side render to fail;
  - research R8 (geometry to 0.01);
  - R17 (non-whole-minute offsets).

  Each row names the test that pins it.
- [ ] T110 [P] [US5] Measure and record SC-008 in `specs/009-python-react-stack/parity-report.md` § "Measurements", for both stacks on the same machine, with the command used for each:
  - suite wall time: `time dotnet test` against `time (uv run pytest && npm test)`;
  - runtime dependency counts: the reference's `PackageReference`s in `src/**/*.csproj` against `pyproject.toml` `dependencies` + `package.json` `dependencies`, both direct and resolved (`uv tree`, `npm ls --omit=dev --all`);
  - bytes downloaded before first paint, cold load from devtools with the cache disabled, `/` on each app;
  - SC-004 first meaningful content for `consecutive-200` seeded, on each app;
  - SC-005 an incremental sync with a handful of new activities, timed against the live account or the `incremental-lookback` scenario, and labelled with which one.

  Mark each result PASS or FAIL. SC-004 passes when first meaningful content arrives in ≤ 2 s **and** no later than the reference's median of 3 cold loads (measure the new implementation the same way). SC-005 passes at ≤ 10 s. A FAIL is reported to the developer; it is not tuned away silently.
- [ ] T111 [US5] Write § "TDD assessment" in `specs/009-python-react-stack/parity-report.md`: where RED preceded GREEN and where it did not, with examples from the git history (`git log --oneline -- alt-stack`); where a golden rather than a hand-written test caught a defect; any mocking temptation and how it was avoided. This is for the developer's own judgement (SC-008); state facts, and leave the verdict to the developer
- [ ] T112 [US5] VERIFY (009 US5 scenario 2), from clean state:
  - `git clean -xdn alt-stack` reviewed;
  - `dotnet test` at the root is green (FR-003);
  - `cd alt-stack/backend && uv sync --frozen && uv run pytest` is green;
  - `cd alt-stack/frontend && npm ci && npm test && npm run build` is green;
  - `grep -rn "TrainingLoadAnalyzer\|\.razor\|\.cs\"" alt-stack/backend/src alt-stack/frontend/src` finds only provenance comments, and there are no shared files or imports between the stacks (FR-004).

**Checkpoint**: US5 is complete. The comparison is ready for the developer's review.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, the constitution completion review and the final sweep.

- [ ] T113 [P] Write `alt-stack/README.md`: prerequisites, the `.env` variables from research R14 (with the note "never commit; git-ignored"), the development and run commands with `--workers 1` and why, the test and locale commands, where parity lives, and "do not point `TLA_DATABASE_PATH` at the reference's database" (009 FR-019, FR-022). Link it from the root `README.md` with one line under a heading naming feature 009. This is a documentation-only edit, and does not touch the reference's code
- [ ] T114 [P] Sweep for credentials and secrets. `git grep -nE "(access|refresh)_token\s*[:=]\s*['\"][a-f0-9]{20,}"`, `git grep -n client_secret -- alt-stack parity` (fake values only), and `git ls-files | grep -E "\.env$|\.db$"` all find nothing. Confirm no `print(` in `alt-stack/backend/src` (logging only, Principle VI)
- [ ] T115 [P] Sweep for simplicity: list every module, class and function in `alt-stack/backend/src/tla` and `alt-stack/frontend/src` that has a single caller and exists only as an abstraction (not a port of a reference unit). Remove it or justify it in plan.md Complexity Tracking (Principle III). Confirm the three basis `combine` helpers are still separate (004 R12)
- [ ] T116 Run the quickstart end to end ([quickstart.md](./quickstart.md) §1–§8) on a clean clone. Fix any step that does not work as written, in the quickstart itself if it is the document that is wrong
- [ ] T117 Write the constitution completion review as the final section of `specs/009-python-react-stack/parity-report.md`: an explicit check of Principles I–VII and the "Alternative-stack experiment" constraints, one line each with evidence (test path or command). The checks cover in particular: strict TDD (I); domain independence (`test_independence.py`, `test_layering.py`) (II); no unnecessary abstraction (T115) (III); no mocking library (`git grep -nE "unittest\.mock|vi\.fn|vi\.mock" alt-stack` is empty) (IV); nothing merged into `main` (merge rule). List the follow-ups from research R3: run `/speckit-bug-assess` against `main` for the token renewal and the in-flight sync state

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup. It **blocks every story**, because the goldens must exist before any parity test can be red.
- **US1 (Phase 3)**: depends on Foundational only.
- **US2 (Phase 4)**: depends on Foundational. T043–T049 (the Strava shapes, rate limit, client and OAuth) can run **in parallel with US1**. T050 onward needs US1's T025 (`TrainingActivity`, `HeartRateSeries`).
- **US3 (Phase 5)**: depends on US1 (domain, `display`) and US2 (store, authorization, coordinator, `sync_message`). The frontend tasks T082–T093 depend only on the goldens and can start as soon as Phase 2 ends, in parallel with US1/US2 backend work.
- **US4 (Phase 6)**: depends on US3's components (T093, T095) and the copied theme (T088). T103–T105 (the theme tests) need only T088.
- **US5 (Phase 7)**: depends on US1–US4.
- **Polish (Phase 8)**: depends on US5 (T117 reads its report).

### User Story Dependency Graph

```text
Setup ─► Foundational (goldens) ─┬─► US1 domain ──┬─► US2 mapper … coordinator ─┐
                                 │                │                              ├─► US3 backend ─┐
                                 ├─► US2 strava client (T043–T049) ──────────────┘               ├─► US4 ─► US5 ─► Polish
                                 └─► US3 frontend (T082–T093, goldens only) ─────────────────────┘
```

The three P1 stories are not independent of each other. The plan orders them domain → import → dashboard, because the dashboard displays the domain's figures from the import's store. Each is still **independently testable**: US1 by its domain and parity tests, US2 by recorded responses, and US3 by its API and component tests against goldens.

### Within Each Slice

- RED before GREEN, always. Confirm the failure message before writing production code.
- Ported hand-computed tests first, then golden parity tests, then REFACTOR.
- Backend: domain → strava/persistence → sync → dashboard → api.
- Frontend: types → pure modules (api, window, geometry) → leaf components → `Dashboard` → `App`.
- Commit after each GREEN or REFACTOR, so the git history shows the TDD sequence T111 assesses.

### Parallel Opportunities

- Setup: T003–T007 in parallel after T001/T002.
- Foundational: the fixture tasks T009–T012 in parallel; the harnesses T019–T021 in parallel with the generator T013–T017.
- US1: the RED tasks T022/T023/T024, T027/T028, T031, T034, T037, T040 touch different test files and can be written in parallel. Their GREENs are sequential only where one module imports another (activity → aggregation → metrics/trends).
- US2: T043–T046 (RED) in parallel; T053/T054 in parallel; T059/T060 in parallel.
- US3: T076/T077/T078 (route REDs) in parallel; T083/T084/T085 in parallel; T089–T092 (component REDs) in parallel.
- US4: T098, T099, T100 and T103–T105 in parallel.
- US5: T109 and T110 in parallel.

---

## Parallel Example: User Story 1

```bash
# RED, all at once (different test files):
Task: "T022 [US1] RED heart-rate series refusals in alt-stack/backend/tests/domain/test_heart_rate.py"
Task: "T023 [US1] RED activity creation/validation in alt-stack/backend/tests/domain/test_activity.py"
Task: "T024 [US1] RED zone weights, TRIMP and estimated load in alt-stack/backend/tests/domain/test_training_load.py"
Task: "T027 [US1] RED DateRange and IsoWeek in alt-stack/backend/tests/domain/test_{date_range,iso_week}.py"
Task: "T040 [US1] RED display formatting probes in alt-stack/backend/tests/dashboard/test_display.py"
```

## Parallel Example: User Story 2

```bash
# While US1 is in progress (no domain dependency):
Task: "T043 [US2] RED Strava JSON shapes in alt-stack/backend/tests/strava/test_shapes.py"
Task: "T044 [US2] RED rate-limit headers and retry time in alt-stack/backend/tests/strava/test_rate_limit.py"
Task: "T045 [US2] RED client paging/retry/429/404 in alt-stack/backend/tests/strava/test_client.py"
Task: "T046 [US2] RED OAuth URL, exchange, refresh in alt-stack/backend/tests/strava/test_oauth.py"
```

## Parallel Example: User Story 3

```bash
# Frontend pure modules against goldens, while the backend routes are built:
Task: "T083 [US3] RED api client in alt-stack/frontend/tests/api.test.ts"
Task: "T084 [US3] RED windowing in alt-stack/frontend/tests/window.test.ts"
Task: "T085 [US3] RED chart geometry parity in alt-stack/frontend/tests/geometry.test.ts"
# Component REDs:
Task: "T089 [US3] RED MetricRow in alt-stack/frontend/tests/MetricRow.test.tsx"
Task: "T090 [US3] RED RecentActivities in alt-stack/frontend/tests/RecentActivities.test.tsx"
Task: "T091 [US3] RED MetricsChart in alt-stack/frontend/tests/MetricsChart.test.tsx"
Task: "T092 [US3] RED Rail and SyncPanel in alt-stack/frontend/tests/{Rail,SyncPanel}.test.tsx"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1: Setup.
2. Phase 2: Foundational. Generate and **review** the goldens (T018) before anything else.
3. Phase 3: US1.
4. **STOP and VALIDATE**: the domain and formatter match the reference on every fixture (SC-001). This alone answers the experiment's first question: can the new stack compute the same figures?

### Incremental delivery

1. Setup + Foundational → the parity reference is ready.
2. US1 → the figures match (SC-001).
3. US2 → import matches on recorded responses (SC-003). The app can now hold real data, headless.
4. US3 → the populated dashboard matches (SC-002). This is the first athlete-usable increment.
5. US4 → every state, device and appearance (SC-006, SC-007).
6. US5 + Polish → the comparison report and the completion review (SC-008).

### Solo-developer note

This is a solo project, so the parallel markers are mostly about **ordering freedom**, not staffing. The most valuable overlap is building the frontend's pure modules and components against the goldens (T082–T093) while the backend's import slice is in progress.

---

## Notes

- [P] = a different file, with no dependency on an incomplete task.
- The goldens are never hand-edited. If one looks wrong, the reference is wrong or the fixture is: raise it, and regenerate only deliberately (quickstart §8).
- `expired-token` is the only golden a test deliberately does not match (Amendment 1(b)1).
- Stop at any checkpoint to validate the story on its own.
