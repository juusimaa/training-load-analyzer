# Implementation Plan: Dashboard

**Branch**: `006-dashboard` | **Date**: 2026-09-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-dashboard/spec.md`

## Summary

One page showing what features 001–005 already compute: current Fitness, Fatigue and Form; this ISO
week's load and how it compares with last week's; a 180-day chart of the three metrics; the seven most
recent sessions with their load marked measured or estimated; and a button that syncs from Strava.
Blazor Interactive Server, one new project and one new test project.

**Nothing is stored and nothing is calculated.** This feature adds no entity, no table, no migration,
and no training arithmetic of its own — every figure is derived on read by calculators that already
exist. `TrainingLoadAnalyzer.Domain` and `TrainingLoadAnalyzer.Infrastructure` end this feature
byte-identical to how they started, and [quickstart.md](./quickstart.md) makes that a `git diff`
rather than an intention.

**The design's one real decision is where the arithmetic lives.** It goes into a pure static
`DashboardViewBuilder.Build(activities, today, maximumHeartRate, isConnected)` rather than into
components. Every question with a right answer — which days the chart covers, which week is current,
whether there is enough history to compare two weeks — is then a plain xUnit assertion at the speed of
the 204 domain tests, and the component tests are left covering what is genuinely about markup
(research R11). That is Principle IV applied before reaching for a test tool rather than after.

**Three probe findings changed the design**, and two of them are defects that would not have surfaced
in review:

- **SVG coordinates are culture-sensitive, and the developer's machine is `fi-FI`.** Rendering the
  points `(0, 45.3)` and `(1.5, 12.25)` the obvious way produced `points="0,45,3 1,5,12,25"` —
  well-formed markup, silently wrong geometry, no exception, and it would have looked perfect on an
  `en-US` CI machine. All coordinate formatting is pinned to `InvariantCulture` and asserted (R9).
- **Feature 005's one-sync-at-a-time guard stops working the moment a host exists.**
  `StravaActivitySync` holds its semaphore in an *instance* field, and a probe showed it cannot be
  registered as a singleton at all — so as a scoped service, two browser tabs get two semaphores and
  005's FR-040 quietly stops holding. The specification independently needs sync state to outlive a
  circuit (a page refresh is a new circuit), so one singleton coordinator satisfies both (R13).
- **`AddDbContextFactory` already registers a scoped `DbContext`, and adding `AddDbContext` as well
  breaks the container at build time.** The obvious second registration throws
  *"Cannot resolve scoped service … from root provider"* (R12).

**Performance was measured rather than assumed.** A history of 3 years 9 months, 953 activities and
1.7 million heart-rate samples reads from SQLite and recomputes everything in **292–677 ms** against
SC-001's two-second budget. No cache, no stored metrics table, no background recomputation — all three
were candidates, none is needed (R16).

**Four items were put to the developer rather than settled here**, under Principles VII and III, and
all four were answered on 2026-09-18. All four amended the specification before any task was generated.
They are in [Amendments made during planning](#amendments-made-during-planning).

Full rationale, alternatives and revisit triggers for all twenty-two decisions are in
[research.md](./research.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`). SDK 10.0.100, confirmed installed.

**Primary Dependencies**: `Microsoft.NET.Sdk.Web` (the Blazor Web App template, Interactive Server) for
the host; **`bunit` 2.11.3** and `Microsoft.AspNetCore.Mvc.Testing` for the test project. Three
packages in total, two of them test-only. **No charting library, no JavaScript interop, no mocking
library** — each declined with evidence in [research.md](./research.md) (R8, R17). `Domain` keeps zero
package references and zero project references; `Infrastructure` gains nothing.

**Storage**: unchanged. The three tables feature 005 created, read through
`ActivityStore.InRangeAsync` and `ImportDbContext.Connections`. This feature adds no migration; the host
applies the existing three at startup with `MigrateAsync` (R20).

