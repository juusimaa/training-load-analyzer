# Implementation Plan: Strava Import

**Branch**: `005-strava-import` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-strava-import/spec.md`

## Summary

Connect a Strava account once, then keep a local copy of the athlete's runs and rides in sync:
authorization-code exchange and token renewal, a paged walk of the activity list, mapping onto the
`TrainingActivity` model features 001–004 already calculate against, persistence in SQLite, and an
incremental sync that resumes after a rate limit instead of starting over.

**This is the first feature to leave the domain**, and the approach is shaped by that. It creates
`TrainingLoadAnalyzer.Infrastructure` and its test project — the first new projects since 001 — and
adds the first package reference in the repository. `TrainingLoadAnalyzer.Domain` gains **nothing**: no
type, no property, no package, no project reference. It ends this feature byte-identical to how it
started, and [quickstart.md](./quickstart.md) makes that a checkable command rather than an intention.

Two design decisions were settled by **building and running probes** rather than by reasoning, and both
came out against the obvious answer:

- **EF Core cannot map `TrainingActivity` directly.** It binds constructor parameters to properties by
  name, and this one's fourth parameter is `activityType` while the property is `Type`. There is no
  Fluent API to correct that. The alternatives were to rename a parameter in a signed-off domain type
  because a database library wants it, or to bind through a shadow property, which was verified to work
  and makes `Type` invisible to LINQ. An Infrastructure-owned `ActivityRow` is less total machinery
  than either ([research.md](./research.md) R4).
- **`DateTimeOffset` round-trips through SQLite exactly but cannot be ordered or compared.**
  `OrderBy(x => x.StartedAt)` throws; `Where(x => x.StartedAt > cutoff)` fails to translate. Every
  query this feature makes is a range over start time, so the row stores ticks and offset-minutes
  instead (R5).

**The third-party facts turned out to be shakier than expected.** Strava's current reference documents
no ordering for the activity list, no `per_page` maximum, and neither of the streams parameters that
every client library sends. What "everyone knows" rests on forum posts from Strava engineers and on
retired documentation. The design therefore depends on none of it: the walk is anchored with `after`,
results are sorted client-side, writes are upserts, and full stream resolution is obtained by omitting
the parameter rather than by asking for it (R15, R20).

**Three items were put to the developer rather than settled here**, under Principles VII and III, and
all three were answered on 2026-09-17. Two amended the specification before any task was generated.
They are in [Amendments made during planning](#amendments-made-during-planning).

Full rationale, alternatives, and revisit triggers for each decision are in
[research.md](./research.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`). SDK 10.0.100 confirmed installed.

**Primary Dependencies**: `Microsoft.EntityFrameworkCore.Sqlite` **10.0.12** and
`Microsoft.EntityFrameworkCore.Design` 10.0.12 (private assets), plus `dotnet-ef` 10.0.12 as a local
tool. Pinned to 10.0.12 rather than 10.0.0 because 10.0.0 pulls a transitive
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 and raises `NU1903` for a high-severity advisory. `HttpClient`,
`System.Text.Json`, and `TimeProvider` are base class library types, not dependencies. **The Domain
project keeps zero package references and zero project references** (R1, FR-018).

**Storage**: SQLite via EF Core, with migrations rather than `EnsureCreated` (R7). Three tables —
activities, the connection, and the sync state. The activity row stores an instant as ticks plus
offset-minutes (R5) and the heart-rate series as a JSON text column (R6).

**Testing**: xUnit v3 (`xunit.v3.mtp-v2` 4.0.1) on the Microsoft.Testing.Platform runner, matching the
domain test project. Two hand-written test doubles and no libraries for either: an `HttpMessageHandler`
stub for Strava (R9) and real SQLite in memory with a kept-open connection for the database (R8). No
mocking library, no assertion library, and explicitly **not** the EF Core InMemory provider, which was
verified to succeed at two queries that throw on real SQLite.

**Target Platform**: cross-platform .NET class library; developed on macOS. The database is a local
file on the athlete's own machine.

**Project Type**: class library — the integration layer Constitution Principle V requires, alongside
the domain library features 001–004 built.

**Performance Goals**: none stated or implied, and the binding constraint is not this code. Strava
allows **100 read requests per 15 minutes and 1,000 per day**, so a first import of roughly 1,200
activities costs about 140 requests and **will stop once at the fifteen-minute limit and resume**.
That makes the resumption path something the first run exercises rather than an edge case (R24).

