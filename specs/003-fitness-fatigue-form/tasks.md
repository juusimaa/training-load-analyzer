---

description: "Task list for feature 003 — Fitness, Fatigue, and Form"
---

# Tasks: Fitness, Fatigue, and Form

**Input**: Design documents from `/specs/003-fitness-fatigue-form/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/domain-api.md](./contracts/domain-api.md),
[quickstart.md](./quickstart.md)

**Tests**: MANDATORY. The template treats test tasks as optional; Constitution Principle I (Strict
TDD, NON-NEGOTIABLE) overrides that. Every production member below arrives via a failing test.

**Organization**: Tasks are grouped by user story. Each task is one step of a
RED → GREEN → REFACTOR → VERIFY cycle, tied to a numbered acceptance scenario in
[spec.md](./spec.md).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: US1 / US2 / US3, mapping to the user stories in spec.md
- **RED**: write a test that fails, and confirm it fails *for the stated reason*
- **GREEN**: the minimum production code that makes it pass — nothing more
- **CONFIRM**: a test expected to pass with no new production code. It pins behaviour the design
  already implies. If it fails, that is a real defect, not a cue to write more code.
- **VERIFY**: run the whole suite and check the cycle's discipline held

## Path Conventions

The two projects that already exist, per [plan.md](./plan.md):
`src/TrainingLoadAnalyzer.Domain/` and `tests/TrainingLoadAnalyzer.Domain.Tests/`. This feature
creates **no** project and adds **no** package.

## Constructing a daily load history

Every test below builds an `IReadOnlyList<DailyTrainingLoad>` by hand. Feature 002's type is
`DailyTrainingLoad(DateOnly Day, decimal Points, int ActivityCount, LoadBasis Basis)`, constructed
directly — this feature never touches `TrainingActivity`, `TrainingLoadAggregator`, or a maximum
heart rate.

Four day shapes are used throughout, and the differences between them matter:

| Shape | Constructed as | Means |
|-------|----------------|-------|
| A training day | `(day, 100m, 1, LoadBasis.Measured)` | load 100, measured |
| An estimated day | `(day, 100m, 1, LoadBasis.Estimated)` | load 100, estimated from duration |
| A rest day | `(day, 0m, 0, LoadBasis.None)` | no session at all — contributes no basis (FR-018) |
| A zero-point session | `(day, 0m, 1, LoadBasis.Measured)` | a real measured session scoring 0 — **does** contribute its basis |

A helper that builds a continuous run of identical days keeps the tests readable; write it in the
test project when the second test needs it, not before.

## Expected figures

Every number below was computed on the installed SDK (.NET 10.0.100) and is recorded in
[quickstart.md](./quickstart.md). Assert them with `Assert.Equal(expected, actual, tolerance:
0.0001)` — never with exact equality, per FR-029 and research R7.

| Situation (all loads 100 unless stated) | Fitness | Fatigue | Form |
|------------------------------------------|---------|---------|------|
| Day 1 from a zero seed | `2.352831335` | `13.31221002` | `−10.95937869` |
| Day 2, a rest day | `2.2974731819` | `11.5400606675` | `−9.2425874856` |
| After 42 days | `63.212056` | — | — |
| After 200 days (settled) | `99.1451` | `100.0000` | `−0.8549` |
| Settled, then 7 rest days | `83.9245` | `36.7879` | `+47.1365` |
| Settled, then 7 days at load 200 | `114.6281` | `163.2121` | `−48.5839` |
| After 365 days | `99.9832` | `100.0000` | `−0.0168` |
| Day 60 of a 90-day run (2026-03-01) | `76.034896` | `99.981056` | `−23.946159` |

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the starting point. No scaffolding — this feature adds no project and no
package, per [research.md](./research.md) R1.

- [ ] T001 Establish the baseline: `dotnet test` reports 103 passing, 0 failing — features 001 and 002. Any red here is a pre-existing problem to fix before adding to it
- [ ] T002 Confirm `src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj` still contains zero `PackageReference` and zero `ProjectReference` elements, and that `grep -rn "double" src/TrainingLoadAnalyzer.Domain/ --include='*.cs'` returns **nothing**. Both are mechanical guards: the first for Principle II, FR-030 and C30; the second establishes that every `double` added from here on is one this feature deliberately introduced (research R2). If any task below appears to need a package or a new project, that is design drift — stop and re-read research R1

**Checkpoint**: 001 and 002 green, no metrics code exists yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Intentionally empty.** There is no foundational production code in this feature. Every new member
is created by a user story's GREEN step, because writing one before a test demands it would violate
Principle I.

User Story 1 is the de facto foundation: it creates `DailyTrainingMetrics` and
`TrainingMetricsCalculator`, which Stories 2 and 3 both extend. This is recorded honestly in
Dependencies below rather than papered over with a claim of full story independence.

---

## Phase 3: User Story 1 - Read today's fitness, fatigue, and form (Priority: P1) 🎯 MVP

**Goal**: A daily load history yields the three figures, advancing day by day from a zero seed under
FR-005's recurrence.

**Independent Test**: Feed a hand-built daily load series with known values through the calculation
and check the final day's three figures against numbers worked out from the stated formula; include
a series ending in a hard block and one ending in a rest week.

### The first Fitness figure

- [ ] T003 [US1] RED: write the test — a history of one day, `(2026-03-01, 100m, 1, Measured)`, over the range `2026-03-01` to `2026-03-01`, produces one entry whose `Fitness` is `2.352831335` within `0.0001`. Confirm it fails to compile because `DailyTrainingMetrics` and `TrainingMetricsCalculator` do not exist — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`
- [ ] T004 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/DailyTrainingMetrics.cs` as `public readonly record struct DailyTrainingMetrics(DateOnly Day, double Fitness)`. **No `Fatigue`, no `Form`, no `IsReliable`, no bases** — nothing demands them yet, and adding them now would be production code without a failing test
- [ ] T005 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs` as a `public static class` with `public static IReadOnlyList<DailyTrainingMetrics> Calculate(IReadOnlyList<DailyTrainingLoad> history, DateRange range)`. Walk `history` from index 0, carrying a `double fitness` initialised to `0` (FR-012), and for each day apply `fitness += ((double)day.Points - fitness) * AlphaFitness`, appending an entry per history day. Declare `private static readonly double AlphaFitness = 1.0 - Math.Exp(-1.0 / 42.0);` — write the formula, **not** a transcribed literal and **not** `1.0 / 42.0` (FR-005, FR-029a, research R3). `range` is not consulted yet; T021 is what forces it

