---

description: "Task list for feature 006 — Dashboard"
---

# Tasks: Dashboard

**Input**: Design documents from `/specs/006-dashboard/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/web-contract.md](./contracts/web-contract.md),
[quickstart.md](./quickstart.md)

**Tests**: MANDATORY. The template treats test tasks as optional; Constitution Principle I (Strict
TDD, NON-NEGOTIABLE) overrides that. Every production member below arrives via a failing test, with
one deliberate and explicitly-marked exception — Phase 1.

**Organization**: Tasks are grouped by user story. Each task is one step of a
RED → GREEN → REFACTOR → VERIFY cycle, tied to a numbered acceptance scenario in
[spec.md](./spec.md).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: US1 / US2 / US3 / US4 / US5, mapping to the user stories in spec.md
- **RED**: write a test that fails, and confirm it fails *for the stated reason*
- **GREEN**: the minimum production code that makes it pass — nothing more
- **CONFIRM**: a test expected to pass with no new production code. It pins behaviour the design
  already implies. If it fails, that is a real defect, not a cue to write more code
- **VERIFY**: run the whole suite and check the cycle's discipline held

## ⚠️ The Principle I risk in this feature, and what to do about it

Feature 005's risk was that plumbing had to exist before any assertion could run. **This one is
different and, for this project, more insidious: markup.** A `.razor` file is where "get it on screen,
then test what it does" hides most comfortably, because the page looks like progress and the test looks
like paperwork. That is not TDD regardless of the coverage that follows.

Three rules keep it honest, and the Constitution Check in [plan.md](./plan.md) records them as this
feature's named mitigation:

1. **Phase 1 is the only non-TDD phase.** It creates a project from a template and edits `.csproj`
   files. It creates **no type under `Features/`** and no component beyond the template's own shell.
2. **Every task from T010 onward starts RED.** If a GREEN task seems to need a type no RED step asked
   for, that is design drift — stop and re-read [research.md](./research.md).
3. **Most of this feature is not markup at all.** Research R11 deliberately moved every decision with a
   right answer into `DashboardViewBuilder`, `MetricsChart` and `SyncMessage`, all pure static
   functions testable exactly like features 001–004. If you find yourself writing a component to work
   out *what* to display rather than *how*, the logic is in the wrong file.

## Path Conventions

Four projects already exist and are **not modified by this feature**:
`src/TrainingLoadAnalyzer.Domain/`, `src/TrainingLoadAnalyzer.Infrastructure/`,
`tests/TrainingLoadAnalyzer.Domain.Tests/` and `tests/TrainingLoadAnalyzer.Infrastructure.Tests/`.
T002 records their tree hashes and T115–T116 check them.

Two are created in Phase 1: `src/TrainingLoadAnalyzer.Web/` and `tests/TrainingLoadAnalyzer.Web.Tests/`,
laid out per [plan.md](./plan.md) — `Features/Dashboard/`, `Features/Sync/`, `Endpoints/` and
`Components/` in the first; one file per user story in the second.

## The clock

Every test in this feature runs at **2026-09-18T04:00:00Z**, in a fixture time zone of **UTC+03:00**,
so the athlete's local moment is **2026-09-18T07:00:00+03:00** — a **Friday**, in **ISO week 2026-W38**.
Three windows follow from it and are used throughout:

| Window | Span |
|---|---|
| this ISO week (2026-W38) | Mon **2026-09-14** – Sun **2026-09-20** |
| last ISO week (2026-W37) | Mon **2026-09-07** – Sun **2026-09-13** |
| the 180-day chart window | **2026-03-23** – **2026-09-18** |
| the 30-day chart threshold | 30 or more entries; 29 days back is **2026-08-20** |

**The time zone has to be pinned, not just the instant.** Feature 005's `FixedClock` overrides only
`GetUtcNow()`, and `TimeProvider.GetLocalNow()` converts through `LocalTimeZone`, which defaults to
`TimeZoneInfo.Local`. Verified by probe: with `GetUtcNow()` pinned to `2026-09-18T04:00:00Z`,
`GetLocalNow()` returned `2026-09-18T07:00:00+03:00` **because the machine is `Europe/Helsinki`** — on
a UTC build agent it would return `04:00` and every date assertion in this feature would move. T010
creates a clock that pins both.

## Fixture histories

Every test below draws from these. They are purpose-built: **estimated** loads throughout except where
stated, so a load is exactly `minutes × 2` (001 FR-013) and every expectation is arithmetic a reviewer
can check by hand, independent of the maximum heart rate.

### H1 — one session, for the hand-computed metrics

A single 60-minute run on **2026-09-18** at 07:00+03:00. Load **120 points**, basis `Estimated`.

| Figure | Exact value | Displayed |
|---|---|---|
| Fitness (CTL) | `120 × (1 − e^(−1/42))` = **2.8233976017308082** | **`2.8`** |
| Fatigue (ATL) | `120 × (1 − e^(−1/7))` = **15.974652029978209** | **`16.0`** |
| Form (TSB) | Fitness − Fatigue = **−13.151254428247402** | **`-13.2`** |
| `IsReliable` | `false` — one day against a 42-day warm-up (003 FR-014) | — |

All four figures were produced by running the repository's own `TrainingLoadAggregator` and
`TrainingMetricsCalculator`, not derived by hand, and they are quoted here so a test asserting them is
asserting *the real CTL* rather than the raw load. Feature 003's tests already prove the arithmetic;
these prove the dashboard is showing it.

### H2 — two weeks, for the weekly total and the trend

Seven 60-minute runs, all estimated, all at 07:00+03:00:

| ISO week | Days | Sessions | Total |
|---|---|---|---|
| 2026-W37 | 09-07, 09-09, 09-11 | 3 | **360 points** |
| 2026-W38 | 09-14, 09-15, 09-16, 09-17 | 4 | **480 points** |

Expected trend, verified by running feature 004's calculator:

| Clock | `AbsoluteChange` | `RelativeChange` | `IsComplete` | `Classification` |
|---|---|---|---|---|
| Friday 2026-09-18 | **+120** | **1/3** (0.3333…) | `false` | **`Indeterminate`** |
| Sunday 2026-09-20 | +120 | 1/3 | `true` | **`SignificantIncrease`** |

**H2 is the fixture that matters most in this feature.** The same two weeks classify differently
depending only on whether the current week has finished — which is FR-005a, and is feature 004's rule
(004 FR-017), not a display choice. It also clears both of 004's thresholds comfortably (+120 against a
50-point floor, +33% against 15%), so a dashboard that quietly reimplemented the spec's original "5%"
rule would pass every other test in Phase 4 and fail nothing until a week moved by 8%.

### H3 — the shapes that have no data

| # | History | Exercises |
|---|---|---|
| H3a | no activities at all | FR-011, US1 sc2, US5 sc5 |
| H3b | one activity, today | US2 sc3 (no previous week), US3 sc2 (under 30 days) |
| H3c | 29 days of daily sessions ending today (from **2026-08-20**) | US3 sc2's boundary — one day short |
| H3d | 30 days of daily sessions ending today (from **2026-08-19**) | US3 sc2's boundary — just enough |
| H3e | 400 days of daily sessions ending today | FR-006's 180-day cap, C83 |
| H3f | 10 activities over 3 weeks, one carrying a heart-rate series | US4 (7-of-10, ordering), FR-008 |
| H3g | 3 activities | US4 sc4 — fewer than seven |

### H4 — the row that cannot be read

One `ActivityRow` written directly to SQLite with `Type = "Swimming"` — a value `Enum.Parse<ActivityType>`
refuses. It cannot be produced by feature 005's mapper, which is the point: it stands for a database
touched by something else, and `ActivityRow.ToDomain` throws on it by design (005 C57). Used for
`IsUnavailable` (C87).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: create the host and its test project. **This is the only phase in this feature that is
not test-first**, and it creates no type under `Features/` — see the Principle I note above.

- [X] T001 Establish the baseline: `dotnet test` reports **309 passing, 0 failing** — 204 domain, 105 infrastructure. Any red here is a pre-existing problem to fix before adding to it
- [X] T002 Record the two trees that must not change: `git rev-parse HEAD:src/TrainingLoadAnalyzer.Domain` and `git rev-parse HEAD:src/TrainingLoadAnalyzer.Infrastructure`. T115 and T116 compare against these. This feature adds no member to either project ([contracts](./contracts/web-contract.md))
- [X] T003 Create the host: `dotnet new blazor -n TrainingLoadAnalyzer.Web -o src/TrainingLoadAnalyzer.Web -int Server -e --no-https`, add it to `TrainingLoadAnalyzer.sln`, and add a project reference to `src/TrainingLoadAnalyzer.Infrastructure/`. `-int Server` is research R1 and closes project plan 22.1; `-e` omits the template's sample pages. The reference runs **one way only** — neither library may ever reference the host
- [X] T004 Create `tests/TrainingLoadAnalyzer.Web.Tests/` with `dotnet new xunit3`, add it to the solution, and reference `src/TrainingLoadAnalyzer.Web/`
- [X] T005 Reconcile the generated test `.csproj`: change the SDK to **`Microsoft.NET.Sdk.Razor`** (required to compile `.razor` components), then **delete** the generated `<Content Include="xunit.runner.json" …/>` item. Both are probe findings (research R10) — the Razor SDK includes content items by default, so copying the other two test projects' explicit item fails the build with **`NETSDK1022`**. Keep the rest of this repository's conventions: `xunit.v3.mtp-v2` 4.0.1, `OutputType=Exe`, the `Xunit` implicit `Using`
- [X] T006 Add `bunit` **2.11.3** and `Microsoft.AspNetCore.Mvc.Testing` to `tests/TrainingLoadAnalyzer.Web.Tests/`. Confirm `dotnet restore` is warning-free — feature 005 hit `NU1903` from a transitive package and the same check applies here
- [X] T007 Reuse feature 005's test doubles rather than copying them: add `<Compile Include="..\TrainingLoadAnalyzer.Infrastructure.Tests\Fakes\SqliteFixture.cs" Link="Fakes\SqliteFixture.cs" />` and the same for `StubHttpMessageHandler.cs` to `tests/TrainingLoadAnalyzer.Web.Tests/`. Linked source, **not** a project reference: both types are `internal`, so a reference would not expose them, and a copy would be two files to keep in step (research R17)
- [X] T008 Add `*.db`, `*.db-shm` and `*.db-wal` under `src/TrainingLoadAnalyzer.Web/` to `.gitignore`, and confirm with `git check-ignore`. The athlete's training database must never be committed (C78)
- [X] T009 Checkpoint: `dotnet build` succeeds and `dotnet test` still reports **309 passing**. Confirm `src/TrainingLoadAnalyzer.Web/Features/` **does not exist** and no component beyond the template's shell has been written. If a `DashboardView`, a reader or a chart exists at this point, Phase 1 was over-extended

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the clock, the test host, and the configuration every story needs. The two doubles are
test code, so Principle I does not require a failing test first; everything else here starts RED.

**Note**: `DashboardViewBuilder` is a pure function and depends on none of this. If you want to start
Phase 3's first RED step before Phase 2 finishes, you can — that is research R11 paying off.

- [X] T010 [P] Write `tests/TrainingLoadAnalyzer.Web.Tests/Fakes/FixedLocalClock.cs` — a `TimeProvider` pinned to **2026-09-18T04:00:00Z** that overrides **both** `GetUtcNow()` **and** `LocalTimeZone`, returning a custom zone of a fixed **+03:00** offset (`TimeZoneInfo.CreateCustomTimeZone`), with a method to move the instant. Do **not** reuse feature 005's `FixedClock`: it overrides only `GetUtcNow()`, so `GetLocalNow()` follows the build machine — verified by probe, where it returned `07:00+03:00` on `Europe/Helsinki` and would return `04:00Z` on CI. A fixed custom zone rather than `Europe/Helsinki` so the tests do not depend on a time-zone database either (research R22)
- [X] T011 RED: assert **FR-015** and **C77** — building the host with `Athlete:MaximumHeartRate` absent throws at startup, and the message names the key. Confirm it fails because nothing reads the key — file: `tests/TrainingLoadAnalyzer.Web.Tests/StartupTests.cs`
- [X] T012 GREEN: create `src/TrainingLoadAnalyzer.Web/AthleteSettings.cs` as `public sealed record AthleteSettings(int MaximumHeartRate)`, bind it from `Athlete:MaximumHeartRate` in `Program.cs`, and **fail startup** when the value is absent or not positive. Add `public partial class Program;` at the end of `Program.cs` so the test project can name it. Nothing else — no DbContext, no dashboard page; T015 is what forces those
- [X] T013 [P] CONFIRM: assert **C77**'s boundaries — `0` is refused, `-1` is refused, `190` is accepted. A non-positive maximum must not reach `CalculateTrainingLoad`, whose own `ArgumentOutOfRangeException` would surface as a broken page rather than a configuration error (research R4, R19) — file: `tests/TrainingLoadAnalyzer.Web.Tests/StartupTests.cs`
- [X] T014 [P] Write `tests/TrainingLoadAnalyzer.Web.Tests/Fakes/WebAppFactory.cs` — a `WebApplicationFactory<Program>` that supplies `Athlete:MaximumHeartRate=190` and the Strava configuration, replaces the database with a `SqliteFixture` connection the test holds open, replaces `TimeProvider` with `FixedLocalClock`, and routes `StravaApiClient`'s and `StravaAuthorization`'s `HttpClient` through `StubHttpMessageHandler`. Test code, so no RED step — but it must come **after T012**, which is what makes `Program` nameable from the test project
- [X] T015 RED: assert **C73** — `GET /` returns **200** against an empty database, with no Strava connection and no network. Confirm it fails because there is no dashboard route and no database wiring — file: `tests/TrainingLoadAnalyzer.Web.Tests/StartupTests.cs`
- [X] T016 GREEN: wire `Program.cs` — `AddRazorComponents().AddInteractiveServerComponents()`, **exactly one** `AddDbContextFactory<ImportDbContext>(…)` call, `AddHttpClient<StravaApiClient>()`, `StravaCredentials` from configuration, `TimeProvider.System`, and `db.Database.MigrateAsync()` at startup — plus a placeholder `Components/Pages/Dashboard.razor` at `@page "/"` containing a heading and nothing else. **Do not also call `AddDbContext`**: verified by probe, the pair fails at container build with *"Cannot resolve scoped service `IEnumerable<IDbContextOptionsConfiguration<ImportDbContext>>` from root provider"* (research R12). **Do not use `EnsureCreated`**: feature 005 generated three migrations and the two do not mix (research R20)
- [X] T017 [P] CONFIRM: assert **C86** and research R12 — one `AddDbContextFactory` call yields **both** a resolvable `IDbContextFactory<ImportDbContext>` **and** a resolvable scoped `ImportDbContext`, and the provider builds with `ValidateOnBuild` and `ValidateScopes` enabled — file: `tests/TrainingLoadAnalyzer.Web.Tests/StartupTests.cs`
- [X] T018 [P] CONFIRM: assert research R20 — after startup the `Activities`, `Connections` and `SyncStates` tables all exist, and `grep -rn 'EnsureCreated' src/TrainingLoadAnalyzer.Web/` produces no output — file: `tests/TrainingLoadAnalyzer.Web.Tests/StartupTests.cs`
- [X] T019 VERIFY: `dotnet test` green, and `src/TrainingLoadAnalyzer.Web/Features/` still does not exist. The host starts, refuses to start without a maximum heart rate, and serves an empty page

**Checkpoint**: the host runs and is testable. Every task from here on starts RED.

---

## Phase 3: User Story 1 - View Current Training Status (Priority: P1) 🎯 MVP

**Goal**: an athlete opens the dashboard and sees Fitness, Fatigue and Form derived from their whole
stored history — or a clear empty state saying there is none.

**Independent Test**: seed activities into the store, load `/`, and read the three tiles. Fully
testable with no sync, no chart, no Strava.

### The read model, and the figures it carries

- [X] T020 [US1] RED: assert **FR-001**, **FR-002** and **FR-003** against fixture **H1** — `DashboardViewBuilder.Build` returns a `Current` whose `Fitness` is **2.8233976017308082**, `Fatigue` is **15.974652029978209** and `Form` is their difference. Confirm it fails to compile because `DashboardViewBuilder` does not exist. These are the real CTL and ATL for a single 120-point day, not the load itself — an implementation that displayed the raw 120 would fail here — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardViewBuilderTests.cs`
- [X] T021 [US1] GREEN: create `src/TrainingLoadAnalyzer.Web/Features/Dashboard/DashboardView.cs` and `DashboardViewBuilder.cs` with the minimum: aggregate the history daily over `firstActivityDay … max(today, lastActivityDay)`, compute metrics over the chart range, and expose `Current`. No weekly total, no trend, no recent list — T048, T051 and T078 are what force those
- [X] T022 [P] [US1] CONFIRM: assert **C80** — `Current` is the last entry of `Metrics`, not a stored field. The tiles and the chart's final point are therefore the same figures by construction, which is what makes **SC-002** structural rather than maintained — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardViewBuilderTests.cs`
- [X] T023 [P] [US1] CONFIRM: assert **C79** — `Build` is pure: called twice with the same arguments it returns equal views, and shuffling the order of `activities` changes nothing. It reads no clock (`today` is a parameter), no storage and no ambient state, continuing 002 C13 — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardViewBuilderTests.cs`
- [X] T024 [US1] RED: assert **US1 sc2** and **C81** against fixture **H3a** — with no activities, `Current` is null, `HasActivities` is false and `Metrics` is empty. Confirm it fails because `TrainingMetricsCalculator` throws on an empty history rather than being asked. The builder must **not** call it at all in this case; catching the exception would be the wrong fix (research R19) — file: `tests/TrainingLoadAnalyzer.Web.Tests/EmptyAndPartialHistoryTests.cs`
- [X] T025 [US1] GREEN: return an empty view when there are no activities, carrying `IsStravaConnected` through unchanged
- [X] T026 [P] [US1] CONFIRM: assert **C81** against fixture **H3b** — a history of one activity produces a view without throwing: `Current` is present, `Trend` is null, `HasEnoughHistoryForChart` is false — file: `tests/TrainingLoadAnalyzer.Web.Tests/EmptyAndPartialHistoryTests.cs`
- [X] T027 [P] [US1] CONFIRM — **discriminating check** for **R22**: an activity dated **tomorrow** (a device with a wrong clock) neither throws nor vanishes. It appears in `Recent` and contributes to the daily history, because the history window ends at `max(today, lastActivityDay)`. An implementation that ended the window at `today` either throws from `DateRange` or silently drops the session from every total while still listing it — file: `tests/TrainingLoadAnalyzer.Web.Tests/EmptyAndPartialHistoryTests.cs`

### Reading it from the store

- [X] T028 [US1] RED: assert **FR-013** — `DashboardReader.ReadAsync` over a `SqliteFixture` seeded with fixture **H1** returns the same figures T020 asserts. Confirm it fails to compile because `DashboardReader` does not exist — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardReaderTests.cs`
- [X] T028a [P] [US1] CONFIRM: assert **FR-013** and **C72** — during a dashboard load the stub handler records **zero** requests. T028 proves the figures come from the store; this proves nothing else was consulted to produce them, which is the half of FR-013 that says "no external API calls beyond the sync operation itself" — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardReaderTests.cs`
- [X] T029 [US1] GREEN: create `src/TrainingLoadAnalyzer.Web/Features/Dashboard/DashboardReader.cs` taking `IDbContextFactory<ImportDbContext>`, `TimeProvider` and `AthleteSettings`, performing **one** `ActivityStore.InRangeAsync` over the whole stored history and handing the result to `DashboardViewBuilder`. One read serves all five sections (research R15) — do not add a method to `ActivityStore` and do not query per section
- [X] T030 [P] [US1] CONFIRM: assert **C86** — `ReadAsync` creates its own `ImportDbContext` from the factory and disposes it, so two consecutive reads share no change-tracking state. A circuit-lifetime context is the pitfall this avoids (research R12) — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardReaderTests.cs`
- [X] T031 [US1] RED — **discriminating check** for **C88** and **R22**: with the clock at **2026-09-18T22:00:00Z** and the fixture zone at +03:00, the athlete's local moment is **2026-09-19T01:00+03:00**, so `AsOf` must be **2026-09-19**. Confirm it fails against an implementation using `GetUtcNow()`. This is the defect that would otherwise put an evening session into the wrong ISO week, and only for evening sessions — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardReaderTests.cs`
- [X] T032 [US1] GREEN: derive `today` from `TimeProvider.GetLocalNow()`, matching how feature 002 attributes an activity to a calendar day (002 FR-003)
- [X] T033 [US1] RED: assert **C87** and the "corrupted or malformed" edge case against fixture **H4** — a stored row whose `Type` no enum value matches yields a view with `IsUnavailable` set, and `ReadAsync` does not throw. Confirm it fails because `ActivityRow.ToDomain`'s exception escapes — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardReaderTests.cs`
- [X] T034 [US1] GREEN: catch the documented failures at the reader's boundary and return an unavailable view. Catch **only** what cannot be checked in advance — a short history is a question with an answer, not an error (research R19). Log it through `ILogger`; nothing here may be swallowed silently (Principle VI)