**Constraints**: the domain must stay dependency-free and free of Strava vocabulary (FR-018,
Principle II). Round-trip fidelity is exact, including the UTC offset and every heart-rate sample
(FR-024). Reconciliation may delete only after a span was read to completion (FR-031c) — the one
failure this feature must never produce is losing training to a dropped connection. Credentials never
reach a log, a summary, or a tracked file (FR-005). Strava's rotated refresh token must be persisted on
every token response or the athlete is locked out permanently (R13).

**Scale/Scope**: 2 new projects, 2 new package references, roughly 10 new types, 58 functional
requirements, 14 success criteria, 4 user stories. Substantially larger than features 002–004
combined — this is the feature the project plan's risk section warns can consume the schedule.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Gate | Status |
|-----------|------|--------|
| **I. Strict TDD (NON-NEGOTIABLE)** | Sharper risk than any previous feature, from a new direction. Features 001–004 tested pure functions; here the first RED step needs a project, a package, a `DbContext` and a stub handler before any assertion can run. The temptation is to "just get the plumbing working" and start testing afterwards — which is precisely what Principle I calls not-TDD regardless of the coverage that follows. Mitigation: `/speckit-tasks` must make the scaffolding its own explicit non-TDD task, done once and named as such, so that every task after it starts RED. Mapping, rate-limit accounting, the resume point and reconciliation are all pure enough to test first. | ⚠️ **PASS with a named risk** |
| **II. Domain Independence from Strava** | The principle this feature exists to stress. The domain project ends unchanged — verified by two `grep` commands in [quickstart.md](./quickstart.md) and by `git diff --stat` against `main`. Strava's DTOs stay in Infrastructure (FR-018, C69); the dependency runs one way. **`ActivityRow` is what makes this hold under pressure**: mapping the domain type directly would have required renaming a domain constructor parameter to suit EF Core (R4). One judgement was escalated rather than taken quietly — whether the principle's `IActivitySource` example is a mandate (R2). | ✅ PASS |
| **III. Simplicity Before Abstraction (YAGNI)** | Eight candidate types are declined with recorded triggers in [data-model.md](./data-model.md), including a repository interface, a provider-neutral activity, a resilience policy, and an outstanding-series work queue. Three packages a reviewer would expect are declined with reasons: a mocking library (R9), a resilience library (R17), the EF Core InMemory provider (R8). `TrainingLoadAnalyzer.Application` was revisited per project plan 22.2 and is still not created — no host exists, so no use case has two callers (R1). | ✅ PASS |
| **IV. Testability** | No mocking library. Two hand-written doubles under a hundred lines total, both giving more direct control than a library would — particularly for rate-limit **headers**, which is where the behaviour lives. The database double is real SQLite, so mapping and round-trip fidelity are genuinely exercised rather than simulated; the InMemory provider was compared and rejected on evidence, not preference (R8). | ✅ PASS |
| **V. Isolation of External Integrations** | The principle that forces this feature's shape. OAuth, token handling, API access, paging and rate limits all land in `TrainingLoadAnalyzer.Infrastructure`, tested in its own test project at its own boundary, separate from the 204 domain tests that must stay fast and green. This is project plan 22.3 becoming real. | ✅ PASS |
| **VI. Observability & Deliberate Error Handling** | Every failure mode is a named `SyncOutcome` rather than an exception escaping or a silent empty result: rate-limited, interrupted, reconnection-required, refused. Skipped activities are counted with reasons; removals are reported by identifier (FR-038, FR-039). Nothing is repaired silently — which is exactly why R21 is open rather than decided. No logging or metrics infrastructure is introduced. | ✅ PASS |
| **VII. Specification Adherence** | This gate did more work than in any previous feature. Designing against the requirements found **two places the specification was silent on behaviour that will certainly occur** — a narrower granted scope (R14) and heart-rate samples the domain refuses (R21). Neither was settled in code: both were written up with options and a recommendation, put to the developer, answered, and written into the specification as FR-002a, FR-017f and FR-017g before tasks were generated. A third (R2) concerned the constitution's own wording and was answered without a code change. | ✅ PASS |

**Result**: all seven gates pass. No entries in Complexity Tracking — the two new projects and two new
packages are required by Principle V and by the specification, not chosen for convenience.

**One gate carries a caveat rather than a clean pass.** Principle I's risk is that this feature's shape
invites plumbing-first work, and no design decision removes it; the mitigation is a task-structure
requirement, recorded in the table above so `/speckit-tasks` inherits it. Principle VII's three items
are closed — all were answered before tasks were generated, which is the point of raising them during
planning rather than during implementation.

