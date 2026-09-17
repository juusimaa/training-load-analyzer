---

description: "Task list for feature 004 — Load Trends"
---

# Tasks: Load Trends

**Input**: Design documents from `/specs/004-load-trends/`

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

## Constructing a weekly load history

Every test below builds an `IReadOnlyList<WeeklyTrainingLoad>` by hand. Feature 002's type is
`WeeklyTrainingLoad(IsoWeek Week, decimal Points, int ActivityCount, LoadBasis Basis)`, constructed
directly — this feature never touches `TrainingActivity`, `TrainingLoadAggregator`, or a maximum
heart rate.

The weeks used throughout, all verified against `System.Globalization.ISOWeek`:

| Week | Monday | Sunday |
|------|--------|--------|
| 2026-W09 | 2026-02-23 | 2026-03-01 |
| 2026-W10 | 2026-03-02 | 2026-03-08 |
| 2026-W11 | 2026-03-09 | 2026-03-15 |
| 2026-W12 | 2026-03-16 | 2026-03-22 |
| 2026-W13 | 2026-03-23 | 2026-03-29 |

Build a week with `IsoWeek.For(new DateOnly(2026, 3, 2))`. A helper that turns a list of point
totals into a contiguous run of weeks from a given Monday keeps the tests readable; write it when the
second test needs it, not before.

**Every test needs one more week of history than it has entries**, because the week before the range
must be present (FR-022). The smallest valid request is a two-week history and a range covering the
second week.

## Expected values

Computed in decimal arithmetic and verified against the specification on 2026-09-17; the same table
is in [quickstart.md](./quickstart.md). All 16 agree.

| # | previous | current | absolute | relative | classification | pins |
|---|---------:|--------:|---------:|---------:|----------------|------|
| 1 | 500 | 600 | `+100` | `+0.2` | `SignificantIncrease` | US1.1, US2.1 |
| 2 | 600 | 450 | `−150` | `−0.25` | `SignificantDecrease` | US1.2 |
| 3 | 500 | 500 | `0` | `0` | `Steady` | US1.3 |
| 4 | 500 | 520 | `+20` | `+0.04` | `Steady` | US2.2 |
| 5 | 20 | 26 | `+6` | `+0.3` | `Steady` | US2.3 — **floor discriminator** |
| 6 | 1000 | 1060 | `+60` | `+0.06` | `Steady` | FR-010 — **relative discriminator** |
| 7 | 600 | 400 | `−200` | `−0.333…` | `SignificantDecrease` | US2.4 |
| 8 | 500 | 0 | `−500` | `−1` | `SignificantDecrease` | US2.5 |
| 9 | 0 | 400 | `+400` | *(null)* | `SignificantIncrease` | US3.1 |
| 10 | 0 | 0 | `0` | *(null)* | `Steady` | US3.2 |
| 11 | 0 | 30 | `+30` | *(null)* | `Steady` | FR-014 |
| 12 | 400 | 460 | `+60` | `+0.15` | `SignificantIncrease` | FR-011 — relative exactly at threshold |
| 13 | 400 | 459 | `+59` | `+0.1475` | `Steady` | FR-011 — just under |
| 14 | 200 | 250 | `+50` | `+0.25` | `SignificantIncrease` | FR-011 — absolute exactly at threshold |
| 15 | 200 | 249 | `+49` | `+0.245` | `Steady` | FR-011 — just under |
| 16 | 505.7 | 610.4 | `104.7` | — | — | FR-033 — exact, not `104.69999999999999` |

**Rows 5 and 6 are the pair that matters most.** Row 5 fails against an implementation with only a
relative threshold; row 6 fails against one with only an absolute floor. Neither alone proves FR-010;
together they do.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the starting point. No scaffolding — this feature adds no project and no
package, per [research.md](./research.md) R1.

- [X] T001 Establish the baseline: `dotnet test` reports 143 passing, 0 failing — features 001, 002 and 003. Any red here is a pre-existing problem to fix before adding to it
- [X] T002 Confirm `src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj` still contains zero `PackageReference` and zero `ProjectReference` elements, and record which files currently contain `double` — `grep -rln "double" src/TrainingLoadAnalyzer.Domain/ --include='*.cs'` should list exactly `DailyTrainingMetrics.cs` and `TrainingMetricsCalculator.cs`, feature 003's two files. That list must not grow: this feature is `decimal` throughout (research R2, C32). If any task below appears to need a package or a new project, that is design drift — stop and re-read research R1

