# Implementation Plan: Load Trends

**Branch**: `004-load-trends` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-load-trends/spec.md`

## Summary

Turn the weekly totals feature 002 produces into a week-by-week trend series: how much each ISO week's
training load moved against the week before it, in points and as a proportion, and whether that
movement is worth calling out.

The technical approach is deliberately small: **two** new types in the existing
`TrainingLoadAnalyzer.Domain` assembly — an enum and a result record — plus a static calculator with
one method. No new project, no new package, no interface, no test double. One pass validates the
history, a second emits one entry per week touching the range.

The design's one load-bearing decision is that the entry **stores five values and derives three**
(research R6). The absolute change, the relative change, and the classification are all computed
properties over the week's own points, the preceding week's points, and whether the week is complete.
That makes four requirements structural rather than behavioural: an entry whose classification
contradicts its numbers is unconstructable, and a partial week cannot be labelled a significant
decrease because the derivation reads completeness before it reads any threshold. It is feature 003's
`Form` lesson applied three times over.

**This feature returns to `decimal`.** Feature 003 departed to `double` because its smoothing factors
are irrational and exact reproduction was unachievable. Nothing here is irrational, FR-033 demands
exactness with no tolerance, and `double` fails it on ordinary totals — `610.4 − 505.7` gives
`104.69999999999999`. Measured on four realistic TRIMP pairs, three fail
([research.md](./research.md) R2). `decimal` also makes FR-004 hard to get wrong, since dividing by
zero throws rather than silently producing the infinity the requirement forbids.

**One specification gap and one design judgement were put to the developer** rather than settled in
code, under Principles VII and III. Both were answered on 2026-09-17; the spec now carries **FR-022b**
— see [Amendments made during planning](#amendments-made-during-planning) and R11/R12 in research.

Full rationale, alternatives, and revisit triggers for each decision are in
[research.md](./research.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`). SDK 10.0.100 confirmed installed.

**Primary Dependencies**: None added. The domain project keeps zero NuGet and zero project references
(research R1, feature 001 research R6). `DateOnly` and `System.Globalization.ISOWeek` are base class
library types reached only through feature 002's `IsoWeek`, not new dependencies. Test project
unchanged: `xunit.v3.mtp-v2` 4.0.1 on the Microsoft.Testing.Platform runner.

**Storage**: N/A. FR-005 forbids reading storage, and persistence remains out of scope.