**Post-Phase 1 re-check**: re-evaluated after [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/infrastructure-api.md](./contracts/infrastructure-api.md)
and [quickstart.md](./quickstart.md) were written. The design added exactly the two projects and two
packages Principle V forces, and nothing else; every further dependency that suggested itself was
declined on recorded evidence. Principle III came under a different pressure than in feature 004 —
there, an existing rule wanted extracting; here, an ecosystem of conventional infrastructure packages
wanted adding, each individually reasonable. The answer in every case was that the requirement is
small enough to write directly. Principles I and VII remain the residual risks, for the reasons above.
Still ✅ PASS on all seven.

## Amendments made during planning

Three items were raised with the developer while working the design through, and answered on
2026-09-17. Two changed the specification; the third changed nothing but is recorded so the completion
review examines it deliberately rather than discovering it. All are argued in full in
[research.md](./research.md).

| # | Item | Resolution | Effect |
|---|------|------------|--------|
| **R2** | Constitution Principle II says external data must cross into the domain through an explicit abstraction "(e.g. `IActivitySource`)". In this feature both sides of such an interface would live in Infrastructure, so it would decouple nothing — the shape Principle III calls a defect. Principle II names the type; Governance says the constitution wins. | **No port for this feature.** Principle II's own stated rationale — independent testing of load logic, insulation from API churn — is already fully achieved by the project boundary. It arrives in Feature 6, shaped by its first real consumer. | No code change; revisit trigger recorded against Feature 6 |
| **R14** | An athlete can untick `activity:read_all` on Strava's consent page and approve the rest. The spec required that scope (FR-002) but said nothing about it being withheld. Private activities would then vanish silently, the import would report success, and every downstream figure would be wrong undetectably. | **Refuse the connection**, naming the withheld access, on FR-006's terms. A connection that cannot see private activities is not a degraded connection; it is a refused one. | **FR-002a**, User Story 1 scenario 7, a new edge case, SC-009 widened, guarantee C52 |
| **R21** | `HeartRateSeries` requires every sample between 20 and 250 bpm. Real Strava streams carry zero-bpm dropouts, most often in the opening seconds. The spec's Assumptions forbade repairing a series, so one dropout would have sent a whole ride to estimated load — undoing most of what the measured window was chosen to achieve. | **Discard implausible samples, count them, treat the series as unusable only when fewer than two survive** — two being the fewest that score anything, so no arbitrary threshold is invented. Discarding happens in the mapper, before `HeartRateSeries` is constructed, so no domain invariant moves. | **FR-017f**, **FR-017g**, two acceptance scenarios, a new edge case, a rewritten Assumption, `DiscardedSamples` on `SyncResult`, guarantee C71 |

R21 is the same shape of gap feature 003 and feature 004 each hit at their own R11: invisible while
writing requirements, unmissable while designing against them. It is the third consecutive feature in
which planning-as-a-separate-phase has paid for itself — and the first where the gap would not merely
have produced undocumented behaviour, but would have quietly cancelled a decision the developer had
already made deliberately.

## Project Structure

### Documentation (this feature)