**Checkpoint**: 001, 002 and 003 green, no trend code exists yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Intentionally empty.** There is no foundational production code in this feature. Every new member
is created by a user story's GREEN step, because writing one before a test demands it would violate
Principle I.

User Story 1 is the de facto foundation: it creates `WeeklyLoadTrend` and
`TrainingLoadTrendCalculator`, which Stories 2 and 3 both extend. This is recorded honestly in
Dependencies below rather than papered over with a claim of full story independence.

---

## Phase 3: User Story 1 - See how this week compares to last week (Priority: P1) 🎯 MVP

**Goal**: A weekly load history yields, for each week in the requested range, how far that week moved
against the week before it — in points and as a proportion.

**Independent Test**: Feed a hand-built weekly series with known totals through the calculation and
check each entry's two changes against arithmetic done by hand; include a rise, a fall, no change,
and a week following an idle one.

### The absolute change

- [X] T003 [US1] RED: write the test — a history of 2026-W10 at `500m` and 2026-W11 at `600m`, over the range `2026-03-09` to `2026-03-15` (all of W11), produces one entry whose `AbsoluteChange` is `100m`. Confirm it fails to compile because `WeeklyLoadTrend` and `TrainingLoadTrendCalculator` do not exist — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`
- [X] T004 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs` as `public readonly record struct WeeklyLoadTrend(IsoWeek Week, decimal Points, decimal PreviousPoints)` with `public decimal AbsoluteChange => Points - PreviousPoints;` as a **computed property**. **No `RelativeChange`, no `Classification`, no `IsComplete`, no `Basis`** — nothing demands them yet, and adding them now would be production code without a failing test. `AbsoluteChange` is derived rather than stored because that is the *simpler* implementation, not merely the preferred one (research R6)
- [X] T005 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs` as a `public static class` with `public static IReadOnlyList<WeeklyLoadTrend> Calculate(IReadOnlyList<WeeklyTrainingLoad> history, DateRange range)`. Walk `history` from index 1, emitting one entry per week comparing `history[i]` against `history[i - 1]`. `range` is not consulted yet; T015 is what forces it. Everything is `decimal` — if a `double` appears here, that is a defect (research R2)

### The relative change

- [X] T006 [US1] RED: the same history produces a `RelativeChange` of `0.2m`. Confirm it fails to compile because there is no `RelativeChange` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`
- [X] T007 [US1] GREEN: add `public decimal? RelativeChange => AbsoluteChange / PreviousPoints;` to `src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs` as a computed property. Deliberately **without** a zero guard — T008 is what forces it, and the way it fails is the point
- [X] T008 [US1] RED: assert FR-004 — a history of 2026-W10 at `0m` and 2026-W11 at `400m` produces an entry whose `RelativeChange` is `null`. Confirm it fails with a **`DivideByZeroException`**, not with a wrong value. That distinction is research R2's second argument for `decimal` made visible: had this been `double`, the test would have failed with `Infinity` — a value FR-004 forbids and which no exception would have announced — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`
- [X] T009 [US1] GREEN: add the guard — `PreviousPoints == 0 ? null : AbsoluteChange / PreviousPoints` — in `src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs`. The absence must be `null`, never `0m` and never a sentinel (FR-004, C33)
- [X] T010 [US1] CONFIRM: rows 2 and 3 of the expected-values table — 600 → 450 gives `−150m` and `−0.25m`; 500 → 500 gives `0m` and `0m`. Assert in the same test that a zero change reports `RelativeChange` of `0m` and **not** `null`: only an absent *divisor* produces null, never an absent *change* — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`
- [X] T011 [US1] CONFIRM: assert FR-033 and C32 with row 16 — a history of `505.7m` followed by `610.4m` produces an `AbsoluteChange` of exactly `104.7m`, asserted with plain `Assert.Equal` and **no tolerance**. This is the test that catches a `double` slipping into the calculation, since the same subtraction in `double` yields `104.69999999999999` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`
- [X] T012 [P] [US1] CONFIRM — **discriminating check**: assert by reflection that `WeeklyLoadTrend`'s primary constructor has **no** parameter named `AbsoluteChange` or `RelativeChange`, and that neither property has a setter. Every test above would pass just as well against stored fields assigned by the calculator; only this one establishes that C32 and C33 hold by construction rather than by the calculator remembering to keep them in step (research R6) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`