**Testing**: xUnit v3 (`xunit.v3.mtp-v2` 4.0.1) on Microsoft.Testing.Platform, matching both existing
test projects. Three layers, each covering what the others cannot: plain xUnit over the pure read model
(most of the feature), bUnit over rendered components (order, badges, rounding, chart geometry), and
`WebApplicationFactory` over the two connect endpoints and the statically-rendered empty state (R10,
R18). Feature 005's `SqliteFixture` and `StubHttpMessageHandler` are reused; no new test double is
introduced.

**Target Platform**: a local ASP.NET Core process on the athlete's own machine, macOS in development.
The browser needs no WebAssembly and no JavaScript beyond Blazor's own circuit script (SC-007).

**Project Type**: web application — one host project plus its test project, added to the two libraries
features 001–005 built.

**Performance Goals**: SC-001's two seconds for a dashboard load. Measured at 292–677 ms in process
against a history thirteen times longer than the criterion describes (R16). SC-005's ten seconds applies
to an incremental sync; a first import is bounded by Strava's rate limits, not by this code (R7).

**Constraints**: the domain and the integration layer must end the feature unchanged (Principle II,
checkable by `git diff`). No training arithmetic in the UI — the trend thresholds are feature 004's and
stay there (FR-005, contract C103). No credential reaches a page, a log, a query string or a tracked
file (005 FR-005, contracts C76, C78). The application refuses to start without a maximum heart rate
rather than displaying figures from a default nobody chose (FR-015). Every numeric rendering is
culture-pinned (R9).