### Rendering the three tiles

- [X] T035 [US1] RED: assert **SC-002**, **C99** and **C100** — a formatting helper renders `2.8233976017308082` as **`2.8`**, `15.974652029978209` as **`16.0`**, `-13.151254428247402` as **`-13.2`**, and `null` as **`—`**. Set `CultureInfo.CurrentCulture` to `fi-FI` inside the test: the assertion must still hold. Confirm it fails to compile because no helper exists. Verified by probe — the naive version renders `45,3` on this developer's machine (research R9) — file: `tests/TrainingLoadAnalyzer.Web.Tests/DisplayFormatTests.cs`
- [X] T036 [US1] GREEN: create `src/TrainingLoadAnalyzer.Web/Features/Dashboard/Display.cs` with a single metric formatter using `CultureInfo.InvariantCulture` and the `0.0` format, returning `—` for null
- [X] T037 [US1] RED: assert **FR-001**, **FR-002** and **FR-003** in markup — `MetricTile` renders its label and its value. Confirm it fails because the component does not exist — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [X] T038 [US1] GREEN: create `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricTile.razor` with `Label`, `Value`, `Basis` and `IsReliable` parameters, formatting through `Display`. Derive the test class from **`BunitContext`**, not `TestContext` — the latter is ambiguous with `Xunit.TestContext` under xUnit v3 (`CS0104`, research R10)
- [X] T039 [P] [US1] CONFIRM: assert **C100** — a tile with no value renders `—`, never `0.0`, never an empty element (**US1 sc2**) — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [X] T040 [P] [US1] CONFIRM: assert **003 FR-014** reaches the page — a figure whose `IsReliable` is false is marked as still warming up, so a reading from four days of history is not presented as though it were settled — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [X] T041 [US1] RED: assert **US1 sc1** — `Dashboard.razor` seeded with fixture **H1** renders three tiles labelled Fitness, Fatigue and Form carrying `2.8`, `16.0` and `-13.2`. Confirm it fails because the page is still the placeholder from T016 — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [X] T042 [US1] GREEN: build `Components/Pages/Dashboard.razor` — inject `DashboardReader`, read once in `OnInitializedAsync`, render three `MetricTile`s
- [X] T043 [US1] RED: assert **FR-011** and **FR-017** against fixture **H3a** — with no activities and no connection, the page shows "No activities recorded" and an anchor to **`/connect`**. Confirm it fails because there is no empty state. The route itself arrives in Phase 7 at T089; asserting the anchor does not require it to resolve yet — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [X] T044 [US1] GREEN: add the empty state, choosing its guidance from `IsStravaConnected` — connect, or sync
- [X] T045 [P] [US1] CONFIRM: assert the "metric calculations fail" edge case in markup — a view with `IsUnavailable` renders "Data unavailable" and no tiles, rather than a stack trace or a blank page — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [X] T045a [P] [US1] CONFIRM: assert **SC-006** and **C73** — with fixture **H1** stored, **no Strava connection**, and the stub handler refusing every request, `GET /` still returns 200 and still renders `2.8`, `16.0` and `-13.2`. T015 asserts the page survives having no data; this asserts it serves the data it *has* when Strava is unreachable, which is the half of SC-006 that says "displays cached data". The messaging half is T112 — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [X] T046 [US1] REFACTOR: with US1 green, review `DashboardViewBuilder` for the one thing this story can get structurally wrong — a stored `Fitness` field shadowing `Metrics[^1]`. `Current` must remain derived (C80)
- [X] T047 [US1] VERIFY: `dotnet test` — all green, including all 309 existing tests. Confirm `Features/Dashboard/` contains no weekly, trend, chart or recent-activity code: this story showed three numbers and nothing else