### The requested range decides which weeks are reported

- [X] T013 [US1] RED: assert FR-023 and FR-028 — a four-week history (W09–W12 at `400m`, `500m`, `600m`, `700m`) over the range `2026-03-16` to `2026-03-22` (all of W12) produces **exactly one** entry, for W12, comparing against W11. Confirm it fails because the calculator returns three entries, ignoring the range — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`
- [X] T014 [US1] GREEN: bound emission to the weeks touching the range in `src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs`. Compute `IsoWeek.For(range.Start)` and `IsoWeek.For(range.End)`, and compare weeks by their `Monday`, **never** by `(Year, Week)` — 2026 is a 53-week ISO year and the year boundary is where a hand-rolled comparison goes wrong (research R9, `IsoWeek`'s own remarks)
- [X] T015 [US1] CONFIRM: scenario US1.4 — a five-week history over a range spanning four whole weeks produces exactly four entries, in ascending week order, each comparing itself to the week immediately before it. Assert the ordering explicitly rather than assuming it — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`
- [X] T016 [US1] CONFIRM: assert C31's edge — a range lying entirely inside one week (`2026-03-04` to `2026-03-06`, both in W10) produces exactly one entry, for W10. A range narrower than a week still touches a week — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`

### Refusals

- [X] T017 [P] [US1] RED: assert scenario US1.5 and FR-022 — a history beginning at the range's own first week, with no week before it, is refused with an `ArgumentException` whose `ParamName` is `"history"` and whose message names the week that should have preceded it. Confirm it fails because no refusal exists yet — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendInputTests.cs`
- [X] T018 [US1] GREEN: add refusals 1–3 from [data-model.md](./data-model.md) to `src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs` — `ArgumentNullException.ThrowIfNull` for both parameters; an empty history; and a history that does not reach the week before the range's first week. **The empty check must come first**, because the reach-back check indexes `history[0]` (research R13). Each message must name its own rule and the offending week
- [X] T019 [US1] RED: assert scenario US1.6 and FR-022b — a history of W09–W10 over a range spanning W10–W13 is refused, with a message identifying the shortfall. Confirm it fails because the calculator silently returns one entry. **This is the rule that came from an amendment** (research R11): the tempting implementation zero-fills the missing weeks, and the very next thing this feature would do is report a significant decrease for a week with no data behind it — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendInputTests.cs`
- [X] T020 [US1] GREEN: add refusal 4 — a history ending before the range's last week — in `src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs`, ordered after the reach-back check so the more specific diagnosis wins
- [X] T021 [US1] RED: assert FR-024 — a history skipping W11 (W09, W10, W12) is refused, with a message naming the index and both weeks. Confirm it fails because nothing checks continuity — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendInputTests.cs`
- [X] T022 [US1] GREEN: add refusal 5 in `src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs` — one check over consecutive pairs, `history[i].Week.Monday == history[i - 1].Week.Monday.AddDays(7)`, which catches a gap, a repeat and an inversion alike (research R10). Refuse rather than repair: filling a gap with a zero week would compare a week against the wrong predecessor
- [X] T023 [US1] CONFIRM: assert C41 — all four `ArgumentException` refusals (empty, reach-back, shortfall, continuity) carry `ParamName` `"history"`, and their four messages are mutually distinct. A test asserting only on `ParamName` would pass against the wrong refusal, which is why the messages are the assertion. Add the repeated-week and out-of-order cases here, both expecting the continuity message — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendInputTests.cs`
- [X] T024 [US1] CONFIRM: assert FR-026 and C42 — an inverted range is refused by `DateRange`'s own constructor before `Calculate` is reached, so this feature adds no check of its own; and no refusal returns a partial series alongside the exception — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendInputTests.cs`
- [X] T025 [US1] VERIFY: run `dotnet test` — all green, including every 001, 002 and 003 test. Confirm `WeeklyLoadTrend` has exactly three positional members and two derived properties, and that no `double` appears in either new file

**Checkpoint**: User Story 1 is complete. Every week in a requested range reports how far it moved,
in points and as a proportion, and malformed histories are refused rather than repaired.

---

## Phase 4: User Story 2 - Have the changes that matter called out (Priority: P2)

**Goal**: Each week's change carries a label, so a genuine ramp-up or collapse stands out and
ordinary variation does not.