### Fatigue and Form

- [ ] T006 [US1] RED: the same one-day history produces `Fatigue` of `13.31221002` within `0.0001`. Confirm it fails to compile because `DailyTrainingMetrics` has no `Fatigue` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`
- [ ] T007 [US1] GREEN: add `double Fatigue` to the record's positional parameters and a second accumulator to the loop, with `private static readonly double AlphaFatigue = 1.0 - Math.Exp(-1.0 / 7.0);` — files: `src/TrainingLoadAnalyzer.Domain/DailyTrainingMetrics.cs`, `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`
- [ ] T008 [US1] RED: the same one-day history produces `Form` of `−10.95937869` within `0.0001`. Confirm it fails to compile because there is no `Form` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`
- [ ] T009 [US1] GREEN: add `public double Form => Fitness - Fatigue;` to `src/TrainingLoadAnalyzer.Domain/DailyTrainingMetrics.cs` as a **computed property**. It must **not** become a constructor parameter and the calculator must **not** accumulate it — FR-003 is what makes SC-003 structural, and T016 asserts it

### Proving the two tests that could pass against a wrong implementation

These are the feature's discriminating tests. Both are expected to pass; neither is worth anything
until you have watched it fail against the plausible wrong implementation.

- [ ] T010 [US1] CONFIRM: 42 consecutive training days over a matching range produce a final `Fitness` of `63.212056` within `0.0001`. **Before accepting it, prove it discriminates**: temporarily change `AlphaFitness` to `1.0 / 42.0`, confirm this test fails (that approximation gives `63.654405`, a gap of 0.44 — more than four thousand times the tolerance), then revert. The `1 ÷ N` form is the single most likely wrong implementation of FR-005 and looks entirely reasonable in a diff — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`
- [ ] T011 [US1] CONFIRM: assert FR-012 explicitly — for the one-day history of T003, `Fitness` is `2.352831335`, which is `100 × α` and not `100`. **Prove it discriminates**: temporarily seed `fitness` from the first day's own load instead of `0` — a plausible-looking "warm start" — confirm this test fails, then revert — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`