**Scale/Scope**: 2 new projects, 3 new package references, ~10 new types and 6 components, 21
functional requirements (13 original plus 8 added during planning), 7 success criteria, 5 user stories.
Smaller than feature 005 by a wide margin — the arithmetic all exists, and what is left is reading it,
shaping it and rendering it.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Gate | Status |
|-----------|------|--------|
| **I. Strict TDD (NON-NEGOTIABLE)** | A different risk from feature 005's, and a sharper one. UI work invites "get it on screen, then test what it does" — which Principle I calls not-TDD whatever coverage follows — and a `.razor` file is where that habit hides most easily. Two mitigations are structural rather than aspirational. First, R11 moves nearly every testable decision out of the components into a pure function, so most of this feature is testable exactly like features 001–004. Second, the scaffolding (a template, three csproj edits, a DI registration) genuinely cannot be driven by a failing test, so `/speckit-tasks` **must** make it one explicit, named non-TDD task, done once, with every task after it starting RED. The residual honest risk is markup: a test asserting "the newest activity is first" is written first, but the surrounding HTML is not, and should not be pretended otherwise. | ⚠️ **PASS with a named risk** |
| **II. Domain Independence from Strava** | The strongest gate in this feature, and the easiest to check. `Domain` and `Infrastructure` both end byte-identical — a `git diff --stat` in [quickstart.md](./quickstart.md), not a claim. `DashboardView` and `RecentActivity` are built from `TrainingActivity`, `DailyTrainingMetrics`, `WeeklyTrainingLoad`, `WeeklyLoadTrend` and `TrainingLoad`, and name Strava nowhere (contract C85, with a `grep` to prove it). Strava appears only in `Features/Sync/` and the two connect endpoints, which is where it belongs. R3 revisited feature 005's deferred `IActivitySource` question now that a real consumer exists, and the consumer does not want one. | ✅ PASS |
| **III. Simplicity Before Abstraction (YAGNI)** | Eleven candidate types are declined with recorded triggers in [data-model.md](./data-model.md), including `IDashboardReader`, an `AthleteProfile` entity, a `TrendDirection` enum, a DTO mirroring `SyncResult`, and a cache. Four packages a reviewer would expect are declined with reasons: a charting library (R8), a mocking library, Playwright, and the EF Core InMemory provider (R17). Project plan 22.2's `Application` project was revisited — the host has arrived, which is the argument for it, and none of 22.2's three conditions actually moved (R2). The one type that *is* introduced against YAGNI's grain, `SyncCoordinator`, is argued from three separate specification requirements rather than from tidiness (R13). | ✅ PASS |
| **IV. Testability** | The principle that shaped the design. R11's pure read model means the majority of this feature is tested with no test double at all, as fast as the 204 domain tests. bUnit is the one new test dependency and it covers a small, honest surface — things with no non-rendering formulation. No mocking library; the two doubles used are feature 005's, reused unchanged. The database is real SQLite, as 005's R8 evidence still requires. | ✅ PASS |
| **V. Isolation of External Integrations** | Unchanged and respected: OAuth, tokens, paging and rate limits stay in `Infrastructure`, tested at their own boundary. The host adds two endpoints that *call* `StravaAuthorization` and hold none of that logic themselves. The one place this principle came under pressure is R13 — the concurrency guard genuinely belongs in Infrastructure but its instance-field form cannot survive a host — and the resolution deliberately leaves 005 unmodified and records the trigger rather than reaching across the boundary. | ✅ PASS |
| **VI. Observability & Deliberate Error Handling** | R19 enumerates every exception the layers below are documented to throw and gives each one a displayed state. **Three of the five are reachable on entirely ordinary data** — a new athlete with four days of history hits two of them — so the read model checks preconditions rather than catching exceptions where a question has an answer (contract C82). FR-015's startup refusal is the deliberate choice this gate most wants: a wrong maximum heart rate produces confidently wrong numbers with nothing to notice. No logging or metrics infrastructure is introduced; the framework's `ILogger` is used where a failure would otherwise be invisible. | ✅ PASS |
| **VII. Specification Adherence** | This gate did the most work in this feature, as it did in 005. Designing against the requirements found **a direct contradiction with a shipped feature** (FR-005's 5% against feature 004's 15%-plus-50-points), **a value the whole system needs and nobody owns** (the maximum heart rate, declined three times by design), **an unrunnable requirement** (reconnect, with nowhere to reconnect from), and **an unmeetable success criterion** (SC-005 against Strava's documented rate limits). None was settled in code. All four were written up with options and a recommendation, put to the developer, answered, and written into the specification before tasks were generated. | ✅ PASS |

**Result**: all seven gates pass. No entries in Complexity Tracking — the two new projects are a host
and its tests, which Interactive Server requires and Principle V's separation encourages.

**One gate carries a caveat rather than a clean pass.** Principle I's risk is that markup work invites
implementation-first habits. No design decision removes it entirely; R11 removes most of it, and the
rest is a task-structure requirement recorded here so `/speckit-tasks` inherits it.

**Post-Phase 1 re-check**: re-evaluated after [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/web-contract.md](./contracts/web-contract.md) and
[quickstart.md](./quickstart.md) were written. The design added exactly one host project, one test
project and three packages, and nothing else. Principle III came under a different pressure than in 005
— there, conventional infrastructure packages wanted adding; here, the pull was toward a caching layer
and a charting library, and both were answered with measurements rather than with preference (R16, R8).
Principle II is in a stronger position than at any previous gate, because for the first time the check
is that two whole projects did not change. Principles I and VII remain the residual risks, for the
reasons above. Still ✅ PASS on all seven.

## Amendments made during planning

Four items were raised with the developer while working the design through, and answered on
2026-09-18. All four changed the specification. Each is argued in full in
[research.md](./research.md).

| # | Item | Resolution | Effect on [spec.md](./spec.md) |
|---|------|------------|--------------------------------|
| **R4** | Every measured load is `CalculateTrainingLoad(int maximumHeartRate)` and **nothing in the system holds that number**. Feature 001 refused to put it on the activity, 002 refused an `AthleteProfile` for it, 005 stated it "neither reads it from Strava nor stores it". Every call site so far has been a test picking 190. This is the first with a real athlete behind it, and the spec was silent. | **Configuration** — `Athlete:MaximumHeartRate`, with startup refusing to run when it is absent or not positive. It reaches the application the way `StravaCredentials` already does, and adds no entity, migration or form. | **FR-014**, **FR-015**, a new Assumption |
| **R5** | FR-005 said a weekly change of "≥5%" is significant. Feature 004 FR-010 — built, tested, shipped — requires **both** ≥15% and ≥50 points, and argues at length for the floor. Two rules would have been computed over the same two weeks, free to disagree in front of the athlete. | **Feature 004's rule wins.** The dashboard renders `TrendClassification` and applies no threshold of its own; a `grep` in [quickstart.md](./quickstart.md) proves no threshold number exists in `Web`. | rewritten **FR-005**, new **FR-005a**, US2 scenario 2 |
| **R6** | US5 scenario 5 requires an error "directing them to reconnect" and FR-011 requires "guidance on next steps" — but feature 005 built no host, so **there was nowhere to direct anyone**. `StravaAuthorization` could only be driven from a test, and no US5 scenario could be run end to end. | **A minimal connect flow is in scope**: a redirect to Strava's consent screen and a callback that completes the exchange, both calling code that already exists. Connect only — no disconnect, no account management. | **FR-016**, **FR-017**, **FR-018**, a rewritten Assumption |
| **R7** | SC-005 bounded a sync of "up to 2 years of activities" at ten seconds. Feature 005's own R24 established that ~1,200 activities cost ~140 requests against an allowance of 100 per fifteen minutes, so a first import *will* stop and resume — minutes, not seconds. The criterion was unmeetable as written. | **Restated** to bound an incremental sync of an already-current history. A first import is explicitly excluded and covered by FR-010's rate-limit messaging. | rewritten **SC-005** |

R5 is a shape this project has not hit before: not a gap in the specification but a **contradiction
with a feature already signed off**, written in good faith by someone who had not gone back to check
004's thresholds. It is the kind of thing that survives review — both documents read fine on their own
— and is caught only by designing one against the other. R6 is the same story from the other side: a
requirement that reads as satisfiable until you look for the thing it depends on and find that it was
never built, because no feature owned it.

## Project Structure

### Documentation (this feature)

```text
specs/006-dashboard/
├── spec.md               # Feature specification (/speckit-specify output, amended during planning)
├── plan.md               # This file (/speckit-plan output)
├── research.md           # Phase 0 output - 22 decisions, 7 settled by probe, 4 by the developer
├── data-model.md         # Phase 1 output - the read model, 11 declined types, requirement trace
├── quickstart.md         # Phase 1 output - scaffolding, the culture check, constitution commands
├── contracts/
│   └── web-contract.md   # Phase 1 output - routes, configuration, read model, components (C72-C103)
├── checklists/
│   └── requirements.md   # Specification quality checklist
└── tasks.md              # Phase 2 output (/speckit-tasks - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── TrainingLoadAnalyzer.Domain/                 # 001-004. UNCHANGED, including its .csproj
├── TrainingLoadAnalyzer.Infrastructure/         # 005. UNCHANGED, including its .csproj
└── TrainingLoadAnalyzer.Web/                    # NEW - the host (project plan 22.1, research R1)
    ├── Features/                                # project plan 22.2 - use cases, not an assembly
    │   ├── Dashboard/
    │   │   ├── DashboardView.cs                 # NEW - FR-001 - FR-008, FR-011 (C80, C84)
    │   │   ├── RecentActivity.cs                # NEW - FR-007, FR-008
    │   │   ├── DashboardViewBuilder.cs          # NEW - the pure read model (C79, C81-C83)
    │   │   ├── MetricsChart.cs                  # NEW - FR-006, SC-003, and research R9 (C89-C92)
    │   │   └── DashboardReader.cs               # NEW - the one store read (C86-C88)
    │   └── Sync/
    │       ├── SyncStatus.cs                    # NEW - FR-010, FR-012
    │       ├── SyncMessage.cs                   # NEW - US5 scenarios 1-5 (C97)
    │       └── SyncCoordinator.cs               # NEW - singleton; FR-009, FR-012, research R13
    ├── Endpoints/
    │   └── StravaConnectEndpoints.cs            # NEW - FR-016 - FR-018 (C74, C75)
    ├── Components/
    │   ├── Pages/Dashboard.razor                # NEW - @page "/"
    │   ├── Dashboard/MetricTile.razor           # NEW - FR-001 - FR-003
    │   ├── Dashboard/WeeklyLoadPanel.razor      # NEW - FR-004, FR-005, FR-005a
    │   ├── Dashboard/MetricsChartView.razor     # NEW - FR-006
    │   ├── Dashboard/RecentActivityList.razor   # NEW - FR-007, FR-008
    │   └── Dashboard/SyncPanel.razor            # NEW - FR-009, FR-010
    ├── AthleteSettings.cs                       # NEW - FR-014, FR-015 (C77)
    └── Program.cs                               # NEW - DI, migrations, startup validation

tests/
├── TrainingLoadAnalyzer.Domain.Tests/           # 001-004. UNCHANGED - must stay at 204 green
├── TrainingLoadAnalyzer.Infrastructure.Tests/   # 005. UNCHANGED - must stay at 105 green
└── TrainingLoadAnalyzer.Web.Tests/              # NEW - Microsoft.NET.Sdk.Razor (research R10)
    ├── DashboardViewBuilderTests.cs             # NEW - US1, US2, US4: pure, no doubles
    ├── EmptyAndPartialHistoryTests.cs           # NEW - FR-011, US1 sc2, US2 sc3, US3 sc2 (C81)
    ├── MetricsChartTests.cs                     # NEW - SC-003 and the culture check (C89, C90)
    ├── DashboardComponentTests.cs               # NEW - bUnit: rounding, badges, order (C99-C101)
    ├── SyncMessageTests.cs                      # NEW - US5 scenarios 1-5, pure
    ├── SyncCoordinatorTests.cs                  # NEW - the guard, the no-connection failure (C94, C95)
    ├── DashboardReaderTests.cs                  # NEW - real SQLite, reusing 005's SqliteFixture
    └── ConnectEndpointTests.cs                  # NEW - WebApplicationFactory; FR-016 - FR-018, C75
```

**Structure Decision**: one host project and one test project, and no more.

`TrainingLoadAnalyzer.Web` exists because project plan 22.1 needed deciding and Interactive Server was
its stated default with nothing forcing otherwise (R1). Choosing it is what keeps the count at two:
Interactive WebAssembly would have required `TrainingLoadAnalyzer.Api` and a shared contracts project
as well, four new projects for a dashboard one person opens on their own machine.

**`TrainingLoadAnalyzer.Application` is still not created.** Project plan 22.2 lists three conditions
and a host arriving is not one of them (R2). Use cases live in `Features/` inside the project that
hosts them, exactly as 22.2 prescribes until an assembly earns its place.

The split inside `Features/` is the boundary made visible, continuing 005's `Strava/`–`Persistence/`–
`Sync/` convention: **`Dashboard/` names no Strava type and has no I/O apart from `DashboardReader`**,
while `Sync/` and `Endpoints/` are the only places Strava appears. That makes Principle II checkable by
`grep` rather than only by intent (quickstart §5).

Test files are split by user story so the P1–P3 slices stay independently runnable, continuing features
003, 004 and 005's convention — with the chart's geometry and the empty/partial-history cases in files
of their own, because both cut across several stories and both are where the specification's edge cases
actually live.

## Complexity Tracking

> No Constitution Check violations. This table is intentionally empty.
>
> Two new projects and three new package references arrive here, which sounds like feature 005 again but
> is not: two of the three packages are test-only, and the host project is the template's own output
> with a project reference added. Nothing is stored, no schema moves, and two existing projects are
> untouched.
>
> The decisions a reviewer is most likely to challenge are, as in 005, the ones where something was
> **declined** — no charting library, no `Application` project, no `IActivitySource`, no
> `IDashboardReader`, no cache, no `AthleteProfile` entity, no mocking library, no browser automation.
> Each is recorded with its evidence and its revisit trigger in [research.md](./research.md) and under
> "Types deliberately *not* created" in [data-model.md](./data-model.md).
>
> The one addition that runs against YAGNI's grain is `SyncCoordinator`, and it is argued from three
> separate specification requirements — the page-refresh edge case, FR-012 and FR-010 — rather than from
> tidiness. Feature 005's now-redundant instance semaphore is left in place deliberately, with the
> trigger for revisiting recorded in R13.
