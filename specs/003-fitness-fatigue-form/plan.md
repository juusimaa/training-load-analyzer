# Implementation Plan: Fitness, Fatigue, and Form

**Branch**: `003-fitness-fatigue-form` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-fitness-fatigue-form/spec.md`

## Summary

Turn the gap-free daily load series feature 002 produces into a day-by-day series of the three figures
the MVP exists for: Fitness, Fatigue, and Form. Each entry carries the three numbers, whether they are
yet reliable, and how much of the load behind them was measured rather than estimated.

The technical approach is deliberately small: **two** new types in the existing
`TrainingLoadAnalyzer.Domain` assembly — a result record and a static calculator with one method — no
new project, no new package, no interface, no test double. One forward pass over the history advances
both exponentially weighted averages from a zero seed; Form and its basis are computed properties, so
they cannot drift from what they are derived from.

Three load-model decisions were made by the developer during `/speckit-specify` and are fixed in
FR-005, FR-012, and FR-014: the true exponential smoothing factor `1 − e^(−1/N)` rather than the
`1 ÷ N` approximation, a zero seed, and a 42-day warm-up. The first of those has a consequence that
shapes this whole plan: the factor is irrational, so these figures cannot be reproduced by hand
exactly the way features 001 and 002 require of their point totals. FR-029 states a tolerance of
0.0001 points in its place, and with exactness gone, so is the reason `decimal` was chosen — **the
three metrics are `double`**, while the loads feeding them stay `decimal`. That departure, and the
measurements backing it, are in [research.md](./research.md) R2.

**Two specification gaps were found while designing.** Under Principle VII they were put to the
developer rather than settled in code; both were answered on 2026-09-17 and the specification was
amended with **FR-019a**, **FR-019b**, and **FR-022a** before tasks were generated — see
[Amendments made during planning](#amendments-made-during-planning) below and R10/R11 in research.

Full rationale, alternatives, and revisit triggers for each decision are in
[research.md](./research.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`). SDK 10.0.100 confirmed installed.

**Primary Dependencies**: None added. The domain project keeps zero NuGet and zero project references
(research R1, feature 001 research R6). `System.Math` and `DateOnly` are base class library types, not
dependencies. Test project unchanged: `xunit.v3.mtp-v2` 4.0.1 on the Microsoft.Testing.Platform runner.

**Storage**: N/A. FR-007 forbids reading storage, and persistence remains out of scope.

**Testing**: xUnit v3 with built-in `Assert.*`. No assertion library, no mocking library — nothing in
this feature has a collaborator to substitute. One new assertion form:
`Assert.Equal(double, double, tolerance: 0.0001)`, confirmed present in `xunit.v3.assert` 4.0.1.

**Target Platform**: Cross-platform .NET class library; developed on macOS.

**Project Type**: Class library (pure domain), extending the assembly features 001 and 002 built.

**Performance Goals**: None stated or implied. One forward pass over the history, plus a bounded
backward scan per day for the bases — about 179,000 comparisons for a 10-year series (research R9,
R15).

**Constraints**: The figures are `double` and carry a stated tolerance of 0.0001 points (FR-029),
which is a deliberate departure from the `decimal` exactness of features 001 and 002 and the single
most reviewable decision here (research R2). The smoothing factors must be applied at ten or more
significant figures (FR-029a) — `Math.Exp` gives ~17, verified. The domain assembly must still
reference no external activity provider (FR-030, Principle II). Basis windows must be bounded at 42
and 7 days, or one early estimated session would mark every later day `Mixed` forever (FR-019).