### The three figures behave as the athlete expects

- [ ] T012 [US1] CONFIRM: scenario 1 — a history of 365 identical training days produces a final `Fitness` of `99.9832` and `Fatigue` of `100.0000`, both within 1% of the daily load of 100, and `Form` of `−0.0168`, within 1% of 100 of zero (SC-004) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`
- [ ] T013 [US1] CONFIRM: scenario 2 — 200 training days at load 100 followed by 7 days at load 200 give `Fitness` `114.6281`, `Fatigue` `163.2121` and `Form` `−48.5839`. Assert that Fatigue rose further than Fitness across those seven days and that Form is negative on the last — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`
- [ ] T014 [US1] CONFIRM: scenario 3 — 200 training days at load 100 followed by 7 rest days give `Fitness` `83.9245` (down 16% from `99.1451`) and `Fatigue` `36.7879` (down 63% from `100.0000`), with `Form` `+47.1365`. Assert Fatigue fell by the larger **proportion** and Form is positive (SC-005). If Form does not turn positive here, the two time constants are the wrong way round — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`
- [ ] T015 [US1] CONFIRM: scenario 4 — over a varied 60-day history, **every** entry satisfies `Form == Fitness - Fatigue` exactly, asserted with `Assert.Equal` on the raw `double`s and no tolerance. This one comparison is exact precisely because `Form` is derived rather than accumulated (C20, SC-003) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`
- [ ] T016 [US1] CONFIRM: assert FR-003 structurally by reflection — the single public constructor of `DailyTrainingMetrics` has no parameter named `Form`, and `Form` has no setter. Use plain reflection, not an architecture-test package. This is what stops a later change quietly turning Form into stored state — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`
- [ ] T017 [US1] CONFIRM: scenario 5 — calculating twice over the same history and range produces element-by-element equal results, with no dependence on the current date (FR-027, C26, SC-010) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`

### Refusing a missing argument