**Checkpoint**: User Story 1 is complete and is a genuine MVP. An athlete with imported history opens
the dashboard and sees their training status; an athlete with none sees why and what to do.

---

## Phase 4: User Story 2 - View Weekly Load and Trend (Priority: P1)

**Goal**: this ISO week's training load, and an honest comparison with last week's that uses feature
004's judgement rather than a second rule.

**Independent Test**: seed fixture H2 and read the weekly panel. Independent of the chart, the recent
list and sync.

### This week's load

- [ ] T048 [US2] RED: assert **FR-004** and **US2 sc1** against fixture **H2** — `CurrentWeek.Points` is **480** and its `IsoWeek` is **2026-W38**. Confirm it fails because `DashboardView` has no `CurrentWeek` — file: `tests/TrainingLoadAnalyzer.Web.Tests/WeeklyLoadAndTrendTests.cs`
- [ ] T049 [US2] GREEN: aggregate weekly over the same history window and select the entry whose `IsoWeek` equals `IsoWeek.For(today)`
- [ ] T050 [P] [US2] CONFIRM: assert **US2 sc3** — a week in which the athlete did not train is **present with zero points**, not absent. Feature 002 builds the series by walking the range rather than grouping the activities, precisely so a rest week has an entry (002 FR-005) — file: `tests/TrainingLoadAnalyzer.Web.Tests/WeeklyLoadAndTrendTests.cs`