**Scale/Scope**: 2 new domain types, 1 static calculator with 1 method, 34 functional requirements,
13 success criteria, 3 user stories. Roughly 100–150 lines of production code
expected, on top of features 001 and 002.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Gate | Status |
|-----------|------|--------|
| **I. Strict TDD (NON-NEGOTIABLE)** | Every behaviour here is a pure function of two explicit arguments, so no case exists where a test cannot precede the code. `/speckit-tasks` must emit RED/GREEN/REFACTOR/VERIFY steps tied to the spec's given/when/then scenarios. The named risk is the same as feature 002's and slightly sharper: [research.md](./research.md) R3 and R12 describe the shape the implementation converges on, and [quickstart.md](./quickstart.md) lists verified expected figures. Those are the *assertions* tests may use; they are not a licence to write the loop before a test asks for it. | ✅ PASS |
| **II. Domain Independence from Strava** | No new dependency of any kind; the domain `.csproj` is unchanged and still declares nothing. No provider vocabulary appears in either new type — see [data-model.md](./data-model.md). This feature does not even see an activity, only totals. | ✅ PASS |
| **III. Simplicity Before Abstraction (YAGNI)** | Two types, one method, no interface, no project, no configuration. Seven candidate abstractions are rejected with recorded revisit triggers in [data-model.md](./data-model.md) under "Types deliberately not created" — including the project plan's own sketch of three separate calculators, a `DailyLoadHistory` type, and `Fitness`/`Fatigue`/`Form` wrapper types. Notably, feature 002 recorded a `TrainingLoadSeries` wrapper with *this* feature as its revisit trigger; the trigger did not fire, and it is rejected again (research R14). | ✅ PASS |
| **IV. Testability** | Nothing under test has a collaborator. Every test builds a list of `DailyTrainingLoad` values directly, calls one static method, and asserts on a returned value or a thrown exception. Zero test doubles required. The one testing subtlety is asserting `double` with a tolerance rather than by whole-value equality, recorded in research R7 and demonstrated in quickstart. | ✅ PASS |
| **V. Isolation of External Integrations** | No external integration exists in this feature. Satisfied vacuously, and protected by Out of Scope: loading or persisting anything belongs to the import feature. | ✅ PASS (N/A) |
| **VI. Observability & Deliberate Error Handling** | Six refusals, each naming its rule and the offending day, five of them sharing a `ParamName` so their messages must tell them apart (research R13, guarantee C28). Nothing is defaulted, repaired, or dropped: a history with a gap is refused rather than filled with zeroes, precisely because this feature cannot tell an absent day from an aggregation defect. No logging or metrics infrastructure is introduced — proportionate to a domain library. | ✅ PASS |
| **VII. Specification Adherence** | Every type and rule traces to a numbered requirement; the trace table is in [data-model.md](./data-model.md). **This is the gate that did the most work.** Two places where the specification was ambiguous (R10) or silent (R11) were found while designing. Neither was settled in code: both were written up with options and a recommendation, put to the developer, and answered, and the specification now carries FR-019a, FR-019b, and FR-022a as a result. That is exactly what this principle asks for. | ✅ PASS |

**Result**: all gates pass. No entries in Complexity Tracking. Nothing is outstanding; the two items
Principle VII raised were closed by amending the spec before tasks were generated.

**Post-Phase 1 re-check**: re-evaluated after [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/domain-api.md](./contracts/domain-api.md), and
[quickstart.md](./quickstart.md) were written. The design added no dependency, no project, and no
indirection beyond the two types the requirements force — one fewer than feature 002 needed, in a
feature with more requirements. Principle III came under less pressure than expected: working the
design through actually *removed* a type that feature 002 had anticipated. The pressure moved to
Principle VII instead, which found two gaps in the specification, and to Principle I, because the
verified figures in quickstart make it tempting to write the loop first and paste the numbers in
afterwards. Still ✅ PASS on all seven; the Principle VII items were resolved by amendment rather than
left open.

## Amendments made during planning

Two gaps in the specification were found while working the design through. Neither was settled in
code: both were put to the developer, answered on 2026-09-17, and written into
[spec.md](./spec.md) before `/speckit-tasks` ran. Full analysis in [research.md](./research.md) R10
and R11.