**Independent Test**: Construct `WeeklyLoadTrend` values directly — no history, no calculator, no
range — either side of both thresholds, and read `Classification`. Because the classification is a
derived property (research R6), this story is testable entirely through the type.

**Depends on**: Phase 3 (the type must exist).

**Note on churn**: tests here construct `WeeklyLoadTrend` positionally, and Phase 5 adds two more
positional members. Write **T026's helper first** so that churn lands in one line rather than thirty.

- [X] T026 [US2] Write a private test helper in `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs` — `private static WeeklyLoadTrend Trend(decimal previous, decimal current)` — building a `WeeklyLoadTrend` for 2026-W10 from the two totals. Every test in this phase goes through it, so Phase 5's two new members change one line here instead of every test. This is test infrastructure, not production code, and needs no RED of its own

### Significant increases

- [X] T027 [US2] RED: assert scenario US2.1 and row 1 — `Trend(500m, 600m).Classification` is `TrendClassification.SignificantIncrease`. Confirm it fails to compile because neither `TrendClassification` nor `Classification` exists — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`
- [X] T028 [US2] GREEN: create `src/TrainingLoadAnalyzer.Domain/TrendClassification.cs` as `public enum TrendClassification { Steady, SignificantIncrease }` and add `public TrendClassification Classification` to `src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs` as a **computed property** returning `SignificantIncrease` when `AbsoluteChange > 0` and `Steady` otherwise. **No thresholds yet** — T029 and T031 are what force them. It must not become a constructor parameter and the calculator must not assign it (FR-007, research R6)
- [X] T029 [US2] RED: assert scenario US2.2 and row 4 — `Trend(500m, 520m)` is `Steady`: +20 points is under the floor. Confirm it fails, returning `SignificantIncrease` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`
- [X] T030 [US2] GREEN: add the absolute floor to `src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs` as `private const decimal AbsoluteThreshold = 50m;`, returning `Steady` when `Math.Abs(AbsoluteChange) < AbsoluteThreshold`. **Private** — a public constant invites a test that asserts the code agrees with itself (research R7). Use `<`, not `<=`, so a change sitting exactly on the threshold passes through (FR-011)
- [X] T031 [US2] RED — **relative discriminator**: assert FR-010 with row 6 — `Trend(1000m, 1060m)` is `Steady`: +60 points clears the floor but +6% does not clear the relative threshold. Confirm it fails, returning `SignificantIncrease`. Without this test an implementation with only an absolute floor passes everything written so far — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`
- [X] T032 [US2] GREEN: add `private const decimal RelativeThreshold = 0.15m;` to `src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs` and return `Steady` when `RelativeChange` has a value whose magnitude is below it. Keep the floor test **before** it — that ordering is what makes FR-013 fall out in T037 rather than needing a branch of its own (data-model, "The classification rule")
- [X] T033 [US2] CONFIRM — **floor discriminator**: assert scenario US2.3 with row 5 — `Trend(20m, 26m)` is `Steady`: +30% clears the relative threshold but +6 points does not clear the floor. Together with T031 this is the pair that proves FR-010's "both MUST be cleared"; neither test alone does — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`

### Significant decreases

- [X] T034 [US2] RED: assert scenario US2.4 and row 7 — `Trend(600m, 400m)` is `SignificantDecrease`. Confirm it fails to compile because the enum has no such member — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`
- [X] T035 [US2] GREEN: add `SignificantDecrease` to `src/TrainingLoadAnalyzer.Domain/TrendClassification.cs` and branch on the sign of `AbsoluteChange` in `WeeklyLoadTrend.cs`. Compare **magnitudes** against both thresholds, so a rise and a fall of the same size are treated alike
- [X] T036 [US2] CONFIRM: scenario US2.5 and row 8 — `Trend(500m, 0m)` is `SignificantDecrease` with a `RelativeChange` of exactly `−1m`. A week of total rest after a real week is a significant drop, and the proportion is defined — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`

### When there is no proportion to test

