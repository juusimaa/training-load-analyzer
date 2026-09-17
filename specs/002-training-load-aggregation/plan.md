# Implementation Plan: Training Load Aggregation

**Branch**: `002-training-load-aggregation` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-training-load-aggregation/spec.md`

## Summary

Add pure domain aggregation that turns a collection of recorded sessions plus a requested date range
into two gap-free series: a total per calendar day, and a total per ISO-8601 week. Each entry
carries its points, how many sessions contributed, and whether the total is measured, estimated,
mixed, or has no basis at all.

The technical approach is additive and deliberately small: five new types in the existing
`TrainingLoadAnalyzer.Domain` assembly, no new project, no new package, no interface, no test
double. Day assignment reads each session's own offset-local date (`DateTimeOffset.Date`), ISO weeks
come from the platform's `System.Globalization.ISOWeek` rather than a hand-rolled rule, and the
weekly series is built as seven-day chunks of a daily series over an extended range — which makes
"a week equals the sum of its seven days" and "every week covers seven days" structural facts rather
than properties to be maintained.

Both of the specification's open questions were resolved by the developer before planning. Day
assignment uses each session's **own recorded UTC offset**; weekly totals **extend outward to whole
ISO weeks**, so a weekly total can include load from days outside the requested range while the
daily series never does. That asymmetry is the defining decision of this feature and is what most of
the design exists to keep honest.

Full rationale, alternatives, and revisit triggers for each decision are in
[research.md](./research.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`). SDK 10.0.100 confirmed installed.

**Primary Dependencies**: None added. The domain project keeps zero NuGet and zero project
references (research R1, feature 001 research R6). `System.Globalization.ISOWeek` and `DateOnly` are
base class library types, not dependencies. Test project unchanged: `xunit.v3` 4.x on the
Microsoft.Testing.Platform runner.

**Storage**: N/A. FR-024 forbids reading storage, and persistence remains out of scope.

**Testing**: xUnit v3 with built-in `Assert.*`. No assertion library, no mocking library — nothing in
this feature has a collaborator to substitute.

**Target Platform**: Cross-platform .NET class library; developed on macOS.

**Project Type**: Class library (pure domain), extending the assembly feature 001 created.

**Performance Goals**: None stated or implied. The realistic worst case is a few thousand activities
over a daily series in the thousands of entries — a decade is 3,653 days. The one deliberate choice
is to build the series by walking the range and looking each day up, rather than rescanning all
activities per day (research R14).

**Constraints**: Totals must be exactly reproducible by hand (FR-022, SC-004), which keeps every sum
in `decimal` with no rounding at any step. The domain assembly must still reference no external
activity provider (FR-025, Principle II). ISO week numbering must be the platform's, not ours —
2026 is a 53-week ISO year and the year-boundary rule is the classic place to be quietly wrong
(research R5).

**Scale/Scope**: 5 new domain types, 1 static aggregator with 2 methods, 25 functional requirements,
12 success criteria, 3 user stories. Roughly 150–250 lines of production code expected, on top of
feature 001's.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Gate | Status |
|-----------|------|--------|
| **I. Strict TDD (NON-NEGOTIABLE)** | Every behaviour here is a pure function of explicit arguments, so no case exists where a test cannot precede the code. `/speckit-tasks` must emit RED/GREEN/REFACTOR/VERIFY steps tied to the spec's given/when/then scenarios. One risk is named explicitly: research R8 describes the shape the weekly implementation is expected to converge on, and that is a REFACTOR target, not a licence to write it before a test asks for it. | ✅ PASS |
| **II. Domain Independence from Strava** | No new dependency of any kind; the domain `.csproj` is unchanged and still declares nothing. No provider vocabulary appears in any new type name — see [data-model.md](./data-model.md). External identifiers are never even compared (FR-023), so no provider's identifier format can influence behaviour. | ✅ PASS |
| **III. Simplicity Before Abstraction (YAGNI)** | No new project, no interface, no service instance, no wrapper type, no caching. Six candidate abstractions were considered and rejected with recorded revisit triggers in [data-model.md](./data-model.md) under "Types deliberately not created", including `ITrainingLoadAggregator` and a `TrainingLoadSeries`. The one type that might look speculative — `DateRange` — is justified by two call sites needing the same two guards and by the spec naming it as an entity (research R7). | ✅ PASS |
| **IV. Testability** | Nothing under test has a collaborator. Every test builds activities directly, calls one static method, and asserts on a returned value or a thrown exception. Zero test doubles are required — the outcome this principle aims at, and the reason no mocking library is introduced. | ✅ PASS |
| **V. Isolation of External Integrations** | No external integration exists in this feature. Satisfied vacuously, and protected by Out of Scope: loading activities from anywhere belongs to the import feature, and `IActivitySource` is deliberately not created here (research R15). | ✅ PASS (N/A) |
| **VI. Observability & Deliberate Error Handling** | Both refusals (FR-019, FR-020) throw naming the offending parameter, and a null collection is refused rather than silently treated as empty (research R12). Nothing is defaulted or dropped. One boundary is documented rather than silently handled: a range ending in the final ISO week of year 9999 surfaces the framework's own exception, recorded in research R13 and in [quickstart.md](./quickstart.md) instead of being hidden. No logging or metrics infrastructure is introduced — proportionate to a domain library. | ✅ PASS |
| **VII. Specification Adherence** | Every type and rule traces to a numbered requirement; the trace table is in [data-model.md](./data-model.md). The spec carries zero open clarification markers — both were answered by the developer, not by this plan. Where the specification is silent (the year-9999 boundary), no rule was invented; the silence is recorded with a revisit trigger. | ✅ PASS |