### The comparison with last week

- [ ] T051 [US2] RED: assert **FR-005** and **US2 sc2** against fixture **H2** — `Trend.Points` is 480, `PreviousPoints` is 360, `AbsoluteChange` is **+120** and `RelativeChange` is **1/3**. Confirm it fails because `DashboardView` has no `Trend` — file: `tests/TrainingLoadAnalyzer.Web.Tests/WeeklyLoadAndTrendTests.cs`
- [ ] T052 [US2] GREEN: call `TrainingLoadTrendCalculator.Calculate(weekly, new DateRange(thisMonday, today))` and take the entry for the current week
- [ ] T053 [US2] RED: assert **FR-005a** — with the clock on **Friday 2026-09-18**, `Trend.Classification` is **`Indeterminate`**, because the week is not finished. Confirm it fails if the builder passed a range that made the week look complete — file: `tests/TrainingLoadAnalyzer.Web.Tests/WeeklyLoadAndTrendTests.cs`
- [ ] T054 [US2] RED — **discriminating check** for **FR-005** and research R5: move the clock to **Sunday 2026-09-20** and assert the same two weeks now classify as **`SignificantIncrease`**. This is the test that proves the dashboard is reading feature 004's judgement rather than hardcoding one: +120 points on 360 clears both 004's thresholds (≥50 points, ≥15%), and nothing in the dashboard decides that — file: `tests/TrainingLoadAnalyzer.Web.Tests/WeeklyLoadAndTrendTests.cs`
- [ ] T055 [US2] RED: assert **C82** and **US2 sc3** against fixture **H3b** — with a history that does not reach the previous ISO week, `Trend` is **null** and nothing throws. Confirm it fails because `TrainingLoadTrendCalculator` throws rather than being asked: it refuses to invent a rest week (004 FR-023). The builder must check the precondition — is there a previous week? — never catch the exception (research R19) — file: `tests/TrainingLoadAnalyzer.Web.Tests/EmptyAndPartialHistoryTests.cs`
- [ ] T056 [US2] GREEN: guard the trend on `IsoWeek.For(historyStart).Monday <= thisMonday.AddDays(-7)`, as a precondition check
- [ ] T057 [P] [US2] CONFIRM: assert **004 FR-021** reaches the page — the trend carries its `LoadBasis`, so a comparison resting on estimated loads is not presented as though it were measured — file: `tests/TrainingLoadAnalyzer.Web.Tests/WeeklyLoadAndTrendTests.cs`

### Rendering it