- [ ] T018 [P] [US1] RED: `Calculate` throws `ArgumentNullException` with `ParamName` `"history"` for a null history and `"range"` for a null range. Confirm both fail — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsInputTests.cs`
- [ ] T019 [US1] GREEN: add both null guards to `Calculate`, before any other work — file: `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`
- [ ] T020 [US1] VERIFY: run `dotnet test` — all green, including the 103 tests from features 001 and 002. Then confirm the discipline held: `src/` gained exactly two files, `DailyTrainingMetrics` still has only `Day`, `Fitness`, `Fatigue` and the derived `Form`, and no interface, service instance, history type, or custom exception was created

**Checkpoint**: The three figures are correct for any history. The range is still ignored, and nothing
says how far a figure can be trusted.

---

## Phase 4: User Story 2 - Follow how the three metrics evolved (Priority: P2)

**Goal**: One entry per calendar day of the requested range, no gaps, built forward from history that
may start before the range — and a malformed history refused rather than repaired.

**Independent Test**: Calculate over a range spanning several weeks and confirm there is exactly one
entry per calendar day, in order, with no gaps — including across a stretch with no training — and
that each day's figures follow from the day before by the stated formula.

**Depends on**: Phase 3 (needs the calculator and the result type to exist).

### The range decides what comes back; the history decides what is counted

- [ ] T021 [US2] RED: write the test for scenario 1 — a history of 90 training days from `2026-01-01` to `2026-03-31` with a requested range of `2026-03-01` to `2026-03-31` produces **exactly 31** entries, the first dated `2026-03-01`. Assert that its `Fitness` is `76.034896` within `0.0001` — two months of accumulated training already behind it, not a zero seed. Confirm it fails because 90 entries are returned (FR-008, FR-011, FR-024, C19) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsSeriesTests.cs`
- [ ] T022 [US2] GREEN: in `Calculate`, keep iterating from `history[0]` but append an entry only when the day falls within `range`. The accumulators must keep advancing across the days before the range — filtering the *output*, never the iteration (FR-011, FR-024) — file: `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`
- [ ] T023 [US2] CONFIRM: the other half of FR-024 — a history running to `2026-03-31` with a range ending `2026-03-15` produces entries up to and including `2026-03-15` and no further. Also assert the last day of the 90-day history over its full range has `Fitness` `88.268083`, so the trailing days are not silently dropped from the accumulation — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsSeriesTests.cs`

### Rest days are inputs, not gaps

- [ ] T024 [US2] CONFIRM: scenario 3 — a history of one training day `(2026-03-01, 100m, 1, Measured)` followed by one rest day `(2026-03-02, 0m, 0, None)` gives `2026-03-02` a `Fitness` of `2.2974731819` and a `Fatigue` of `11.5400606675`, both **lower** than the day before. An implementation that iterates only the days that had training leaves them unchanged (FR-009, C22) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsSeriesTests.cs`
- [ ] T025 [US2] CONFIRM: scenario 2 — a 30-day history in which every day is a rest day produces 30 entries, each with `Fitness` and `Fatigue` of exactly `0`, and none omitted. Then the same with 10 training days followed by 30 rest days: every one of the 30 has an entry, and the figures decrease strictly day by day toward zero without reaching it (FR-009, SC-006) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsSeriesTests.cs`
- [ ] T026 [US2] CONFIRM: scenario 4 — over a varied 60-day history, re-derive each entry from the one before it and that day's load using FR-005's recurrence, and assert agreement within `0.0001` for **every** consecutive pair. This is SC-002 asserted across the whole series rather than at one point (C21) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsSeriesTests.cs`
- [ ] T027 [US2] CONFIRM: scenario 5 — a range whose start equals its end produces exactly one entry and is not an error (SC-001) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsSeriesTests.cs`

### Refusing a history that cannot produce the requested range

Four refusals, each with its own rule and message. They share `ParamName` `"history"` with the null
guard from T018, which is why T036 exists.

- [ ] T028 [P] [US2] RED: assert FR-022 — a history starting `2026-03-05` with a range starting `2026-03-01` throws `ArgumentException` with `ParamName` `"history"`, and the message names both dates. Confirm it fails because no guard exists — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsInputTests.cs`
- [ ] T029 [US2] GREEN: add the guard comparing `history[0].Day` against `range.Start` — file: `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`
- [ ] T030 [US2] RED: assert FR-022 for an **empty** history — it throws `ArgumentException` with `ParamName` `"history"`, and the message says the history is empty rather than reporting a nonexistent date. An empty history fails to reach back to the range's first day, so this is FR-022's rule and not a separate one (research R13). Confirm it fails, most likely with an index-out-of-range from T029's guard — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsInputTests.cs`
- [ ] T031 [US2] GREEN: add the empty-history guard **before** the T029 guard that indexes `history[0]` — file: `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`
- [ ] T032 [US2] RED: assert FR-022a — a history ending `2026-03-20` with a range ending `2026-03-31` throws `ArgumentException` with `ParamName` `"history"`, naming the shortfall. Confirm it fails: the current code returns a short series instead, which is exactly the silent truncation FR-008 and SC-001 exist to prevent — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsInputTests.cs`
- [ ] T033 [US2] GREEN: add the guard comparing the last history day against `range.End` — file: `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`
- [ ] T034 [US2] RED: assert FR-023 in all three forms, as three test cases — a history missing `2026-03-03` between `03-02` and `03-04`; a history containing `2026-03-02` twice; and a history with `03-04` before `03-03`. Each throws `ArgumentException` with `ParamName` `"history"`, and the message names the position and the two days at the break. Confirm all three fail: today the gap silently skips a day and changes every figure after it — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsInputTests.cs`
- [ ] T035 [US2] GREEN: add a single forward scan asserting `history[i].Day == history[i-1].Day.AddDays(1)` for every `i`, refusing on the first break. One check catches all three forms — a gap, a duplicate and an inversion are the same violation seen from different sides. Refuse rather than repair: this feature cannot tell a genuinely absent day from an aggregation defect (spec Assumptions, FR-023) — file: `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`
- [ ] T036 [US2] CONFIRM: assert feature 001's contract guarantee C3 for the five refusals sharing `ParamName` `"history"` — null, empty, starts too late, ends too early, and not contiguous. Collect the five messages and assert they are pairwise distinct and that each names its own rule, so a test for one cannot pass against another (FR-026, C28) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsInputTests.cs`
- [ ] T037 [US2] CONFIRM: assert FR-025 — a 60-day history in which every day is `(day, 0m, 0, None)` returns all 60 entries with both figures at `0`, not an empty result and not a refusal. A history of all zeroes is valid input; only a *malformed* history is refused (C29) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsInputTests.cs`
- [ ] T038 [US2] VERIFY: run `dotnet test` — all green, including every US1 test. Confirm US1's figures are unchanged by T022: filtering the output must not have altered any accumulation

**Checkpoint**: The series is correct, complete, and refuses what it cannot compute. Nothing yet says
how far a figure can be trusted.

---

## Phase 5: User Story 3 - Know how far to trust the three numbers (Priority: P3)

**Goal**: Every entry says whether enough history stands behind it, and how much of the load behind
each figure was measured rather than estimated.

**Independent Test**: Calculate over series built from measured loads only, estimated only, and a
mixture, and confirm each day reports the corresponding basis; separately, calculate over a series
shorter than the warm-up and confirm every day is marked not yet reliable.

**Depends on**: Phases 3 and 4 (extends the result type and the loop).

**Note on churn**: T040, T044, T046 and T053 add members to `DailyTrainingMetrics`. Because no test
constructs that type positionally — tests build `DailyTrainingLoad` and *read* metrics — existing
tests should keep compiling. The reflection assertion in T016 is the one that must be revisited, and
T054 extends it rather than weakening it.

### Whether enough history stands behind the figures

- [ ] T039 [P] [US3] RED: assert FR-013 and FR-014 — for a history beginning `2026-01-01`, the entry for `2026-02-11` (the 42nd day) has `IsReliable` **false** and the entry for `2026-02-12` (the 43rd) has it **true**. Confirm it fails to compile because there is no `IsReliable` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T040 [US3] GREEN: add `bool IsReliable` to the record's positional parameters, computed in the loop as `day >= history[0].Day.AddDays(42)`. Declare the 42 as a named private constant, not a bare literal at the comparison — files: `src/TrainingLoadAnalyzer.Domain/DailyTrainingMetrics.cs`, `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`
- [ ] T041 [US3] CONFIRM: assert SC-007 across the whole boundary — for a 100-day history from `2026-01-01`, every entry from day 1 to day 42 has `IsReliable` false and every entry from day 43 to day 100 has it true, with no exceptions on either side. Off-by-one here changes no figure, so only this flag reveals it — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T042 [US3] CONFIRM: assert FR-015 — an entry inside the warm-up still carries real `Fitness`, `Fatigue` and `Form` figures, equal to those the same history produces when the warm-up flag is ignored. The figures are produced and qualified, never withheld, blanked, or refused (C23) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`