```text
specs/005-strava-import/
├── spec.md               # Feature specification (/speckit-specify output)
├── plan.md               # This file (/speckit-plan output)
├── research.md           # Phase 0 output - 24 decisions, all settled
├── data-model.md         # Phase 1 output - stored shapes, declined types, requirement trace
├── quickstart.md         # Phase 1 output - scaffolding, the two test doubles, constitution checks
├── contracts/
│   └── infrastructure-api.md   # Phase 1 output - the new assembly's surface (C46-C71)
├── checklists/
│   └── requirements.md   # Specification quality checklist (16/16 passing)
└── tasks.md              # Phase 2 output (/speckit-tasks - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── TrainingLoadAnalyzer.Domain/                 # 001-004. UNCHANGED, including its .csproj
│   └── ...                                      # 17 files, zero dependencies, and it stays that way
└── TrainingLoadAnalyzer.Infrastructure/         # NEW - Principle V, project plan 22.3
    ├── Strava/
    │   ├── StravaCredentials.cs                 # NEW - client id and secret, from the environment
    │   ├── StravaAuthorization.cs               # NEW - FR-001 - FR-008
    │   ├── StravaApiClient.cs                   # NEW - FR-027, FR-034 - FR-037: paging, limits, retry
    │   ├── StravaActivitySummary.cs             # NEW - the response shape, never stored
    │   ├── StravaStreamSet.cs                   # NEW - FR-017, FR-017c
    │   ├── RateLimitStatus.cs                   # NEW - FR-034, FR-035
    │   └── StravaActivityMapper.cs              # NEW - FR-009 - FR-020, the sport-type table
    ├── Persistence/
    │   ├── ImportDbContext.cs                   # NEW - FR-022
    │   ├── ActivityRow.cs                       # NEW - FR-023 - FR-024 (research R4, R5, R6)
    │   ├── StravaConnection.cs                  # NEW - FR-003 - FR-008
    │   ├── SyncState.cs                         # NEW - FR-025, FR-027 - FR-029
    │   ├── ActivityStore.cs                     # NEW - FR-023, FR-030
    │   └── Migrations/                          # NEW - generated (research R7)
    └── Sync/
        ├── StravaActivitySync.cs                # NEW - the two entry points (C59)
        ├── SyncResult.cs                        # NEW - FR-038, FR-039
        └── SyncOutcome.cs                       # NEW - FR-006, FR-033, FR-035, FR-037, FR-040

tests/
├── TrainingLoadAnalyzer.Domain.Tests/           # 001-004. UNCHANGED - must stay at 204 green
│   └── ...
└── TrainingLoadAnalyzer.Infrastructure.Tests/   # NEW - Principle V's own boundary
    ├── Fakes/
    │   ├── StubHttpMessageHandler.cs            # NEW - research R9, no mocking library
    │   ├── SqliteFixture.cs                     # NEW - research R8, real SQLite kept open
    │   └── FixedClock.cs                        # NEW - research R10, a TimeProvider tests control
    ├── ConnectionTests.cs                       # NEW - User Story 1
    ├── ActivityMappingTests.cs                  # NEW - User Story 2, FR-009 - FR-020
    ├── PersistenceRoundTripTests.cs             # NEW - FR-024, asserted both ways (research R8)
    ├── IncrementalSyncTests.cs                  # NEW - User Story 3, FR-027 - FR-030
    ├── ReconciliationTests.cs                   # NEW - User Story 3, FR-031 - FR-032
    ├── RateLimitTests.cs                        # NEW - User Story 4, FR-034 - FR-040
    └── ImportedHistoryTests.cs                  # NEW - SC-003: the imported history reaching the calculators
```

**Structure Decision**: two new projects, and no more. `TrainingLoadAnalyzer.Infrastructure` exists
because Constitution Principle V requires the Strava integration and EF Core to be isolated and tested
at their own boundary — project plan section 22.3 named this feature as the moment, and it has arrived.
`TrainingLoadAnalyzer.Infrastructure.Tests` exists for the same reason: mixing tests that open
databases with 204 fast domain tests would slow the loop every cycle runs.

**`TrainingLoadAnalyzer.Application` is still not created.** Section 22.2 named this feature as the
revisit point, so it was revisited (R1). Its three conditions are a use case running from two hosts,
Blazor and API endpoints sharing orchestration, and wanting the compiler to keep host types out. None
has moved, because this feature introduces no host at all. The sync lives in a `Sync/` folder inside
the project that hosts it, exactly as 22.2 prescribes until an assembly earns its place.

The three-folder split inside Infrastructure — `Strava/`, `Persistence/`, `Sync/` — is the boundary
made visible: `Strava/` knows about a third party and nothing about storage, `Persistence/` knows about
storage and nothing about Strava, and `Sync/` is the only place that knows both. That makes the
one-way dependency checkable by inspection rather than only by intent.

Test files are split by user story so the P1–P4 slices stay independently runnable, continuing features
003 and 004's convention, with round-trip fidelity in a file of its own because FR-024 is asserted
twice — once on the domain object and once on the raw columns.

## Complexity Tracking

> No Constitution Check violations. This table is intentionally empty.
>
> Two new projects and two new package references arrive in this feature, which is more added structure
> than 002, 003 and 004 produced together. None of it is discretionary: Principle V requires the
> integration layer and its separate test boundary, and persistence requires a persistence library. The
> decisions a reviewer is most likely to challenge are the ones where something was **declined** —
> no mocking library, no resilience package, no EF Core InMemory provider, no repository interface, no
> Application project, and no port in the domain. Each is recorded with its evidence and its revisit
> trigger in [research.md](./research.md) and under "Types deliberately *not* created" in
> [data-model.md](./data-model.md).