- [X] T037 [US2] RED: assert scenario US3.1, FR-013 and FR-014 with row 9 — `Trend(0m, 400m)` is `SignificantIncrease`, with `RelativeChange` null. Confirm the failure mode before fixing it: an implementation that tests the relative threshold *before* the floor returns `Steady` here, silently refusing to flag every return from injury or off-season. If T032 was written in the order it specifies, this test passes as written — record which it was, because that is the evidence the ordering was deliberate — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`
- [X] T038 [US2] GREEN (no change needed - T037 passed as written, see note below): only if T037 failed — reorder so the absolute floor is tested before the relative one in `src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs`. If T037 passed, make no change: a GREEN task with nothing to do is the correct outcome, not a licence to refactor
- [X] T039 [US2] CONFIRM: rows 10 and 11 — `Trend(0m, 30m)` is `Steady` (FR-014: a gentle return is not a significant increase) and `Trend(0m, 0m)` is `Steady` with a null `RelativeChange`. Assert in the same test that neither reports `Indeterminate` — that member does not exist yet, and Phase 5 must not capture these cases — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`

### Exactly on the threshold

- [X] T040 [P] [US2] CONFIRM: assert FR-011 and C35 with rows 12–15, as four cases in one test — `Trend(400m, 460m)` and `Trend(200m, 250m)` are both `SignificantIncrease`; `Trend(400m, 459m)` and `Trend(200m, 249m)` are both `Steady`. The two thresholds are pinned **separately** because FR-011's own illustration — exactly 0.15 *and* exactly 50 — needs a previous week of `50 ÷ 0.15 = 333.333…` and is not exactly representable. The requirement is sound; only its example is unreachable — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`
- [X] T041 [US2] CONFIRM — **discriminating check**: extend T012's reflection assertion — `WeeklyLoadTrend`'s primary constructor has no parameter named `Classification`, and the property has no setter. A caller cannot construct an entry whose label contradicts its numbers, which is what makes C34 structural (research R6) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`
- [X] T042 [US2] CONFIRM: assert FR-012 — the thresholds are applied to unrounded values. `Trend(200m, 249.9m)` is `Steady` and `Trend(200m, 250.0000001m)` is `SignificantIncrease`; neither is rounded to the nearest point first — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendClassificationTests.cs`
- [X] T043 [US2] VERIFY: run `dotnet test` — all green. Confirm both thresholds are `private const decimal` declared once, that no test reads them, and that `Classification` is still a computed property

**Checkpoint**: User Stories 1 and 2 are complete. Every week carries both its change and a label,
and ordinary variation is labelled steady.

---

## Phase 5: User Story 3 - Know when a trend is not worth trusting (Priority: P3)

**Goal**: Each comparison states whether the week was fully inside the requested range and how far
the load behind it can be trusted.

**Independent Test**: Calculate over ranges that cut a week short at either end and confirm the
classification is withheld while the changes are still reported; separately, calculate over histories
of differing bases and confirm each comparison reports the combined basis.

**Depends on**: Phases 3 and 4 (extends the result type, the rule, and the loop).

**Note on churn**: T045 and T052 add positional members to `WeeklyLoadTrend`. T026's helper absorbs
that for Phase 4's tests; Phase 3's tests construct the type only in T012's reflection assertion,
which T054 extends rather than weakens.

### Whether the week was fully inside the range

- [X] T044 [US3] RED: assert FR-015 and FR-016 — a history of W09–W10 over the range `2026-03-02` to `2026-03-04` (Monday to Wednesday of W10) produces one entry whose `IsComplete` is **false**, while the same history over `2026-03-02` to `2026-03-08` produces `IsComplete` **true**. Confirm it fails to compile because there is no `IsComplete` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T045 [US3] GREEN: add `bool IsComplete` to `WeeklyLoadTrend`'s positional parameters, computed in `TrainingLoadTrendCalculator` as `week.Monday >= range.Start && week.Sunday <= range.End`. Derived from the **range alone** — never from the totals, never from the clock (FR-016, C38, research R8) — files: `src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs`, `src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs`. Update T026's helper to pass `true`
- [X] T046 [US3] CONFIRM: a range cut at the **start** is partial too — `2026-03-04` to `2026-03-08` (Wednesday to Sunday of W10) gives `IsComplete` false. Partiality is not only an end-of-range phenomenon, and an implementation checking only `range.End` passes T044 — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T047 [US3] RED: assert scenario US3.3 and FR-017 — a history of W09 at `500m` and W10 at `200m` over `2026-03-02` to `2026-03-04` reports `Classification` of `TrendClassification.Indeterminate`, not `SignificantDecrease`. Confirm it fails to compile because the enum has no such member — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T048 [US3] GREEN: add `Indeterminate` to `src/TrainingLoadAnalyzer.Domain/TrendClassification.cs` **as the first member**, so it is `default` and an uninitialised classification reads as "no judgement available" — the same reasoning that put `None` first in feature 002's `LoadBasis`. Nothing persists this enum, so reordering is safe. In `WeeklyLoadTrend.cs`, return it when `IsComplete` is false, **before any threshold is consulted** (data-model, step 1)
- [X] T049 [US3] CONFIRM: assert FR-018 and C37 — that same partial entry still reports an `AbsoluteChange` of `−300m` and a `RelativeChange` of `−0.6m`. The changes are produced and qualified, never withheld or blanked, on the same terms as a warm-up day's figures in feature 003 (C23) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T050 [US3] CONFIRM: assert FR-019 and C39 — the partial week is compared against the **whole** preceding week. With W09 at `500m` and a three-day view of W10 at `200m`, `PreviousPoints` is `500m` and `AbsoluteChange` is `−300m`; neither week is scaled, pro-rated, or extrapolated to make the comparison even — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T051 [US3] CONFIRM: assert C38 — `IsComplete` depends only on the week and the range. Two histories with wildly different totals over the same range give the same `IsComplete` on every entry, and a range running Monday to Sunday across four weeks gives `IsComplete` true on all four — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`