### How much of the load behind a figure was measured

- [ ] T043 [US3] RED: assert scenario 1 — a history of 50 measured training days reports `FitnessBasis` of `LoadBasis.Measured` on its last day. Confirm it fails to compile because there is no `FitnessBasis` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T044 [US3] GREEN: add `LoadBasis FitnessBasis` to the record and compute it by scanning back over the **42 days ending on the day itself**, truncated at the history's start, combining the `Basis` of every day whose own basis is not `None`. Reuse feature 002's combination rule unchanged: any measured plus any estimated is `Mixed`, all of one kind is that kind, nothing is `None` (FR-017, FR-018, FR-019). A plain backward scan — no rolling counters, no optimisation (research R9) — files: `src/TrainingLoadAnalyzer.Domain/DailyTrainingMetrics.cs`, `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`
- [ ] T045 [US3] RED: assert that the two windows are different lengths — in a history of 50 measured days where the day 8 days before the last is estimated, the last day has `FitnessBasis` `Mixed` but `FatigueBasis` `Measured`, because that day is outside the 7-day window. Confirm it fails to compile because there is no `FatigueBasis` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T046 [US3] GREEN: add `LoadBasis FatigueBasis`, computed by the same scan over **7 days** (FR-019). Factor the window scan into one private method taking the window length rather than writing it twice — files: `src/TrainingLoadAnalyzer.Domain/DailyTrainingMetrics.cs`, `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`
- [ ] T047 [US3] CONFIRM: scenario 2 — a history mixing measured and estimated days within both windows reports both bases as `Mixed`. Add the FR-017 edge: a single estimated day among 41 measured ones still makes `FitnessBasis` `Mixed` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T048 [US3] CONFIRM: a history of only estimated days reports both bases as `Estimated`, not `Mixed` and not `Measured` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T049 [US3] CONFIRM: assert SC-009 and C25 — the window **forgets**. Take one estimated day in an otherwise measured history and assert across the series that `FatigueBasis` is `Mixed` for exactly the 7 entries from that day onward and `Measured` after, and `FitnessBasis` is `Mixed` for exactly 42 and `Measured` after. An implementation combining over the whole history marks everything `Mixed` forever after the athlete's first ride without a heart-rate strap — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T050 [US3] CONFIRM: scenario 5 and FR-020 — a history of 60 rest days reports both bases as `None` on every entry, and after 10 training days followed by 50 rest days the last entry is `None` for both (nothing in either window carried training) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T051 [US3] CONFIRM: assert FR-018's distinction, which is the reason the basis has four states. A window containing only a rest day `(day, 0m, 0, None)` reports `None`; a window containing only a zero-point session `(day, 0m, 1, Measured)` reports `Measured`. Assert in the same test that the two histories produce **identical** `Fitness` and `Fatigue` — the figures cannot tell them apart, and only the basis can (C22, quickstart item 10) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`

### Form's basis

- [ ] T052 [US3] RED: assert scenario 6 and FR-019a — in a history whose last 42 days are measured but whose last 7 days are all rest, the last entry has `FitnessBasis` `Measured`, `FatigueBasis` `None`, and `FormBasis` **`Measured`**. An empty window contributes nothing; it does not count as disagreement. Confirm it fails to compile because there is no `FormBasis` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T053 [US3] GREEN: add `public LoadBasis FormBasis => FitnessBasis;` to `src/TrainingLoadAnalyzer.Domain/DailyTrainingMetrics.cs` as a **computed property**, exactly as `Form` is. Per FR-019b the two can never differ, because Fatigue's 7-day window always sits inside Fitness's 42-day one — deriving it is what makes that impossible to violate. Do **not** add a third window scan
- [ ] T054 [US3] CONFIRM: assert FR-019b and C24a two ways. First, across a varied 120-day history mixing measured, estimated, zero-point and rest days, `FormBasis == FitnessBasis` on **every** entry. Second, extend T016's reflection assertion: the constructor of `DailyTrainingMetrics` has no parameter named `Form` **or** `FormBasis`, and neither has a setter — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T055 [US3] CONFIRM: assert SC-008 — over a varied history, every entry carries an `IsReliable` and all three bases, and no figure is reachable without them. Assert positively that each entry's three bases are defined `LoadBasis` values rather than relying on the type system alone — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MetricsBasisTests.cs`
- [ ] T056 [US3] VERIFY: run `dotnet test` — all green, including every US1 and US2 test. Confirm the record now has exactly six positional members and two derived properties, and that the window scan exists once, not twice