| # | Gap | Resolution | New requirement |
|---|-----|------------|-----------------|
| **R10** | FR-019 said Form's basis is `Mixed` when Fitness's and Fatigue's "disagree". Taken literally, a day whose Fitness basis is `Measured` and whose Fatigue basis is `None` — trained three weeks ago, rested since — reported Form as `Mixed`, though nothing was mixed. | A basis of `None` contributes nothing, as FR-018 already says for rest days. Because Fatigue's 7-day window always sits inside Fitness's 42-day window, this makes `FormBasis` *necessarily* equal to `FitnessBasis` — so it is a derived property, and the requirement cannot be violated. | **FR-019a**, **FR-019b**, plus User Story 3 scenario 6 |
| **R11** | The spec's Edge Cases listed a history that stops before the range's last day, but no requirement resolved it. | Refuse, naming the shortfall, mirroring FR-022 at the other end — the same reasoning the spec's Assumptions already apply to a gap. Truncating instead would return fewer entries than the range asked for, which FR-008 and SC-001 exist to prevent. | **FR-022a** |

Both are the kind of gap that is invisible while writing a specification and obvious while designing
against it, which is the argument for planning as a separate phase rather than going straight to
tasks.

## Project Structure

### Documentation (this feature)

```text
specs/003-fitness-fatigue-form/
├── spec.md              # Feature specification (/speckit-specify output)
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output — decisions, rationale, revisit triggers, 2 open items
├── data-model.md        # Phase 1 output — entities, rules, requirement trace
├── quickstart.md        # Phase 1 output — how to build and validate, with verified figures
├── contracts/
│   └── domain-api.md    # Phase 1 output — the API surface this feature adds (C19–C30)
├── checklists/
│   └── requirements.md  # Specification quality checklist (16/16 passing)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
└── TrainingLoadAnalyzer.Domain/              # existing project, unchanged .csproj
    ├── TrainingActivity.cs                   # 001, unchanged
    ├── ActivityType.cs                       # 001, unchanged
    ├── HeartRateSample.cs                    # 001, unchanged
    ├── HeartRateSeries.cs                    # 001, unchanged
    ├── HeartRateZone.cs                      # 001, unchanged
    ├── TrainingLoad.cs                       # 001, unchanged
    ├── LoadProvenance.cs                     # 001, unchanged
    ├── LoadBasis.cs                          # 002, unchanged — reused as-is
    ├── DateRange.cs                          # 002, unchanged — reused as-is
    ├── IsoWeek.cs                            # 002, unchanged — not used here
    ├── DailyTrainingLoad.cs                  # 002, unchanged — this feature's input
    ├── WeeklyTrainingLoad.cs                 # 002, unchanged — not used here
    ├── TrainingLoadAggregator.cs             # 002, unchanged
    ├── DailyTrainingMetrics.cs               # NEW — FR-001, FR-002, FR-003, FR-013, FR-016
    └── TrainingMetricsCalculator.cs          # NEW — the single entry point

tests/
└── TrainingLoadAnalyzer.Domain.Tests/        # existing project, unchanged .csproj
    ├── ...                                   # 001's and 002's test files, unchanged and staying green
    ├── TrainingMetricsTests.cs               # NEW — User Story 1, the three figures and the recurrence
    ├── MetricsSeriesTests.cs                 # NEW — User Story 2, the day-by-day series
    ├── MetricsBasisTests.cs                  # NEW — User Story 3, bases and reliability
    └── MetricsInputTests.cs                  # NEW — FR-021 to FR-026, the six refusals
```

**Structure Decision**: Everything lands in the two projects that already exist. No
`TrainingLoadAnalyzer.Application` project is created — the reasoning is recorded once in the project
plan's section 22.2 and has not changed: this feature has no use case to orchestrate, no port to
isolate, and no caller outside the domain.

One file per type continues from features 001 and 002, because each RED step introduces at most one new
type and a one-to-one mapping keeps each cycle's diff small. Test files are split by user story so the
P1/P2/P3 slices stay independently runnable, with refusals in a fourth file — the same split feature
002 used, where `DateRangeTests` held the refusals.

Only two production files appear, against feature 002's six, and that is the point rather than an
oversight: the requirements here are dense but they describe one calculation, not five concepts.

## Complexity Tracking

> No Constitution Check violations. This table is intentionally empty.
>
> The one decision that looks like a violation — `double` where the rest of the domain is `decimal` —
> is not an added abstraction or an unjustified dependency; it is the numeric type the specification's
> own tolerance (FR-029) calls for, measured and recorded in [research.md](./research.md) R2.