### How far the comparison can be trusted

- [X] T052 [US3] RED: assert FR-020 — a history of two measured weeks reports a `Basis` of `LoadBasis.Measured`. Confirm it fails to compile because there is no `Basis` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T053 [US3] GREEN: add `LoadBasis Basis` to `WeeklyLoadTrend`'s positional parameters and compute it in `TrainingLoadTrendCalculator` from the two compared weeks, via a **private** method local to that file. This is the third copy of feature 002's combination rule in the solution, and keeping it local rather than extracting it is a decision the developer took on 2026-09-17 — do not extract it here (research R12). Update T026's helper to pass `LoadBasis.Measured` — files: `src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs`, `src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs`
- [X] T054 [US3] RED: assert scenario US3.4 — a measured week followed by an estimated week reports `Mixed`. Confirm it fails, returning `Measured` or `Estimated` depending on which week the GREEN happened to read — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T055 [US3] RED: assert scenario US3.5 and FR-021 — a week carrying **no training** (`LoadBasis.None`) followed by a measured week reports `Measured`, **not** `Mixed` and not `None`. A basis of `None` contributes nothing to the combination, because a week with no training has nothing that could have been measured or estimated. Confirm the failure mode: an implementation treating `None` as a fourth participating state reports `Mixed` and quietly downgrades every comparison following a rest week — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T056 [US3] GREEN: complete the combination in `src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs` so it matches data-model's 4×4 table exactly — accumulate "any measured" and "any estimated" across the two weeks and map the pair, with `None` contributing to neither. Feature 002's rule, reused unchanged
- [X] T057 [US3] CONFIRM: assert C40 in full — walk all sixteen combinations of the two weeks' bases and check each against data-model's table. Assert **symmetry** in the same test: swapping the two weeks never changes the result. A table this small is cheaper to assert whole than to sample — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T058 [US3] CONFIRM: assert FR-021's `None` case — two consecutive untrained weeks report a `Basis` of `None`, distinguishable from a comparison of two zero-point measured weeks, which reports `Measured`. This is feature 002's C12 distinction carried through: a rest week and a week of real sessions totalling zero are not the same thing — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T059 [US3] CONFIRM: assert C44 and SC-007 — a history in which every week carries zero points returns a full series of `Steady` entries with null relative changes and a `Basis` of `None`, never an empty result and never an exception; and every entry in a varied series carries an `IsComplete` and a defined `Basis`, with no figure reachable without them — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrendReliabilityTests.cs`
- [X] T060 [US3] VERIFY: run `dotnet test` — all green, including every US1 and US2 test. Confirm `WeeklyLoadTrend` now has exactly five positional members and three derived properties, and that the basis combination exists once, in one private method

**Checkpoint**: All three user stories are complete. Every comparison states how far it can be
trusted and whether the week behind it was whole.

---

## Phase 6: Polish, Precision, and Compliance Review

**Purpose**: The cross-cutting guarantees, and the compliance check Constitution Governance requires
at feature completion.

- [X] T061 [P] CONFIRM: assert C43 and SC-009 — `Calculate` is pure. The same history and range yield equal results on two calls; a series computed from a history built in a different order of construction is identical; and the result does not change when the system date does. Assert whole-value equality across the series, which is sound here because every member compares exactly (contract, "Notes on the surface") — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`
- [X] T062 [P] CONFIRM: assert C31 across an ISO year boundary — a history spanning 2026-W52, 2026-W53 and 2027-W01 produces one entry per week in ascending order, with W53 comparing against W52 and 2027-W01 against W53. 2026 is a 53-week ISO year, which is exactly where comparing weeks by `(Year, Week)` instead of by `Monday` goes wrong (research R9) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyLoadTrendTests.cs`
- [X] T063 Confirm FR-032 and research R2 by inspection: no `Math.Round`, no cast to `double` or `float`, and no formatting of an intermediate value anywhere in `src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs` or `src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs`. Re-run T002's grep: the list of files containing `double` must still be feature 003's two, unchanged
- [X] T064 Run the constitution checks from [quickstart.md](./quickstart.md): zero package and project references; no match for "strava"; no `public interface`; no mocking or assertion library; no clock read (`DateTime.Now`, `DateTime.Today`, `DateTimeOffset.Now`); and exactly one `public static` method on `TrainingLoadTrendCalculator` — FR-029 forbids a second entry point, and a helpfully-added `CalculateSignificant` overload is the most likely way this feature grows one
- [X] T065 Confirm the assertion style held across the four new test files: whole-value `Assert.Equal` on a `WeeklyLoadTrend` is the **preferred** form here, unlike feature 003, because every member is `decimal`, `bool` or an enum and compares exactly. No assertion anywhere uses a tolerance — if one does, a `double` has crept in
- [X] T066 Confirm no test reads `AbsoluteThreshold` or `RelativeThreshold`, directly or by reflection. Both are private precisely so that a test asserting the code agrees with itself cannot be written (research R7); a test that reaches for them has lost its independent value even if it passes
- [X] T067 Walk the requirement trace in [data-model.md](./data-model.md) and confirm every one of the **35** functional requirements is asserted by at least one test, naming the test for each. Where a requirement has no test, either write one or record why it is not testable at this boundary — do not leave it unaccounted for. FR-025 is the one to look at hardest: it constrains the *caller*, and is enforced here only indirectly through T021's continuity refusal
- [X] T068 Confirm nothing on the rejected list was created anywhere in `src/TrainingLoadAnalyzer.Domain/` (research R14, data-model "Types deliberately not created"): no `WeeklyLoadHistory`, no `LoadSpikeDetector` or `WeeklyLoadAnalyzer`, no `TrendThresholds` or options record, no `ITrendCalculator`, no shared `LoadBases.Combine` helper, no `RelativeChange` value type, no configurable thresholds, no public thresholds, no separate list of significant weeks, no second entry point, no new project, no new package
- [X] T069 REFACTOR: with everything green, read `src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs` once as a whole. The shape it should have converged on is a guard block, a short loop emitting one entry per week in the range, and one private basis-combination method (research R4, R12). `WeeklyLoadTrend.cs` should be five positional members and three computed properties with two private constants. If either has more parts than that, simplify while green; if either has fewer because a rule was folded away, check the rule is still asserted
- [X] T070 Compliance review against [constitution.md](../../.specify/memory/constitution.md), as Governance requires, covering all seven principles with particular attention to I, III and VII. Record the result in the feature's completion notes, including: whether any production code preceded its test; whether T031 and T033 — the threshold discriminator pair — were both actually written, since either alone leaves half of FR-010 unproven; whether T037's outcome was recorded honestly as pass or fail; and that the specification gap found during planning (research R11) was amended into spec.md as FR-022b before implementation rather than decided in code. Note explicitly that R12's basis duplication was a declined extraction, not an oversight
- [X] T071 VERIFY: `dotnet test` all green and `dotnet build -warnaserror` clean. Report the final test count against the 143 baseline

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies
- **Phase 2 (Foundational)**: empty by design
- **Phase 3 (US1)**: needs Phase 1. Creates both production files
- **Phase 4 (US2)**: needs Phase 3 — the type must exist before it can carry a label
- **Phase 5 (US3)**: needs Phases 3 and 4 — extends the result type, the rule and the loop
- **Phase 6 (Polish)**: needs all three stories

### User Story Dependencies

The three stories are **not** fully independent, and the task list says so rather than pretending
otherwise. US1 creates the types; US2 adds the judgement; US3 qualifies it. Each phase still leaves a
working, tested increment, which is the property that actually matters here.

US2 is the most genuinely independent of the three: because `Classification` is a derived property,
every test in Phase 4 runs against `WeeklyLoadTrend` alone, with no history, no range and no
calculator. That is a consequence of the design decision in research R6, not a coincidence.

### Within Each Story

- RED before GREEN, always. A GREEN task that was written before its RED has broken Principle I
  whatever the tests say afterwards
- A CONFIRM that fails unexpectedly has found a defect — establish whether the test or the design is
  wrong before touching either
- T038 may legitimately have nothing to do. Record that it did not, rather than inventing work for it

### Parallel Opportunities

Genuine parallelism is limited because most tasks in a story touch the same test file. Marked [P]
tasks are the ones that open work in a *different* file with no incomplete dependency:

- T012 (reflection, independent of the calculator) alongside T013–T016
- T017 (`TrendInputTests.cs`) alongside T013–T016 (`WeeklyLoadTrendTests.cs`)
- T040 (`TrendClassificationTests.cs`) is self-contained once T035 lands
- T061 and T062 in Phase 6

Every other task either edits a file an earlier task is still in, or depends on the GREEN before it.

---

## Implementation Strategy

### MVP scope

Phases 1 + 3 (T001–T025). That delivers both changes for every week in a requested range, with
malformed histories refused — the first half of MVP item 11, and the smallest thing an athlete can
actually read. It carries no judgement about what the changes mean, which is why it is an MVP and not
the feature.

### Incremental delivery

1. Phase 1 → 001, 002 and 003 green, nothing added
2. Phase 3 → the two changes, for any history and range **(MVP)**
3. Phase 4 → the changes that matter called out, ordinary variation labelled steady
4. Phase 5 → every comparison qualified by completeness and basis
5. Phase 6 → precision, compliance, and the constitution review

Stop at any checkpoint; each leaves a working, tested increment.

---

## Notes

- 71 tasks: 2 setup, 23 for US1, 18 for US2, 17 for US3, 11 polish
- **The threshold discriminator pair is the most important thing in this list.** T031 (row 6: +60
  points but only +6%) and T033 (row 5: +30% but only +6 points) each fail against an implementation
  that applies only one of the two thresholds. Every other classification test passes against both
  wrong implementations. Neither task alone proves FR-010 — only the pair does. Feature 003's T010
  and T011 are the precedent
- **T008's failure mode is the evidence, not the failure itself.** It must fail with
  `DivideByZeroException`, not with a wrong value. That is research R2's second argument for
  `decimal` made observable: in `double` the same test would have failed with `Infinity`, a value
  FR-004 forbids and which arrives silently
- **T012 and T041 are what make three requirements structural.** Every other test in this feature
  would pass just as well against stored fields that the calculator kept in step by hand. Only the
  reflection assertions establish that `AbsoluteChange`, `RelativeChange` and `Classification` cannot
  drift from what they are derived from (research R6, C32–C34)
- **Exactness, not tolerance.** This feature returns to `decimal` after feature 003's departure to
  `double`, so every assertion is exact and whole-value `Assert.Equal` on a `WeeklyLoadTrend` is the
  preferred form. A tolerance appearing anywhere in these four test files means a `double` crept in
  (research R2, C32)
- **CONFIRM tasks are not filler.** They pin behaviour the design already implies, and a CONFIRM that
  unexpectedly fails has found a real defect. Do not "fix" one by adding production code until you
  have established which is wrong, the test or the design
- **Three CONFIRM tasks guard against rules the specification does not ask for**: T024 (the range
  refusal belongs to `DateRange`, not to this feature), T050 (a partial week is never scaled up), and
  T058 (an untrained week is not a zero-point session). Each guards a decision recorded in research.md
  or the spec
- The ordering inside the classification rule is the feature's easiest thing to get subtly wrong. The
  floor is tested **before** the relative threshold, and completeness **before** both. T037 catches
  the first mistake and T047 the second; a rule written in any other order passes most of this list
- Weeks are compared by `Monday`, never by `(Year, Week)`. T062 is the test that would catch it, and
  2026 being a 53-week ISO year is why it is worth a task of its own
- Commit after each task or each RED/GREEN pair, keeping test commits ahead of production commits
- Do not create anything on T068's rejected list. Each was considered and rejected with a recorded
  revisit trigger in [research.md](./research.md) and [data-model.md](./data-model.md). If
  implementation makes one feel necessary, that is a signal to amend the plan deliberately — not to
  add it quietly