**Checkpoint**: All three user stories are complete. Every figure is qualified by how far it can be
trusted and how much history stands behind it.

---

## Phase 6: Polish, Precision, and Compliance Review

**Purpose**: The cross-cutting guarantees, and the compliance check Constitution Governance requires
at feature completion.

- [ ] T057 [P] RED: assert SC-012 and C27 — build a 3,653-day history of varied loads, calculate, and compare each day's `Fitness` and `Fatigue` against the same recurrence carried in `decimal` with the smoothing factors written as 28-digit constants. Assert the largest divergence is below `0.0001`. Measured on this SDK it is about `1.4 × 10⁻¹³`, so the assertion has nine orders of magnitude of headroom — it is there to catch a future change that introduces rounding, not to measure floating point (research R2) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingMetricsTests.cs`
- [ ] T058 [P] Confirm FR-028 by inspection: no `Math.Round`, no cast to `decimal` or `float`, and no formatting of an intermediate value anywhere in `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs`. The single `decimal`-to-`double` conversion of `DailyTrainingLoad.Points` is the only conversion permitted, and it happens once per day
- [ ] T059 Run the constitution checks from [quickstart.md](./quickstart.md): zero package and project references; no match for "strava"; no `public interface`; no mocking or assertion library; no clock read (`DateTime.Now`, `DateTime.Today`, `DateTimeOffset.Now`); and `double` appearing only in the two files this feature added
- [ ] T060 Confirm the assertion style held across `tests/TrainingLoadAnalyzer.Domain.Tests/` (research R7): no test compares two `DailyTrainingMetrics` values whole with `Assert.Equal`, and every assertion on `Fitness`, `Fatigue` or `Form` uses `tolerance: 0.0001` — with the single deliberate exception of T015, which asserts `Form == Fitness - Fatigue` exactly because it is derived
- [ ] T061 Walk the requirement trace in [data-model.md](./data-model.md) and confirm every one of the 34 functional requirements is asserted by at least one test, naming the test for each. Where a requirement has no test, either write one or record why it is not testable at this boundary — do not leave it unaccounted for
- [ ] T062 Confirm nothing on the rejected list was created anywhere in `src/TrainingLoadAnalyzer.Domain/` (research R14, data-model "Types deliberately not created"): no `DailyLoadHistory`, no separate `FitnessCalculator` / `FatigueCalculator` / `FormCalculator`, no `ITrainingMetricsCalculator`, no `Fitness`/`Fatigue`/`Form` wrapper types, no `MetricsReliability` enum, no `TrainingLoadSeries`, no configurable time constants, no public smoothing factors, no caching, no "latest metrics only" overload, no new project, no new package
- [ ] T063 REFACTOR: with everything green, read `src/TrainingLoadAnalyzer.Domain/TrainingMetricsCalculator.cs` once as a whole. The shape it should have converged on is a guard block, one forward loop carrying two accumulators, and one private window-scan method used twice (research R12, R9). If it has more parts than that, simplify while green; if it has fewer because a rule was folded away, check the rule is still asserted
- [ ] T064 Compliance review against [constitution.md](../../.specify/memory/constitution.md), as Governance requires, covering all seven principles with particular attention to I, III and VII. Record the result in the feature's completion notes, including: whether any production code preceded its test; whether the two discriminating checks in T010 and T011 were actually performed rather than assumed; and that the two specification gaps found during planning (research R10, R11) were amended in spec.md before implementation rather than decided in code
- [ ] T065 VERIFY: `dotnet test` all green and `dotnet build -warnaserror` clean. Report the final test count against the 103 baseline

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies
- **Phase 2 (Foundational)**: empty by design
- **Phase 3 (US1)**: needs Phase 1. Creates both production files
- **Phase 4 (US2)**: needs Phase 3 — the calculator must exist before the range can be made to matter
- **Phase 5 (US3)**: needs Phases 3 and 4 — extends the result type and the loop
- **Phase 6 (Polish)**: needs all three stories