- [ ] T058 [US2] RED: assert **FR-004**, **FR-005** and **FR-005a** in markup — `WeeklyLoadPanel` shows this week's total, the change against last week, and — mid-week — an in-progress indication rather than a significance label. Confirm it fails because the component does not exist — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T059 [US2] GREEN: create `src/TrainingLoadAnalyzer.Web/Components/Dashboard/WeeklyLoadPanel.razor` taking `Week` and `Trend`, rendering the change **always** and the classification **only** when the week is complete (research R14)
- [ ] T060 [P] [US2] CONFIRM: assert **C100** and **US2 sc3** — with `Trend` null, the panel renders `—` for the comparison and still shows the weekly total — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T061 [P] [US2] CONFIRM — **discriminating check** for **C103** and research R5: `grep -rnE '0\.15|0\.05|\b15 ?%|\b5 ?%|\b50\b' src/TrainingLoadAnalyzer.Web --include=*.cs --include=*.razor` produces no threshold. The spec's original "≥5%" rule contradicted feature 004's shipped 15%-plus-50-points; the resolution was that the dashboard applies **no** threshold of its own, and this is where that stays true — file: `tests/TrainingLoadAnalyzer.Web.Tests/WeeklyLoadAndTrendTests.cs`
- [ ] T061a [US2] REFACTOR: review `DashboardViewBuilder`'s weekly path for the one thing it can get structurally wrong — a comparison, a percentage or a sign test that has crept in beside the call to `TrainingLoadTrendCalculator`. The builder selects and passes through; it does not judge (C103)
- [ ] T062 [US2] VERIFY: `dotnet test` green. Confirm no arithmetic beyond display rounding was added to any component (C103)

**Checkpoint**: User Stories 1 and 2 both work. The athlete sees where they are and whether this week
is heavier than last.

---

## Phase 5: User Story 3 - View Time-Series Chart (Priority: P2)

**Goal**: 180 days of Fitness, Fatigue and Form as a line chart — plain inline SVG, no JavaScript, no
charting package (research R8).

**Independent Test**: seed a long history and assert on the rendered `points` attributes. Independent
of sync and of the recent list.

### The geometry

- [ ] T063 [US3] RED: assert **SC-003**, **C90** and **C91** — `MetricsChart.Plot` over a 5-day series returns exactly **three** series, each with exactly **5** points, in order. Confirm it fails to compile because `MetricsChart` does not exist — file: `tests/TrainingLoadAnalyzer.Web.Tests/MetricsChartTests.cs`
- [ ] T064 [US3] GREEN: create `src/TrainingLoadAnalyzer.Web/Features/Dashboard/MetricsChart.cs` returning `ChartSeries(Label, Points, CssClass)` for Fitness, Fatigue and Form
- [ ] T065 [US3] RED — **discriminating check**, and **the most important test in this feature**, for **C89** and research **R9**: with `CultureInfo.CurrentCulture` set to **`fi-FI`**, assert that plotting the points `(0, 45.3)` and `(1.5, 12.25)` yields a `Points` string of exactly two coordinate pairs — **two** commas in total, not four — and that it contains no `,` inside a number. Confirm it fails against a `$"{x},{y}"` implementation, which a probe showed produces `points="0,45,3 1,5,12,25"`: well-formed markup, silently wrong geometry, no exception, and correct-looking on an `en-US` machine while broken on this developer's own — file: `tests/TrainingLoadAnalyzer.Web.Tests/MetricsChartTests.cs`
- [ ] T066 [US3] GREEN: format every coordinate with `CultureInfo.InvariantCulture`
- [ ] T067 [P] [US3] CONFIRM: assert **C92** — a series in which Form is negative throughout plots inside the vertical band rather than being clipped at zero. Form is routinely negative (003 FR-003) — file: `tests/TrainingLoadAnalyzer.Web.Tests/MetricsChartTests.cs`
- [ ] T068 [P] [US3] CONFIRM: assert **C91** — an empty `metrics` list returns an empty series list, never a `ChartSeries` with an empty or malformed `points` attribute — file: `tests/TrainingLoadAnalyzer.Web.Tests/MetricsChartTests.cs`

### The window

- [ ] T069 [US3] RED: assert **FR-006** and **C83** against fixture **H3e** — with 400 days of history, `Metrics` holds exactly **180** entries, the first is **2026-03-23** and the last is **2026-09-18**. Confirm it fails if the builder passed the whole history to the chart — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardViewBuilderTests.cs`
- [ ] T070 [P] [US3] CONFIRM: assert **C83** — the 180 entries are gap-free and ascending, one per day. They are `TrainingMetricsCalculator`'s output unmodified, and feature 003 refuses a discontinuous history, so this pins that nothing reshapes it on the way out — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardViewBuilderTests.cs`
- [ ] T071 [P] [US3] CONFIRM — **boundary check** for **US3 sc2**: fixture **H3c** (29 days) leaves `HasEnoughHistoryForChart` false and fixture **H3d** (30 days) makes it true. One day either side of the specification's stated threshold — file: `tests/TrainingLoadAnalyzer.Web.Tests/EmptyAndPartialHistoryTests.cs`

### Rendering it

- [ ] T072 [US3] RED: assert **US3 sc1** — `MetricsChartView` renders an `<svg>` containing three `<polyline>` elements. Confirm it fails because the component does not exist — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T073 [US3] GREEN: create `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricsChartView.razor` rendering `MetricsChart.Plot`'s output. No JavaScript, no interop, no package (research R8)
- [ ] T074 [US3] RED: assert **US3 sc3** — a legend naming Fitness, Fatigue and Form is present, so each line can be identified. The specification offers a legend as an alternative to a tooltip and this takes it. Confirm it fails because there is no legend — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T075 [US3] GREEN: add the legend
- [ ] T076 [P] [US3] CONFIRM: assert **US3 sc2** in markup — with `HasEnoughHistoryForChart` false, the component renders "Not enough data to show trends (30+ days required)" and no `<polyline>` — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T076a [US3] REFACTOR: review `MetricsChart` for a second place where a number becomes a string. Every such place is another chance for the current culture to get in (research R9), so there should be exactly one formatting helper and every coordinate should pass through it
- [ ] T077 [US3] VERIFY: `dotnet test` green, and `grep -rn 'IJSRuntime\|<script' src/TrainingLoadAnalyzer.Web/Components/Dashboard/` produces no output — the chart is markup, and stays markup

**Checkpoint**: the athlete can see how their training has moved over half a year.

---

## Phase 6: User Story 4 - View Recent Activities (Priority: P2)

**Goal**: the last seven sessions, newest first, each showing whether its load was measured or
estimated.

**Independent Test**: seed fixture H3f and read the list. Independent of the chart, the weekly panel
and sync.