**Result**: all gates pass. No entries in Complexity Tracking.

**Post-Phase 1 re-check**: re-evaluated after [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/domain-api.md](./contracts/domain-api.md), and
[quickstart.md](./quickstart.md) were written. The design added no dependency, no project, and no
indirection beyond the five types the requirements force. Principle III remains the one under most
pressure, and the pressure moved: in feature 001 the temptation was a calculator service, here it is
a wrapper type holding both series together, plus the pull toward an `Application` project because
the preliminary project plan sketches one. Both are rejected with triggers recorded. Principle I is
the second watch point, because R8 hands the implementer a target shape — the tasks must still
arrive at it one failing test at a time. Still ✅ PASS on all seven.

## Project Structure

### Documentation (this feature)

```text
specs/002-training-load-aggregation/
├── spec.md              # Feature specification (/speckit-specify output)
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output — decisions, rationale, revisit triggers
├── data-model.md        # Phase 1 output — entities, rules, requirement trace
├── quickstart.md        # Phase 1 output — how to build and validate
├── contracts/
│   └── domain-api.md    # Phase 1 output — the API surface this feature adds
├── checklists/
│   └── requirements.md  # Specification quality checklist (16/16 passing)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
└── TrainingLoadAnalyzer.Domain/          # existing project, unchanged .csproj
    ├── TrainingActivity.cs               # 001, unchanged
    ├── ActivityType.cs                   # 001, unchanged
    ├── HeartRateSample.cs                # 001, unchanged
    ├── HeartRateSeries.cs                # 001, unchanged
    ├── HeartRateZone.cs                  # 001, unchanged
    ├── TrainingLoad.cs                   # 001, unchanged
    ├── LoadProvenance.cs                 # 001, unchanged
    ├── LoadBasis.cs                      # NEW — FR-014
    ├── DateRange.cs                      # NEW — FR-005, FR-019, FR-020
    ├── IsoWeek.cs                        # NEW — FR-002, FR-006
    ├── DailyTrainingLoad.cs              # NEW — FR-001
    ├── WeeklyTrainingLoad.cs             # NEW — FR-002
    └── TrainingLoadAggregator.cs         # NEW — the two entry points

tests/
└── TrainingLoadAnalyzer.Domain.Tests/    # existing project, unchanged .csproj
    ├── ...                               # 001's six test files, unchanged and staying green
    ├── DateRangeTests.cs                 # NEW — FR-019, FR-020, FR-005
    ├── IsoWeekTests.cs                   # NEW — FR-002 week boundaries, 53-week year
    ├── DailyAggregationTests.cs          # NEW — User Story 1
    ├── WeeklyAggregationTests.cs         # NEW — User Story 2, FR-013, FR-017
    └── LoadBasisTests.cs                 # NEW — User Story 3
```

**Structure Decision**: Everything lands in the two projects that already exist. No
`TrainingLoadAnalyzer.Application` project is created, even though the preliminary project plan
sketches one: this feature has no use case to orchestrate, no port to isolate, and no caller outside
the domain, so a second assembly would be structure built for a future that has not arrived
(Principle III, research R1). Adding it later, when the import feature genuinely needs it, is a
cheap move; carrying an empty layer through three features is not.

One file per type continues from feature 001, because each RED step introduces exactly one new type
and a one-to-one mapping keeps each cycle's diff small. Test files are split by user story rather
than by type, so that the P1/P2/P3 slices stay independently runnable — `dotnet test --filter
FullyQualifiedName~DailyAggregation` is the whole of User Story 1.

Two types considered before Phase 1 were dropped once the design was worked through: a `WeekRange`
for the extended span, which turned out to be three lines inside one method, and a `partial` flag on
`WeeklyTrainingLoad`, which under the developer's FR-013 decision would be permanently false. Both
are recorded with revisit triggers in [data-model.md](./data-model.md) under "Types deliberately not
created".

## Complexity Tracking

> No Constitution Check violations. This table is intentionally empty.