### User Story Dependencies

The three stories are **not** fully independent, and the task list says so rather than pretending
otherwise. US1 creates the types; US2 makes the range matter; US3 qualifies the figures. Each phase
still leaves a working, tested increment, which is the property that actually matters here.

### Within Each Story

- RED before GREEN, always. A GREEN task that was written before its RED has broken Principle I
  whatever the tests say afterwards
- A CONFIRM that fails unexpectedly has found a defect — establish whether the test or the design is
  wrong before touching either
- The two discriminating checks (T010, T011) must be performed, not assumed. Both tests pass against
  the correct implementation *and* look like they would pass against a wrong one, which is exactly
  why the temporary break is the only evidence they work

### Parallel Opportunities

Genuine parallelism is limited because most tasks in a story touch the same test file. Marked [P]
tasks are the ones that open work in a *different* file with no incomplete dependency:

- T018 (`MetricsInputTests.cs`) alongside T010–T017 (`TrainingMetricsTests.cs`)
- T028 (`MetricsInputTests.cs`) alongside T021–T027 (`MetricsSeriesTests.cs`)
- T039 (`MetricsBasisTests.cs`) opens the last new file
- T057 and T058 in Phase 6

Every other task either edits a file an earlier task is still in, or depends on the GREEN before it.

---