- [ ] T078 [US4] RED: assert **FR-007**, **SC-004** and **C84** against fixture **H3f** — `Recent` holds **7** entries, ordered newest first by `StartedAt`, and the three oldest of the ten are absent. Confirm it fails because `DashboardView` has no `Recent` — file: `tests/TrainingLoadAnalyzer.Web.Tests/RecentActivitiesTests.cs`
- [ ] T079 [US4] GREEN: create `src/TrainingLoadAnalyzer.Web/Features/Dashboard/RecentActivity.cs` as `record RecentActivity(DateOnly Day, ActivityType Type, TimeSpan MovingTime, TrainingLoad Load)` and populate it by sorting the history already in memory. **No new store method and no second query** (research R15)
- [ ] T080 [P] [US4] CONFIRM: assert **US4 sc4** against fixture **H3g** — three stored activities produce three entries, not three padded to seven — file: `tests/TrainingLoadAnalyzer.Web.Tests/RecentActivitiesTests.cs`
- [ ] T081 [US4] RED: assert **FR-008** — the activity in fixture **H3f** carrying a heart-rate series has `Load.Provenance == Measured`, and the others `Estimated`. Confirm it fails if `RecentActivity` flattened the load to a bare decimal. `TrainingLoad` carries points and provenance inseparably by design (001 SC-007), and flattening it here would reintroduce exactly the mistake that type prevents — file: `tests/TrainingLoadAnalyzer.Web.Tests/RecentActivitiesTests.cs`
- [ ] T082 [P] [US4] CONFIRM: assert **R22** — an activity's `Day` is its local day at its **own** recorded offset, matching how feature 002 buckets it (002 FR-003). A session at 2026-09-16T23:30+03:00 belongs to 2026-09-16, not to 2026-09-16T20:30Z's date — file: `tests/TrainingLoadAnalyzer.Web.Tests/RecentActivitiesTests.cs`
- [ ] T083 [US4] RED: assert **US4 sc1** in markup — `RecentActivityList` renders date, activity type, moving time and training load for each row, newest first. Confirm it fails because the component does not exist — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T084 [US4] GREEN: create `src/TrainingLoadAnalyzer.Web/Components/Dashboard/RecentActivityList.razor`
- [ ] T085 [US4] RED — **discriminating check** for **C101**, **US4 sc2** and **US4 sc3**: a measured row and an estimated row differ in the rendered **markup** — a badge, a title or text — and not only in a CSS class. A difference a screen reader cannot reach does not satisfy FR-008. Confirm it fails against an implementation that only varies styling — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T086 [US4] GREEN: render the provenance as content, not as styling alone
- [ ] T086a [US4] REFACTOR: review `RecentActivity` and its component for any path that reads `Load.Points` without its `Provenance`. The two travel together by design (001 SC-007), and FR-008 is only structurally safe while that stays true
- [ ] T087 [US4] VERIFY: `dotnet test` green. Confirm `ActivityStore` was not modified — `git diff src/TrainingLoadAnalyzer.Infrastructure/` is empty (research R15)

**Checkpoint**: all four display stories are complete. The dashboard is fully useful against data that
is already imported.

---

## Phase 7: User Story 5 - Manually Trigger a Sync (Priority: P3)

**Goal**: an athlete can connect a Strava account and bring their history up to date from the page,
and every way that can end is something the page can say.

**Independent Test**: drive the connect endpoints and the coordinator against the stub handler. No
real Strava call is made by any test.

### Connecting an account (FR-016, FR-017, FR-018)