**Testing**: xUnit v3 with built-in `Assert.*`. No assertion library, no mocking library — nothing in
this feature has a collaborator to substitute. Whole-value equality returns as the default assertion
style, unlike in feature 003, because every member compares exactly (contract, "Notes on the
surface").

**Target Platform**: Cross-platform .NET class library; developed on macOS.

**Project Type**: Class library (pure domain), extending the assembly features 001–003 built.

**Performance Goals**: None stated or implied. Two O(n) passes over the weeks; ten years of history is
522 weeks (research R15). Nothing to measure.

**Constraints**: Every value is `decimal` and every figure exactly reproducible — FR-033 allows no
tolerance at all, which is a stricter bar than feature 003's 0.0001 and the reason `double` is
excluded outright (research R2). Both thresholds are fixed constants, not parameters (FR-010,
research R7). Completeness must be derived from the requested range alone, because the inputs cannot
distinguish an in-progress week from a historical one and the clock is forbidden (research R8). The
domain assembly must still reference no external activity provider (FR-034, Principle II).

**Scale/Scope**: 2 new domain types, 1 static calculator with 1 method, 35 functional requirements,
10 success criteria, 3 user stories. Roughly 120–160 lines of production code expected, on top of
features 001–003.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Gate | Status |
|-----------|------|--------|
| **I. Strict TDD (NON-NEGOTIABLE)** | Every behaviour here is a pure function of two explicit arguments, so no case exists where a test cannot precede the code. `/speckit-tasks` must emit RED/GREEN/REFACTOR/VERIFY steps tied to the spec's given/when/then scenarios. The named risk is sharper than feature 003's: [quickstart.md](./quickstart.md) carries a table of sixteen verified classifications, which is exactly enough to write the whole rule in one go and add tests afterwards. Those figures are the *assertions* tests may use, not a licence to skip the RED step. | ✅ PASS |
| **II. Domain Independence from Strava** | No new dependency of any kind; the domain `.csproj` is unchanged and still declares nothing. No provider vocabulary appears in either new type — see [data-model.md](./data-model.md). This feature never sees an activity, only weekly totals. | ✅ PASS |
| **III. Simplicity Before Abstraction (YAGNI)** | Two types, one method, no interface, no project, no configuration. Five candidate abstractions are rejected with recorded revisit triggers in [data-model.md](./data-model.md) under "Types deliberately not created" — including the project plan's own sketch of `LoadSpikeDetector` and `WeeklyLoadAnalyzer` as separate services, and a `TrendThresholds` options type. FR-029 does some of this work for us by forbidding a second detection pass outright. **One judgement call was escalated rather than decided here**: the basis-combination rule reaching its third copy (R12). | ✅ PASS |
| **IV. Testability** | Nothing under test has a collaborator. Every test builds an array of `WeeklyTrainingLoad` values directly, calls one static method, and asserts on a returned value or a thrown exception. Zero test doubles required. Threshold cases can be expressed without a history at all, by constructing a `WeeklyLoadTrend` directly and reading its derived `Classification`. | ✅ PASS |
| **V. Isolation of External Integrations** | No external integration exists in this feature. Satisfied vacuously, and protected by the spec's Assumptions: loading or persisting anything belongs to the import feature. | ✅ PASS (N/A) |
| **VI. Observability & Deliberate Error Handling** | Five refusals, each naming its rule and the offending week, four sharing a `ParamName` so their messages must tell them apart (research R13, guarantee C41). Nothing is defaulted, repaired, or dropped: a gap in the history is refused rather than zero-filled, and so is a history that stops short — precisely because a manufactured zero week would then be reported as a significant decrease (FR-022b). No logging or metrics infrastructure is introduced. | ✅ PASS |
| **VII. Specification Adherence** | Every type and rule traces to a numbered requirement; the trace table is in [data-model.md](./data-model.md) and covers all 35. **This gate did real work again.** One place where the specification was silent (R11) was found while designing; it was not settled in code but written up with options and a recommendation, put to the developer, answered, and written into the spec as FR-022b before tasks were generated. | ✅ PASS |

**Result**: all gates pass. No entries in Complexity Tracking. Nothing is outstanding; both items
raised during planning were closed by the developer before this plan was finalised.

**Post-Phase 1 re-check**: re-evaluated after [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/domain-api.md](./contracts/domain-api.md), and
[quickstart.md](./quickstart.md) were written. The design added no dependency, no project, and no
indirection beyond the two types the requirements force. Principle III came under more pressure than
in feature 003, from a direction neither earlier feature had: not a new abstraction wanting to be
born, but an existing rule appearing for the third time and inviting a refactor into two completed
features. That was escalated (R12) rather than decided quietly, and the answer — keep the third copy,
record the trigger — is recorded where the completion review will find it. Principle I is the
residual risk here, for the reason in the table above. Still ✅ PASS on all seven.

## Amendments made during planning

Two items were raised with the developer while working the design through, and answered on
2026-09-17. The first changed the specification; the second changed nothing but is recorded so the
completion review examines it deliberately rather than discovering it.

| # | Item | Resolution | Effect |
|---|------|------------|--------|
| **R11** | The spec refused a history starting too late (FR-022) and one with a gap (FR-024), but said nothing about a history that simply **stops before the range's last week** — while FR-028 demanded an entry for every week touching the range. The two could not both hold. | Refuse, naming the shortfall. Zero-filling would manufacture rest weeks that are indistinguishable from real ones and would each be reported as a significant decrease; truncating would silently return fewer entries than asked for. | **FR-022b**, User Story 1 scenario 6, a new edge case, SC-008 widened, guarantee C42 |
| **R12** | Combining two `LoadBasis` values reaches its **third copy** in this feature (002's aggregator, 003's `BasisOver`, and now here). Rule of three says extract; Principle III says do not abstract without need; extracting means editing two signed-off features. | Keep a local copy in feature 004; record the extraction trigger. The three sites share only the final switch — each accumulates differently — so a helper capturing just that is a small, awkward abstraction rather than a clean one. | No code change; trigger recorded for a fourth occurrence or feature 006 |

R11 is the same shape of gap feature 003 hit at its own R11, which is the argument for planning as a
separate phase: invisible while writing requirements, unmissable while designing against them.

## Project Structure

### Documentation (this feature)

```text
specs/004-load-trends/
├── spec.md              # Feature specification (/speckit-specify output, amended with FR-022b)
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output — 15 decisions, rationale, revisit triggers
├── data-model.md        # Phase 1 output — entities, the classification rule, requirement trace
├── quickstart.md        # Phase 1 output — how to build and validate, with verified figures
├── contracts/
│   └── domain-api.md    # Phase 1 output — the API surface this feature adds (C31–C45)
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
    ├── DateRange.cs                          # 002, unchanged — reused as-is, gives FR-026 free
    ├── IsoWeek.cs                            # 002, unchanged — Monday is this feature's ordering key
    ├── DailyTrainingLoad.cs                  # 002, unchanged — not used here
    ├── WeeklyTrainingLoad.cs                 # 002, unchanged — this feature's input
    ├── TrainingLoadAggregator.cs             # 002, unchanged
    ├── DailyTrainingMetrics.cs               # 003, unchanged — not used here
    ├── TrainingMetricsCalculator.cs          # 003, unchanged
    ├── TrendClassification.cs                # NEW — FR-007
    ├── WeeklyLoadTrend.cs                    # NEW — FR-002 – FR-004, FR-008 – FR-017, FR-030
    └── TrainingLoadTrendCalculator.cs        # NEW — the single entry point

tests/
└── TrainingLoadAnalyzer.Domain.Tests/        # existing project, unchanged .csproj
    ├── ...                                   # 001–003's test files, unchanged and staying green
    ├── WeeklyLoadTrendTests.cs               # NEW — User Story 1, the two changes
    ├── TrendClassificationTests.cs           # NEW — User Story 2, the thresholds
    ├── TrendReliabilityTests.cs              # NEW — User Story 3, completeness and basis
    └── TrendInputTests.cs                    # NEW — FR-022 – FR-026, the five refusals
```

**Structure Decision**: Everything lands in the two projects that already exist. No
`TrainingLoadAnalyzer.Application` project is created — the reasoning is recorded once in the project
plan's section 22.2 and has not changed: this feature has no use case to orchestrate, no port to
isolate, and no caller outside the domain. None of its three conditions has moved closer.

One file per type continues from features 001–003, because each RED step introduces at most one new
type and a one-to-one mapping keeps each cycle's diff small. Test files are split by user story so the
P1/P2/P3 slices stay independently runnable, with refusals in a fourth file — the same four-way split
feature 003 used.

Note that `TrendClassificationTests` can be written against `WeeklyLoadTrend` alone, with no history
and no calculator, because the classification is a derived property (research R6). That makes User
Story 2 genuinely independently testable in the sense the spec template asks for, rather than
nominally so.

## Complexity Tracking

> No Constitution Check violations. This table is intentionally empty.
>
> The one decision a reviewer is most likely to challenge — a third copy of the basis-combination
> rule rather than an extraction — is not an added abstraction but a deliberately *declined* one. It
> was put to the developer and answered on 2026-09-17, with the extraction trigger recorded in
> [research.md](./research.md) R12 and in data-model's "Types deliberately not created".