## Implementation Strategy

### MVP scope

Phases 1 + 3 (T001–T020). That delivers the three figures for any supplied history — MVP items 7, 8
and 9, and the smallest thing an athlete can actually read. It ignores the requested range, which is
why it is an MVP and not the feature.

### Incremental delivery

1. Phase 1 → 001 and 002 green, nothing added
2. Phase 3 → Fitness, Fatigue and Form, correct for any history **(MVP)**
3. Phase 4 → the day-by-day series over a requested range, with malformed histories refused
4. Phase 5 → every figure qualified by its basis and its reliability
5. Phase 6 → precision, compliance, and the constitution review

Stop at any checkpoint; each leaves a working, tested increment.

---

## Notes

- 65 tasks: 2 setup, 18 for US1, 18 for US2, 18 for US3, 9 polish
- **The two discriminating checks are the most important tasks in the list.** T010 (the `1 ÷ N`
  approximation) and T011 (a warm-start seed) both pass against the correct implementation, and both
  would also pass if the test were weak. Temporarily breaking the implementation is the only way to
  know the test would catch a regression. Feature 002's T013 is the precedent
- **CONFIRM tasks are not filler.** They pin behaviour the design already implies, and a CONFIRM that
  unexpectedly fails has found a real defect. Do not "fix" one by adding production code until you
  have established which is wrong, the test or the design
- **Tolerance, not equality.** Every assertion on the three figures uses `tolerance: 0.0001`
  (FR-029), with one deliberate exception: T015 asserts `Form == Fitness - Fatigue` exactly, which is
  only sound because Form is derived. Whole-value `Assert.Equal` on two `DailyTrainingMetrics` is
  forbidden — record-struct equality compares `double` exactly (research R7)
- **Three CONFIRM tasks guard against rules the specification does not ask for**: T037 (an all-zero
  history is valid input, not a refusal), T051 (a zero-point session is not a rest day), and T054
  (`FormBasis` stays derived). Each guards a decision recorded in research.md or the spec
- The window lengths are the feature's second-easiest thing to get subtly wrong after the smoothing
  factor: both windows *include the day itself*, so they cover 42 and 7 days, not 43 and 8. T049 is
  the test that would catch an off-by-one
- Commit after each task or each RED/GREEN pair, keeping test commits ahead of production commits
- Do not create anything on T062's rejected list. Each was considered and rejected with a recorded
  revisit trigger in [research.md](./research.md) and [data-model.md](./data-model.md). If
  implementation makes one feel necessary, that is a signal to amend the plan deliberately — not to
  add it quietly
