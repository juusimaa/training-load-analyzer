# Implementation Plan: Training Activity Domain

**Branch**: `001-training-activity-domain` | **Date**: 2026-09-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-training-activity-domain/spec.md`

## Summary

Build the immutable domain model for a single completed training session — external identifier,
start time with offset, moving time, activity type, and an optional heart-rate sample series — that
refuses to exist in an invalid state and exposes its training load in TRIMP points, marked as
measured or estimated.

The technical approach is deliberately minimal: two projects, no NuGet dependencies in the domain
at all, no interfaces, no services, no mocking. The load calculation is an instance method on the
activity taking the athlete's maximum heart rate as an argument, implementing Edwards TRIMP by
integer zone classification and exact `decimal` arithmetic so that every value a test asserts is
one a reviewer can reproduce by hand. Validation failures throw from the constructor, which
satisfies "no partially-formed record" without any Result machinery.

Full rationale, alternatives, and revisit triggers for each decision are in
[research.md](./research.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`). SDK 10.0.100 confirmed installed.

**Primary Dependencies**: None in the domain project — zero NuGet and zero project references, by
design (research R6). Test project only: `xunit.v3` 4.x, `xunit.runner.visualstudio`,
`Microsoft.NET.Test.Sdk`.

**Storage**: N/A. Persistence is explicitly out of scope for this feature.

**Testing**: xUnit v3 with built-in `Assert.*` assertions. No assertion library and no mocking
library (research R3, R4).

**Target Platform**: Cross-platform .NET class library; developed on macOS.

**Project Type**: Class library (pure domain), consumed later by other projects in the same
solution.

**Performance Goals**: None. The largest realistic input is one session's heart-rate series — on
the order of thousands of samples for a long ride at one-second resolution — processed in a single
pass. No performance requirement is stated or implied by the specification.

**Constraints**: The domain assembly must not reference Strava or any external activity provider,
in code or in package references (FR-025, Principle II). Training load must be reproducible by hand
from the specification alone (SC-006), which rules out floating-point drift — hence `decimal` and
integer zone boundaries (research R9, R10).

**Scale/Scope**: 7 domain types, 25 functional requirements, 10 success criteria, 3 user stories.
Roughly 250–400 lines of production code expected.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Gate | Status |
|-----------|------|--------|
| **I. Strict TDD (NON-NEGOTIABLE)** | Every behaviour in this feature is testable first — the types are values and the calculation is pure, so there is no case where a test cannot precede the code. `/speckit-tasks` must emit RED/GREEN/REFACTOR/VERIFY steps tied to the specification's given/when/then scenarios, not "implement X" instructions. | ✅ PASS |
| **II. Domain Independence from Strava** | The domain project declares zero package and project references, making a provider dependency impossible to add without an obvious `.csproj` change. No provider vocabulary appears in any type name in [data-model.md](./data-model.md). Sample times are relative to session start specifically so no provider's timestamp shape leaks in (research R8). | ✅ PASS |
| **III. Simplicity Before Abstraction (YAGNI)** | No repository, no factory, no service class, no interface, no `Result<T>`, no mocking library, no assertion library, no architecture-test package. Two projects rather than the nine sketched in the project plan. Each rejected abstraction is recorded in research.md with the concrete trigger that would justify it later. | ✅ PASS |
| **IV. Testability** | Nothing under test has a collaborator. Every test constructs a value directly and asserts on a returned value or a thrown exception. Zero test doubles are required, which is the outcome this principle is aiming at rather than a coincidence. | ✅ PASS |
| **V. Isolation of External Integrations** | No external integration exists in this feature. The principle is satisfied vacuously here, and is what Out of Scope protects: import, OAuth, and persistence belong to later features. | ✅ PASS (N/A) |
| **VI. Observability & Deliberate Error Handling** | Every refusal throws with the violated rule named (FR-022); nothing is silently defaulted or dropped, including out-of-range heart-rate samples, which refuse the session rather than being discarded. No logging, metrics, or tracing infrastructure is introduced — proportionate to a domain library. | ✅ PASS |
| **VII. Specification Adherence** | Every type and rule in the design traces to a numbered requirement; the trace table is in [data-model.md](./data-model.md). The spec carries zero open clarification markers. Three mechanical defaults the spec had to settle (zone boundary precision, time attribution, estimate weight) were recorded in its Assumptions section rather than decided here in code. | ✅ PASS |

**Result**: all gates pass. No entries in Complexity Tracking.

**Post-Phase 1 re-check**: re-evaluated after [data-model.md](./data-model.md),
[contracts/domain-api.md](./contracts/domain-api.md), and [quickstart.md](./quickstart.md) were
written. The design introduced no type, dependency, or indirection beyond what the requirements
force. Principle III remains the one under most pressure — the standing temptation is a
`TrainingLoadCalculator` or an `ITrainingLoadCalculator`, and research R12 records why neither is
justified while exactly one load method is specified. Still ✅ PASS on all seven.

## Project Structure

### Documentation (this feature)

```text
specs/001-training-activity-domain/
├── spec.md              # Feature specification (/speckit-specify output)
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output — decisions, rationale, revisit triggers
├── data-model.md        # Phase 1 output — entities, rules, requirement trace
├── quickstart.md        # Phase 1 output — how to build and validate
├── contracts/
│   └── domain-api.md    # Phase 1 output — public API surface of the domain library
├── checklists/
│   └── requirements.md  # Specification quality checklist (16/16 passing)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
TrainingLoadAnalyzer.sln

src/
└── TrainingLoadAnalyzer.Domain/
    ├── TrainingLoadAnalyzer.Domain.csproj   # net10.0, zero dependencies
    ├── TrainingActivity.cs
    ├── ActivityType.cs
    ├── HeartRateSample.cs
    ├── HeartRateSeries.cs
    ├── HeartRateZone.cs
    ├── TrainingLoad.cs
    └── LoadProvenance.cs

tests/
└── TrainingLoadAnalyzer.Domain.Tests/
    ├── TrainingLoadAnalyzer.Domain.Tests.csproj
    ├── TrainingActivityCreationTests.cs      # User Story 1
    ├── TrainingActivityValidationTests.cs    # User Story 3
    ├── HeartRateSeriesTests.cs               # FR-007, FR-021
    ├── HeartRateZoneTests.cs                 # FR-009 zone boundaries
    ├── MeasuredTrainingLoadTests.cs          # User Story 2, FR-009 – FR-012, FR-016
    └── EstimatedTrainingLoadTests.cs         # User Story 2, FR-013, FR-014
```

**Structure Decision**: A single class library plus its test project, laid out as `src/` and
`tests/` so later features can add `TrainingLoadAnalyzer.Application`,
`.Infrastructure`, `.Api`, and `.Web` to the same solution without moving anything. The preliminary
project plan's nine-project sketch is deliberately not created up front: this feature needs exactly
one production assembly, and five empty projects would be speculative structure under Principle III.
The solution file is created now so that later additions are incremental.

One file per type is used rather than grouping, because the RED step of each TDD cycle creates
exactly one new type and a one-to-one mapping keeps the diff for each cycle small and reviewable.

Two types sketched before Phase 1 were dropped once the design was worked through: a
`MaximumHeartRate` value object, which would have made FR-016's refusal unreachable, and a custom
domain exception, which `ArgumentException.ParamName` already renders unnecessary. Both are recorded
with revisit triggers in [data-model.md](./data-model.md) under "Types deliberately not created".

## Complexity Tracking

> No Constitution Check violations. This table is intentionally empty.