These requirements were added during planning: US5 sc5 required directing the athlete to reconnect, and
feature 005 built no host to reconnect from ([plan.md](./plan.md#amendments-made-during-planning), R6).

- [ ] T088 [US5] RED: assert **FR-016** — `GET /connect` returns **302** to `https://www.strava.com/oauth/authorize` carrying a `state` parameter. Confirm it fails because the route does not exist — file: `tests/TrainingLoadAnalyzer.Web.Tests/ConnectEndpointTests.cs`
- [ ] T089 [US5] GREEN: create `src/TrainingLoadAnalyzer.Web/Endpoints/StravaConnectEndpoints.cs` with `/connect`, generating a `state` from a cryptographic source, storing it in the athlete's session, and redirecting to `StravaAuthorization.BuildAuthorizeUrl`. The URL itself is feature 005's and is already tested there
- [ ] T090 [P] [US5] CONFIRM: assert **C74** — two requests to `/connect` produce two different `state` values. A reused or predictable one is the hole `state` exists to close — file: `tests/TrainingLoadAnalyzer.Web.Tests/ConnectEndpointTests.cs`
- [ ] T091 [US5] RED — **discriminating check** for **C75** and research **R21**: `GET /strava/callback` with a `state` that does not match the stored one returns **400** **and the stub handler recorded no request to the token endpoint**. The second half is what matters: an implementation that exchanges first and validates afterwards passes a status-code assertion and still hands a forged code to Strava. The stub records every request, which is what makes this assertable — file: `tests/TrainingLoadAnalyzer.Web.Tests/ConnectEndpointTests.cs`
- [ ] T092 [US5] GREEN: validate `state` **before** calling `ExchangeAsync`
- [ ] T093 [US5] RED: assert **FR-016**'s happy path — a matching `state` and a `code` exchange against the stub, store a connection, and redirect to `/`. Confirm it fails because the callback does not complete the exchange — file: `tests/TrainingLoadAnalyzer.Web.Tests/ConnectEndpointTests.cs`
- [ ] T094 [US5] GREEN: call `ExchangeAsync` and redirect
- [ ] T095 [P] [US5] CONFIRM: assert the "connection revoked" edge case and 005 FR-002a — a callback carrying `error=access_denied` shows a declined-consent message without attempting an exchange, and an `InsufficientScopeException` surfaces as a refusal naming the withheld access rather than as a 500 — file: `tests/TrainingLoadAnalyzer.Web.Tests/ConnectEndpointTests.cs`

### What the page says about a sync

- [ ] T096 [US5] RED: assert **FR-010** and **US5 sc1 – sc5** — `SyncMessage.For` returns the eight messages in [data-model.md](./data-model.md#syncmessage): running, imported N, already up to date, rate-limited with its retry time, interrupted, reconnection required, refused, and no-connection. Confirm it fails to compile because `SyncStatus` and `SyncMessage` do not exist. All five scenarios are pure assertions here — no rendering, no Strava, no database — file: `tests/TrainingLoadAnalyzer.Web.Tests/SyncMessageTests.cs`
- [ ] T097 [US5] GREEN: create `src/TrainingLoadAnalyzer.Web/Features/Sync/SyncStatus.cs` and `SyncMessage.cs`. `SyncStatus` carries feature 005's `SyncResult` unchanged — it is already documented as carrying no credential in any field (005 C66)
- [ ] T098 [P] [US5] CONFIRM: assert **C97** — `SyncMessage.For` is total: every `SyncOutcome`, including one added later, yields a message rather than an empty string or an exception. A sync that ends in a way the page cannot describe is Principle VI's silent failure in display form — file: `tests/TrainingLoadAnalyzer.Web.Tests/SyncMessageTests.cs`
- [ ] T099 [P] [US5] CONFIRM: assert **C97** and 005 C66 — no message produced from any `SyncStatus` contains an access token, a refresh token or the client secret — file: `tests/TrainingLoadAnalyzer.Web.Tests/SyncMessageTests.cs`

### Running one

- [ ] T100 [US5] RED: assert **US5 sc5** and **C95** — with no Strava account connected, `SyncCoordinator.RunAsync` returns a status whose `Failure` is set and **does not throw**. Confirm it fails because `StravaActivitySync.SyncAsync` throws `InvalidOperationException` and nothing catches it — file: `tests/TrainingLoadAnalyzer.Web.Tests/SyncCoordinatorTests.cs`
- [ ] T101 [US5] GREEN: create `src/TrainingLoadAnalyzer.Web/Features/Sync/SyncCoordinator.cs` taking `IServiceScopeFactory`, creating a scope per run and resolving the scoped `StravaActivitySync` inside it. Register it as a **singleton** (research R13)
- [ ] T102 [US5] RED: assert **FR-009** and **US5 sc2** — with a connection stored and the stub returning two new activities, `RunAsync` returns `Completed` with `Imported == 2`, and the stored history grows. Confirm it fails if the coordinator never called the sync — file: `tests/TrainingLoadAnalyzer.Web.Tests/SyncCoordinatorTests.cs`
- [ ] T103 [US5] RED — **discriminating check** for **C94** and research **R13**: start two `RunAsync` calls concurrently and assert that **only one walk happened** — the stub recorded one request for page 1, not two — and that the second call returned the running status. Confirm it fails against a coordinator without a guard. Feature 005's own guard cannot do this: its semaphore is an **instance** field, a probe showed `StravaActivitySync` cannot be registered as a singleton at all, and as a scoped service two circuits get two semaphores. 005 FR-040's guarantee lives here now — file: `tests/TrainingLoadAnalyzer.Web.Tests/SyncCoordinatorTests.cs`
- [ ] T104 [US5] GREEN: hold one `SemaphoreSlim` on the coordinator, acquired with a zero timeout so a second request returns rather than queues
- [ ] T105 [P] [US5] CONFIRM — **discriminating check** for **C93**: resolve `SyncCoordinator` from two separate DI scopes and assert it is the **same instance**. Every other test in this phase passes against a scoped registration; only this one fails, and a scoped registration is what silently breaks the page-refresh edge case and the two-tab case — file: `tests/TrainingLoadAnalyzer.Web.Tests/SyncCoordinatorTests.cs`
- [ ] T106 [P] [US5] CONFIRM: assert **C96** — the `ImportDbContext` the sync used is disposed when `RunAsync` returns, so it is never a circuit-lifetime context (research R12) — file: `tests/TrainingLoadAnalyzer.Web.Tests/SyncCoordinatorTests.cs`
- [ ] T107 [P] [US5] CONFIRM: assert **US5 sc4** and 005 FR-035 — a stub returning rate-limit headers produces a status whose message names Strava's next window boundary, not a fixed delay — file: `tests/TrainingLoadAnalyzer.Web.Tests/SyncCoordinatorTests.cs`

### The button

- [ ] T108 [US5] RED: assert **FR-009** and **US5 sc1** — `SyncPanel` renders a "Sync Activities" button, and shows a loading indication while `Status.IsRunning`. Confirm it fails because the component does not exist — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T109 [US5] GREEN: create `src/TrainingLoadAnalyzer.Web/Components/Dashboard/SyncPanel.razor` taking `Status` and an `OnSync` callback
- [ ] T109a [US5] RED: assert **FR-009** and **US5 sc2**'s second half — clicking the button invokes `SyncCoordinator.RunAsync`, and when it returns the page **re-reads** the view, so a sync that imported activities changes the figures and the recent list on screen without a manual reload. Confirm it fails because the button is wired to nothing. Every task before this one tested the two halves separately: T102 proved the coordinator syncs, T108 proved the button renders. Nothing yet joins them, and the join is what the athlete actually does — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T109b [US5] GREEN: wire `SyncPanel`'s `OnSync` to the injected `SyncCoordinator` in `Components/Pages/Dashboard.razor`, and re-read `DashboardReader` when it returns. Re-read rather than patch: the view is derived, and applying a delta to it by hand is how the tiles and the chart start to disagree (C80)
- [ ] T110 [US5] RED: assert **FR-012** and the "sync in progress when the page refreshes" edge case — a **freshly constructed** `Dashboard` component, standing in for a new circuit, reads `IsRunning` from the coordinator and says a sync is running. Confirm it fails if the page kept that state in a component field — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T111 [US5] GREEN: read the status from the injected singleton on initialisation, never from component state
- [ ] T112 [P] [US5] CONFIRM: assert **US5 sc5**, **FR-017** and **FR-018** in markup — with no connection, and again with a `ReconnectionRequired` outcome, the panel shows "Strava connection required" and an anchor to `/connect`, which T089 now makes a real route — file: `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`
- [ ] T113 [US5] REFACTOR: review `SyncCoordinator` for the one thing it can get structurally wrong — a path that sets `IsRunning` without releasing the semaphore, or releases it without clearing `IsRunning`. The two must move together
- [ ] T114 [US5] VERIFY: `dotnet test` green, and `grep -rn 'StravaActivitySync' src/TrainingLoadAnalyzer.Web/Components/` produces no output — assert **C98**, that the coordinator is the sync's only caller

**Checkpoint**: all five user stories are complete. The application can be connected, synced and read
by someone who has never touched the database by hand.

---

## Phase 8: Polish, Precision, and Compliance Review

**Purpose**: the checks that only make sense once everything is green. These are the commands in
[quickstart.md](./quickstart.md) under "Constitution checks", run as tasks.

- [ ] T115 [P] Principle II — `git rev-parse HEAD:src/TrainingLoadAnalyzer.Domain` matches the hash recorded in T002, and `git diff --stat main -- src/TrainingLoadAnalyzer.Domain` is empty. The domain gained nothing in this feature
- [ ] T116 [P] Principle II — the same for `src/TrainingLoadAnalyzer.Infrastructure`, and both existing test projects still report **204** and **105** passing
- [ ] T117 [P] Principle II / **C85** — `grep -rni 'strava' src/TrainingLoadAnalyzer.Web/Features/Dashboard` produces no output. `Features/Sync/` and `Endpoints/` may name Strava; the dashboard's figures may not
- [ ] T118 [P] **C102** — `grep -rn 'ImportDbContext\|ActivityStore\|StravaActivitySync\|TimeProvider' src/TrainingLoadAnalyzer.Web/Components` produces no output. Components take a view and a status; the reader and the coordinator are the only things that touch the layers below
- [ ] T119 [P] **C103** — `grep -rn 'TrainingLoadAggregator\|TrainingMetricsCalculator\|TrainingLoadTrendCalculator' src/TrainingLoadAnalyzer.Web/Components` produces no output. All three belong in `DashboardViewBuilder`
- [ ] T120 [P] Principle III / research R8, R17 — `grep -rn -E 'ChartJs|ApexCharts|Plotly|Blazorise|Moq|NSubstitute|FakeItEasy|InMemory|Playwright|Selenium' src/TrainingLoadAnalyzer.Web tests/TrainingLoadAnalyzer.Web.Tests` produces no output. The risk is not a package added deliberately but one added in passing, to solve a problem research already solved without it
- [ ] T121 [P] research **R9** — run the **whole** suite under a comma-decimal locale: `DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false LANG=fi_FI.UTF-8 dotnet test`. Every test must pass identically. This is the developer's own machine locale, and the failure it catches is invisible on an `en-US` one
- [ ] T122 [P] **C76** / **C78** — `grep -rni 'client_secret\|AccessToken\|RefreshToken' src/TrainingLoadAnalyzer.Web/Components src/TrainingLoadAnalyzer.Web/appsettings*.json` produces no output, and `git check-ignore src/TrainingLoadAnalyzer.Web/*.db` confirms the database is ignored
- [ ] T122a [P] **FR-012a** / research R23 — `grep -rni 'localStorage\|sessionStorage\|IJSRuntime' src/TrainingLoadAnalyzer.Web` produces no output. FR-012 originally called for state "on athlete's device"; under Interactive Server a reload is a new circuit, so the state is the application's and the browser holds none of it. This is where that stays true — and it is the same grep that keeps R8's "no JavaScript" honest
- [ ] T123 [P] **SC-007** — `grep -rn '<script\|IJSRuntime' src/TrainingLoadAnalyzer.Web/Components` finds nothing beyond Blazor's own circuit script in `App.razor`. No charting library means no third-party JavaScript to differ between browsers
- [ ] T124 **SC-001** — measure rather than assume: start the host against a real multi-year database and time three loads of `/` with `curl -o /dev/null -s -w "%{time_total}\n"`. Research R16 measured 292–677 ms in process for a 3-year-9-month, 953-activity, 1.7-million-sample history. A warm figure above 2 seconds reopens R16; a projection does not
- [ ] T125 **SC-005** — time a manual sync of an already-current history end to end. A first import of a multi-year history is explicitly outside this criterion and will stop at Strava's rate limit by design (spec SC-005, 005 R24)
- [ ] T126 Run [quickstart.md](./quickstart.md) end to end on a real account: connect, sync, reload, two tabs. Section 4's manual checks are the ones no test covers — a real consent screen, a real rate limit, a real refresh mid-sync
- [ ] T127 REFACTOR: review `Features/Dashboard/` and `Features/Sync/` for the boundary they exist to make visible — `Dashboard/` naming no Strava type and performing no I/O apart from `DashboardReader`, and `Sync/` and `Endpoints/` the only places Strava appears
- [ ] T128 Principle VI — review every failure path for a message that names its rule: the startup refusal, the unavailable view, the declined consent, the mismatched state, and each `SyncOutcome`. Nothing silently swallowed, nothing reduced to a generic failure
- [ ] T129 Constitution compliance review against all seven principles, recording the result in the feature's completion notes. Principles I, II and III are named in the constitution as most at risk; **Principle I is this feature's residual risk**, per the Constitution Check in [plan.md](./plan.md), so record honestly whether every production member arrived via a failing test — and in particular whether any `.razor` file was written before the test that describes it
- [ ] T130 Update `README.md`: the project structure section still lists only `TrainingLoadAnalyzer.Domain`, and has been stale since feature 005. Add `Infrastructure`, `Web` and their test projects, and note how to run the application
- [ ] T131 VERIFY: `dotnet build -warnaserror` clean and `dotnet test` fully green — features 001–006 together

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)** — blocks everything. Nothing compiles until the projects exist
- **Phase 2 (Foundational)** — blocks every story that touches the host. `DashboardViewBuilder`'s pure tests do not depend on it, so Phase 3 can start early
- **Phase 3 (US1)** — creates `DashboardView`, `DashboardViewBuilder`, `DashboardReader`, `Display` and the page. **Every later story extends these**
- **Phase 4 (US2)** — adds `CurrentWeek` and `Trend` to the view built in Phase 3
- **Phase 5 (US3)** — adds `MetricsChart` and the chart window; the `Metrics` list it plots is Phase 3's
- **Phase 6 (US4)** — adds `Recent` to the same view
- **Phase 7 (US5)** — adds the connect endpoints and the sync coordinator, and the only phase that touches Strava
- **Phase 8 (Polish)** — after everything

### User Story Dependencies

The display stories share one read model, so they are not independent in the way a fresh CRUD feature's
would be — and it is more honest to say so:

- **US1** is genuinely independent, and is the MVP. It needs no other story
- **US2, US3 and US4 each depend on US1** for `DashboardView`, `DashboardViewBuilder` and
  `DashboardReader`. Once US1 is done they are independent **of each other** and can be worked in
  parallel: each adds its own property to the view, its own component, and its own test file
- **US5 depends on US1** only for the page to put the button on. It is otherwise independent — the
  coordinator and the endpoints share nothing with the display stories

This is the same honest ordering features 004 and 005 recorded: each story is independently *testable*
once US1 is in place, and each is a deliverable increment.

### Within Each Story

RED before GREEN, always. CONFIRM tasks may be written at any point after the behaviour they pin
exists. VERIFY closes each phase.

### Parallel Opportunities

- **T010, T013** — the clock and the startup boundary checks, different files
- **T014** is *not* parallel with T010: it names `Program`, which only becomes nameable at T012
- **T013, T017, T018** — the startup CONFIRMs, once the behaviour each pins exists
- **T022, T023, T026, T027** — US1's read-model CONFIRMs
- **T039, T040, T045** — US1's component CONFIRMs, all in `DashboardComponentTests.cs`
- **T050, T057, T060, T061** — US2's CONFIRMs
- **T067, T068, T070, T071, T076** — US3's CONFIRMs
- **T080, T082** — US4's CONFIRMs
- **T090, T095, T098, T099, T105, T106, T107, T112** — US5's CONFIRMs
- **T115 – T123** — the compliance greps, all read-only and independent
- **After Phase 3 closes at T047**, US2, US3, US4 and US5 can proceed in parallel if staffed

Note that `[P]` here means "no dependency", not "edit the same file simultaneously". Several parallel
tasks land in `DashboardComponentTests.cs`; run them in any order, but not concurrently in that file.

---

## Implementation Strategy

### MVP scope

**User Story 1 alone is a genuine MVP**, unlike in feature 005. Three numbers on a page, derived from
a real imported history, is the sentence this whole project exists to produce — features 001–005 have
all been building toward a figure nobody could yet see. US1 makes it visible.

It is worth stopping there and looking at it before going further. If the Fitness number is wrong, it
is wrong in Phase 3 and every later phase inherits it.

### Incremental delivery

1. **Phases 1–2** — scaffolding, the clock, the host. Nothing works yet; nothing is claimed to
2. **Phase 3 (US1)** — the three tiles. **The first shippable increment**, and the moment the project's
   whole premise becomes visible
3. **Phase 4 (US2)** — "am I training more than last week?", answered with feature 004's judgement
4. **Phase 5 (US3)** — half a year of trend, in markup
5. **Phase 6 (US4)** — the sessions behind the numbers
6. **Phase 7 (US5)** — connect and sync from the page, which is what makes the application usable by
   someone who has not been hand-seeding a database
7. **Phase 8** — compliance review

### A note on doing Phase 7 last

US5 is P3 and sits at the end, which means that until Phase 7 the only way to get data in is a test.
That is deliberate — the display stories are where this feature's requirements are, and the connect
flow is scope that planning **added** (R6). But it does mean the first genuinely end-to-end run happens
late. If you want to see the dashboard against your own Strava history sooner, T088–T094 can be lifted
forward on their own: they depend on nothing in Phases 3–6.

---

## Notes

- **139 tasks**, against 20 functional requirements (13 original, 7 added during planning and analysis),
  7 success criteria, 32 new contract guarantees (C72–C103), and 5 user stories
- Phase 1 is the **only** non-TDD phase, and it creates no type under `Features/`. Every task from T010
  onward starts RED — the mitigation the Constitution Check in [plan.md](./plan.md) records for this
  feature's named Principle I risk, which here is **markup**, not plumbing
- Nine tasks are marked **discriminating checks** — T027, T031, T054, T061, T065, T085, T091, T103,
  T105. Each fails against a plausible wrong implementation that every other test in its phase would
  accept. **T065 is the most important test in the feature**: without it the chart renders wrong
  geometry, silently, on the developer's own machine and not on CI
- The four answers from planning are load-bearing and easy to undo by accident. T054 and T061 pin
  FR-005's deferral to feature 004, T011–T013 pin FR-014 and FR-015, T088–T095 pin FR-016 – FR-018, and
  T125 pins the restated SC-005
- Two tests exist because a *probe* found the defect, not because a requirement named it: **T065**
  (culture in SVG geometry) and **T105** (the coordinator's lifetime). Neither would have been written
  from the specification alone
