---

description: "Task list for feature 002 — Training Load Aggregation"
---

# Tasks: Training Load Aggregation

**Input**: Design documents from `/specs/002-training-load-aggregation/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/domain-api.md](./contracts/domain-api.md)

**Tests**: MANDATORY. The template treats test tasks as optional; Constitution Principle I (Strict
TDD, NON-NEGOTIABLE) overrides that. Every production type below arrives via a failing test.

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

## Constructing activities with known loads

Most tasks below need activities whose training load is a known number. Two recipes, both from
feature 001, are used throughout:

- **Estimated load**: an activity with **no** heart-rate data has load `moving minutes × 2`, marked
  `Estimated` (001 FR-013). So 15, 20, 25 and 30 minutes give 30, 40, 50 and 60 points.
- **Measured load**: an activity with heart-rate data has an Edwards TRIMP load, marked `Measured`
  (001 FR-009). The worked example — maximum 190, samples `(0 min, 150)`, `(10 min, 175)`,
  `(20 min, 160)` — is exactly `80` points. A series of `(0 min, 80)`, `(30 min, 80)` at maximum
  200 is 40% of maximum, below every zone, and is exactly `0` points **marked `Measured`** — which
  is the activity a rest day must stay distinguishable from.

`maximumHeartRate` is `190` unless a task says otherwise. It is unused by estimated loads.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the starting point. No scaffolding — this feature adds no project and no
package, per [research.md](./research.md) R1.

- [X] T001 Establish the baseline: `dotnet test` reports 55 passing, 0 failing — feature 001's suite. Any red here is a pre-existing problem to fix before adding to it
- [X] T002 Confirm `src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj` still contains zero `PackageReference` and zero `ProjectReference` elements. This is the mechanical guard for Principle II, FR-025 and contract C18, and it must stay true for the whole feature. If any task below appears to need a package or a new project, that is design drift — stop and re-read research R1

**Checkpoint**: 001 green, no aggregation code exists yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Intentionally empty.** There is no foundational production code in this feature. Every new type is
created by a user story's GREEN step, because writing a type before a test demands it would violate
Principle I.

User Story 1 is the de facto foundation: it creates `DateRange` and `TrainingLoadAggregator`, which
Stories 2 and 3 both need. This is recorded honestly in Dependencies below rather than papered over
with a claim of full story independence.

---

## Phase 3: User Story 1 - See how much training each day carried (Priority: P1) 🎯 MVP

**Goal**: A collection of recorded sessions and a date range yield one total per calendar day, with
every day in the range present — including days with no training.

**Independent Test**: Aggregate hand-built sessions with known loads over a known range and check
each day's total by hand; include a day with several sessions, a day with none, and days at both
ends of the range.

### The first daily total

- [X] T003 [US1] RED: write the test for scenario 1 — three activities starting on `2026-03-02` with moving times 15, 20 and 25 minutes and no heart-rate data (loads 30, 40, 50), aggregated over the range `2026-03-02` to `2026-03-02`, produce a total of `120m` points for `2026-03-02`. Give every activity the offset `+02:00` and a start time of `08:00` local, so the test does not yet distinguish a UTC-based implementation from an offset-local one — T013 exists to do that. Confirm it fails to compile because `DateRange`, `DailyTrainingLoad` and `TrainingLoadAggregator` do not exist — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T004 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/DateRange.cs` as a `sealed class` with readonly `DateOnly Start` and `DateOnly End`, assigned from constructor parameters. **No validation yet** — no test demands it until T020
- [X] T005 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/DailyTrainingLoad.cs` as `public readonly record struct DailyTrainingLoad(DateOnly Day, decimal Points)`. **No `ActivityCount` and no `Basis`** — nothing demands them until Phase 5, and adding them now would be production code without a failing test
- [X] T006 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/TrainingLoadAggregator.cs` as a `public static class` with `public static IReadOnlyList<DailyTrainingLoad> AggregateDaily(IReadOnlyCollection<TrainingActivity> activities, DateRange range, int maximumHeartRate)`, summing as `decimal` the loads of the activities that fall on the range's day. Minimum only — the gap-free series is T008's job

### The gap-free series

- [X] T007 [US1] RED: write the test for scenario 2 — one 15-minute activity (30 points) on `2026-03-02`, aggregated over `2026-03-01` to `2026-03-05`, produces **exactly five** entries in ascending date order, where `2026-03-01`, `03-03`, `03-04` and `03-05` each report `0m`. Confirm it fails because only days that had training are returned — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T008 [US1] GREEN: add `public IEnumerable<DateOnly> Days` to `src/TrainingLoadAnalyzer.Domain/DateRange.cs`, yielding every day from `Start` to `End` **inclusive** in ascending order. Then rewrite `AggregateDaily` to build the series by walking `range.Days` and looking each day up in a dictionary keyed by day — **not** by grouping the activities, which is what returns only the days that had training (FR-005, FR-010, research R14)

### What the range includes and excludes

- [X] T009 [US1] CONFIRM: add scenario 3 — over the range `2026-03-01` to `2026-03-05`, an activity on `2026-02-28` and one on `2026-03-06` contribute to no daily total, and no entry outside the range is produced (FR-009, SC-006). Expect no new production code — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T010 [US1] CONFIRM: add scenario 4 — an activity on the range's first day and one on its last day are both included; both endpoint days are in full (FR-009) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T011 [US1] CONFIRM: a range whose `Start` equals its `End` produces exactly one entry (SC-001) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T012 [US1] CONFIRM: add scenario 5 — an **empty** activity collection over `2026-03-01` to `2026-03-05` produces five entries each reporting `0m`, not an empty list and not an exception (FR-018, C16, SC-002) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`

### Which day an activity belongs to

- [X] T013 [US1] CONFIRM: assert FR-004 — an activity starting `2026-03-02T00:30:00+02:00`, whose UTC instant is `2026-03-01T22:30:00Z`, is attributed to **`2026-03-02`**, and `2026-03-01` reports `0m` over the range `2026-03-01` to `2026-03-02`. This is expected to pass unchanged, because `DateTimeOffset.Date` is already offset-local. **Before accepting it, prove it discriminates**: temporarily change the day assignment to `StartedAt.UtcDateTime.Date`, confirm this test — and only this test — fails, then revert. Without that check there is no evidence the test would catch a UTC implementation (C11, SC-011) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T014 [US1] CONFIRM: the traveller case from the spec's Assumptions — one activity at `2026-03-02T08:00:00+14:00` and another at `2026-03-02T20:00:00-11:00`, whose UTC instants are 37 hours apart, both count against local day `2026-03-02`. Aggregating over `2026-03-01` to `2026-03-04` puts both loads on `03-02` and leaves `03-01`, `03-03` and `03-04` at `0m` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T015 [US1] CONFIRM: an activity starting at exactly `00:00` local and one starting at exactly `23:59` local on the same day both fall on that day, with nothing spilling into the neighbouring days (spec Edge Cases) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`

### Properties the totals must hold

- [X] T016 [US1] CONFIRM: add scenario 6 — the same activities supplied in a different order produce identical totals, and the entries come back in ascending date order regardless of input order (FR-010, FR-011) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T017 [US1] CONFIRM: add scenario 7 — aggregating the same activities over the same range twice produces equal results, with no dependence on the current date. Assert the two result lists are equal element by element (FR-012, C13, SC-005) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T018 [US1] CONFIRM: assert FR-022 — for a day with several activities, the day's `Points` equals the sum of each contributing activity's own `CalculateTrainingLoad(190).Points`, summed in the test. This pins that aggregation introduces **no** rounding of its own; it is not a re-test of feature 001's arithmetic (C14, SC-004) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T019 [US1] CONFIRM: assert FR-023 — two activities constructed with the **same** `ExternalId` on the same day both contribute, so their loads are summed rather than de-duplicated. Deduplication belongs to import and storage, and this test exists so no later change quietly adds it here (Principle VII, C15) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`

### Refusing a malformed range or a missing collection

These serve every story, not just US1; they land here because `DateRange` and the entry points are
born here.

- [X] T020 [P] [US1] RED: assert FR-019 — constructing a `DateRange` whose `end` is earlier than its `start` throws `ArgumentException` with `ParamName` `"end"`. Confirm it fails because no validation exists — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DateRangeTests.cs`
- [X] T021 [US1] GREEN: add the end-before-start guard to the `DateRange` constructor, throwing before any field is assigned — file: `src/TrainingLoadAnalyzer.Domain/DateRange.cs`
- [X] T022 [US1] RED: assert FR-020 — a `start` of `default(DateOnly)` throws `ArgumentException` with `ParamName` `"start"`, and an `end` of `default(DateOnly)` throws with `ParamName` `"end"`. `default(DateOnly)` is how an unbounded end is expressed, following feature 001's treatment of a missing start time. Confirm both fail — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DateRangeTests.cs`
- [X] T023 [US1] GREEN: add both missing-bound guards, ordered so the missing-bound check runs before the end-before-start check — otherwise a default `end` reports the wrong rule — file: `src/TrainingLoadAnalyzer.Domain/DateRange.cs`
- [X] T024 [US1] CONFIRM: assert feature 001's contract guarantee C3 for the two refusals that share `ParamName` `"end"` — a missing end and an end before the start must be distinguishable by message, so that a test for one cannot pass against the other (FR-021) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DateRangeTests.cs`
- [X] T025 [US1] RED: assert that `AggregateDaily` throws `ArgumentNullException` with `ParamName` `"activities"` for a null collection and `"range"` for a null range. A null collection must be refused rather than silently treated as empty, which would hide a caller's bug (FR-021, research R12). Confirm it fails — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`
- [X] T026 [US1] GREEN: add both null guards to `AggregateDaily` — file: `src/TrainingLoadAnalyzer.Domain/TrainingLoadAggregator.cs`
- [X] T027 [US1] CONFIRM: assert contract C17 by reflection — `typeof(DateRange).GetProperties()` yields no property with a public setter, and the type is `sealed`. Use plain reflection, not an architecture-test package — file: `tests/TrainingLoadAnalyzer.Domain.Tests/DateRangeTests.cs`
- [X] T028 [US1] VERIFY: run `dotnet test` — all green, including feature 001's 55 tests. Then confirm the discipline held: `src/` gained exactly three files, `DailyTrainingLoad` still has only `Day` and `Points`, and no interface, service instance, or custom exception type was created

**Checkpoint**: Daily totals work end to end. Weekly totals and the trust markings do not exist yet.

---

## Phase 4: User Story 2 - See how much training each week carried (Priority: P2)

**Goal**: The same sessions yield one total per ISO-8601 week, Monday to Sunday, with every week the
range touches present and each covering all seven of its days.

**Independent Test**: Aggregate hand-built sessions spanning several ISO weeks and check each week's
total by hand, including sessions either side of a Monday boundary and a week with no sessions.

**Depends on**: Phase 3 (needs `DateRange` and the aggregator to exist).

### ISO week identity

- [ ] T029 [P] [US2] RED: assert that the ISO week of `2026-03-02` has `Year` 2026, `Week` 10, `Monday` `2026-03-02` and `Sunday` `2026-03-08`. Confirm it fails because `IsoWeek` does not exist — file: `tests/TrainingLoadAnalyzer.Domain.Tests/IsoWeekTests.cs`
- [ ] T030 [US2] GREEN: create `src/TrainingLoadAnalyzer.Domain/IsoWeek.cs` as a `public readonly record struct` with `Year`, `Week`, `Monday` and `Sunday`, built by a `public static IsoWeek For(DateOnly day)` factory that delegates to `System.Globalization.ISOWeek.GetYear` and `GetWeekOfYear`, takes `Monday` from `ISOWeek.ToDateTime(year, week, DayOfWeek.Monday)`, and derives `Sunday` as `Monday.AddDays(6)`. `ISOWeek` takes `DateTime`, so convert with `day.ToDateTime(TimeOnly.MinValue)` at this one place. Do **not** hand-roll the week rule (research R5)
- [ ] T031 [US2] CONFIRM: add an xUnit `[Theory]` over the boundary table verified in research R5 — `2026-03-01` → `2026-W09`; `2025-12-29` → `2026-W01`; `2027-01-03` → `2026-W53`; `2027-01-04` → `2027-W01`. These pin that the ISO week-numbering year can differ from the calendar year in both directions (User Story 2 scenario 6). Expect no new production code — file: `tests/TrainingLoadAnalyzer.Domain.Tests/IsoWeekTests.cs`
- [ ] T032 [US2] CONFIRM: 2026 is a 53-week ISO year — the week after `2026-W53` (Monday `2026-12-28`) is `2027-W01` (Monday `2027-01-04`), with no gap and no duplicate. Assert by taking `IsoWeek.For(monday.AddDays(7))`, which is the enumeration rule T038 will rely on — file: `tests/TrainingLoadAnalyzer.Domain.Tests/IsoWeekTests.cs`

### The first weekly total

- [ ] T033 [P] [US2] RED: write the test for scenario 1 — three activities with loads 30, 40 and 50 on `2026-03-02`, `2026-03-04` and `2026-03-06`, all in `2026-W10`, aggregated over `2026-03-02` to `2026-03-08`, produce one weekly entry totalling `120m`. Confirm it fails to compile because `AggregateWeekly` and `WeeklyTrainingLoad` do not exist — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyAggregationTests.cs`
- [ ] T034 [US2] GREEN: create `src/TrainingLoadAnalyzer.Domain/WeeklyTrainingLoad.cs` as `public readonly record struct WeeklyTrainingLoad(IsoWeek Week, decimal Points)`. No `ActivityCount`, no `Basis` — Phase 5 adds them
- [ ] T035 [US2] GREEN: add `AggregateWeekly` to `src/TrainingLoadAnalyzer.Domain/TrainingLoadAggregator.cs` with the same parameters as `AggregateDaily`, grouping activities by `IsoWeek.For(localDay)` and summing as `decimal`. Minimum only — empty weeks are T038's job and the whole-week extension is T040's
- [ ] T036 [US2] CONFIRM: add scenario 2 — an activity on Sunday `2026-03-01` and one on Monday `2026-03-02` fall in **different** weeks, `2026-W09` and `2026-W10`. Add the SC-008 boundary cases in the same test: an activity at `00:00` on the Monday belongs to the week that Monday opens, and one at `23:59` on the preceding Sunday belongs to the week before — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyAggregationTests.cs`

### Weeks with no training

- [ ] T037 [US2] RED: write the test for scenario 3 — over the range `2026-03-02` to `2026-03-29`, which is exactly four ISO weeks (`W10`–`W13`), with activities in `W10`, `W11` and `W13` but **none** in `W12` (`2026-03-16` to `2026-03-22`), four weekly entries are produced in ascending order and the third reports `0m`. Confirm it fails because only weeks containing activities are returned — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyAggregationTests.cs`
- [ ] T038 [US2] GREEN: build the weekly series by **walking Mondays** — start from the Monday of the range's first week and add seven days until the last week's Monday is passed, deriving each `IsoWeek` from its Monday. Do not increment a week number, which would need the 52-versus-53-week rule that T032 exists to warn about (FR-006, research R6) — file: `src/TrainingLoadAnalyzer.Domain/TrainingLoadAggregator.cs`

### Whole weeks, including days outside the requested range

This is the decisive behaviour of the feature and the easiest to implement inconsistently.

- [ ] T039 [US2] RED: write the test for scenario 5 — over the range Wednesday `2026-03-04` to Thursday `2026-03-05`, with a 30-minute activity (60 points) on Monday `2026-03-02`, `AggregateWeekly` produces **one** entry for `2026-W10` (Monday `2026-03-02` to Sunday `2026-03-08`) whose total **includes** those 60 points, while `AggregateDaily` over the same range produces only `2026-03-04` and `2026-03-05`, both `0m`. Assert both halves in one test — the asymmetry is the point. Confirm it fails because the Monday activity is currently outside the range and excluded — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyAggregationTests.cs`
- [ ] T040 [US2] GREEN: in `AggregateWeekly`, compute an extended range from the Monday opening the requested range's first week to the Sunday closing its last, build a daily series over **that** range, and chunk it into consecutive groups of seven, one `WeeklyTrainingLoad` per chunk. This makes FR-017 and SC-012 structural rather than properties to maintain (research R8). `AggregateDaily` must be left untouched and must still return only the requested range — file: `src/TrainingLoadAnalyzer.Domain/TrainingLoadAggregator.cs`
- [ ] T041 [US2] CONFIRM: add scenario 4 — over a range covering whole ISO weeks (`2026-03-02` to `2026-03-29`), each weekly entry's `Points` equals the sum of the `Points` of its seven `AggregateDaily` entries, checked week by week (FR-017, C10, SC-003) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyAggregationTests.cs`
- [ ] T042 [US2] CONFIRM: the other half of C10 — for the mid-week range in T039, the weekly total minus the sum of the in-range daily totals equals **exactly** the load of the extended days outside the range, and nothing else (SC-003) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyAggregationTests.cs`
- [ ] T043 [US2] CONFIRM: assert C9 and SC-012 — for a two-day range, a mid-week range, and a range of several months, **every** weekly entry satisfies `Sunday == Monday.AddDays(6)` and its `Monday.DayOfWeek` is Monday. No weekly entry is ever partial — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyAggregationTests.cs`
- [ ] T044 [US2] CONFIRM: a range that starts and ends inside the same ISO week produces exactly **one** weekly entry, covering that whole week (spec Edge Cases) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyAggregationTests.cs`
- [ ] T045 [US2] CONFIRM: a range crossing the year boundary — `2026-12-28` to `2027-01-10` — produces `2026-W53` then `2027-W01` then `2027-W02`, consecutive with no gap and no duplicate, and an activity on `2027-01-03` counts in `2026-W53` (User Story 2 scenario 6) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyAggregationTests.cs`
- [ ] T046 [US2] VERIFY: run `dotnet test` — all green, including feature 001's 55 tests and every US1 test. Confirm `AggregateDaily`'s behaviour is unchanged by T040: the daily series must still stop at the requested range

**Checkpoint**: Daily and weekly totals both work. Neither says how much it can be trusted.

---

## Phase 5: User Story 3 - Know how much to trust a total (Priority: P3)

**Goal**: Every daily and weekly total reports how many sessions produced it and whether it is
measured, estimated, mixed, or has no basis at all.

**Independent Test**: Aggregate days and weeks built from measured sessions only, estimated only, a
mix, and none at all, and confirm each reports the corresponding basis and count.

**Depends on**: Phases 3 and 4 (extends both result types).

**Note on churn**: T049 and T056 add fields to `DailyTrainingLoad`, and T059 to
`WeeklyTrainingLoad`. Positional construction in existing US1/US2 tests will stop compiling. That is
expected and mechanical — fix the call sites, do not weaken the assertions. T061 tidies up
afterwards.

### The basis of a daily total

- [ ] T047 [P] [US3] RED: write the test for scenario 1 — a day whose activities **all** carry heart-rate data reports its total as measured. Use two copies of the worked example (maximum 190, samples `(0 min, 150)`, `(10 min, 175)`, `(20 min, 160)`, 80 points each) for a day total of `160m`. Confirm it fails to compile because `LoadBasis` does not exist and `DailyTrainingLoad` has no `Basis` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/LoadBasisTests.cs`
- [ ] T048 [US3] GREEN: create `src/TrainingLoadAnalyzer.Domain/LoadBasis.cs` as `public enum LoadBasis { None, Measured, Estimated, Mixed }` — in exactly that order, so `default(LoadBasis)` is `None`, which is the correct basis for the zero-load entries that dominate a sparse series (research R10)
- [ ] T049 [US3] GREEN: add `LoadBasis Basis` to `DailyTrainingLoad` and derive it in `AggregateDaily` from the `Provenance` of the contributing loads. Update the US1 test call sites mechanically — file: `src/TrainingLoadAnalyzer.Domain/DailyTrainingLoad.cs`, `src/TrainingLoadAnalyzer.Domain/TrainingLoadAggregator.cs`
- [ ] T050 [US3] RED: add scenarios 2 and 3 — a day whose activities all lack heart-rate data reports `Estimated`; a day with one activity of each kind reports `Mixed`, and specifically **not** `Measured` and **not** `Estimated`. Confirm they fail — file: `tests/TrainingLoadAnalyzer.Domain.Tests/LoadBasisTests.cs`
- [ ] T051 [US3] GREEN: complete the derivation — all measured → `Measured`, all estimated → `Estimated`, both present → `Mixed` — file: `src/TrainingLoadAnalyzer.Domain/TrainingLoadAggregator.cs`
- [ ] T052 [US3] RED: add scenario 4 — a day with **no** activities reports `None`, not `Measured`. An empty set is vacuously "all measured", so a derivation written as "any estimated ? Mixed : Measured" passes T047 and T050 and fails here. Confirm it fails — file: `tests/TrainingLoadAnalyzer.Domain.Tests/LoadBasisTests.cs`
- [ ] T053 [US3] GREEN: handle the empty case explicitly, before the all-measured case — file: `src/TrainingLoadAnalyzer.Domain/TrainingLoadAggregator.cs`
- [ ] T054 [US3] CONFIRM: assert FR-015 — a day with **one** estimated activity among four measured ones reports `Mixed`. The proportion is not recorded, and a single estimate qualifies the whole total — file: `tests/TrainingLoadAnalyzer.Domain.Tests/LoadBasisTests.cs`

### Counting the sessions behind a total

- [ ] T055 [US3] RED: write the test for scenario 6 and FR-016 — a rest day reports `(0m, count 0, None)`, while a day holding one zero-scoring **measured** activity (maximum 200, samples `(0 min, 80)`, `(30 min, 80)`, entirely below 50% of maximum) reports `(0m, count 1, Measured)`. The two must be distinguishable. Confirm it fails because `DailyTrainingLoad` has no `ActivityCount` — file: `tests/TrainingLoadAnalyzer.Domain.Tests/LoadBasisTests.cs`
- [ ] T056 [US3] GREEN: add `int ActivityCount` to `DailyTrainingLoad` and populate it from the number of contributing activities. Update call sites — file: `src/TrainingLoadAnalyzer.Domain/DailyTrainingLoad.cs`, `src/TrainingLoadAnalyzer.Domain/TrainingLoadAggregator.cs`
- [ ] T057 [US3] CONFIRM: assert contract C12 across a mixed range of at least ten days — `Basis == None` if and only if `ActivityCount == 0`. Not "implies": both directions, so neither a counted day marked `None` nor an empty day marked `Measured` can slip through — file: `tests/TrainingLoadAnalyzer.Domain.Tests/LoadBasisTests.cs`

### Weekly totals carry the same markings

- [ ] T058 [US3] RED: assert that a week reports `Basis` and `ActivityCount` on the same rules — a week of all-measured activities is `Measured` with the right count, and a week with no activities is `(0m, count 0, None)`. Confirm it fails to compile because `WeeklyTrainingLoad` has neither field — file: `tests/TrainingLoadAnalyzer.Domain.Tests/LoadBasisTests.cs`
- [ ] T059 [US3] GREEN: add `ActivityCount` and `Basis` to `WeeklyTrainingLoad` and populate them in `AggregateWeekly`. Because the weekly series is chunks of a daily one (T040), derive each week's count as the sum of its seven days' counts and its basis by combining its seven days' bases — the two views cannot then disagree — file: `src/TrainingLoadAnalyzer.Domain/WeeklyTrainingLoad.cs`, `src/TrainingLoadAnalyzer.Domain/TrainingLoadAggregator.cs`
- [ ] T060 [US3] CONFIRM: add scenario 5 — a week containing one estimated activity and three measured ones reports `Mixed`. Combining seven day-bases where most are `None` must not produce `None` or swallow the estimate; this is the case where a naive fold over the daily bases goes wrong — file: `tests/TrainingLoadAnalyzer.Domain.Tests/LoadBasisTests.cs`
- [ ] T061 [US3] REFACTOR: both result shapes are now final. Where a US1 or US2 test asserts three or four properties of one entry in sequence, replace it with a single whole-value `Assert.Equal` against a constructed `DailyTrainingLoad` or `WeeklyTrainingLoad`, which is what the structural equality in research R11 is for. Leave assertions alone where naming the property is clearer. Tests must stay green throughout — files: `tests/TrainingLoadAnalyzer.Domain.Tests/DailyAggregationTests.cs`, `tests/TrainingLoadAnalyzer.Domain.Tests/WeeklyAggregationTests.cs`
- [ ] T062 [US3] VERIFY: run `dotnet test` — all green, including feature 001's 55 tests. Confirm `LoadProvenance` was **not** modified: feature 001's two-state enum describes one activity and must not have grown a `Mixed` or `None` member (research R10)

**Checkpoint**: All three stories complete. Every total carries its points, its count, and its basis.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T063 [P] Work through the seven hand-checks in [quickstart.md](./quickstart.md) — the gap-free series, the offset-local day, the Monday boundary, the extended edge week, the 53-week year, rest day versus zero day, and one estimate tainting the week — confirming each by hand against the spec rather than against the code
- [ ] T064 [P] Run the constitution checks from [quickstart.md](./quickstart.md): the domain `.csproj` shows no references, `grep -ril "strava" src/ | grep -v obj/` returns nothing, no mocking library appears in any test `.csproj`, `grep -rn "public interface" src/ --include='*.cs'` returns nothing, and `grep -rn "UtcDateTime\|ToUniversalTime\|TimeZoneInfo" src/ --include='*.cs'` returns nothing
- [ ] T065 Run `dotnet build -warnaserror` and resolve any warning
- [ ] T066 Review the feature's git history and confirm each test commit precedes the production commit that makes it pass. Commit order is the evidence that Principle I was followed; coverage percentage is not, and must not be used as a substitute
- [ ] T067 Complete the Principle II semantic review by reading every new type, member, and parameter name in `src/` for provider vocabulary. No tool catches this; it is the half of Principle II only a human reviewer can check (SC-010)
- [ ] T068 Confirm no rule was invented for the year-9999 boundary — a range ending in the final ISO week of year 9999 must still surface the framework's own `ArgumentOutOfRangeException` from `DateOnly.AddDays`, not a domain refusal. The specification states no rule here and research R13 records why none was added; this check exists so one is not introduced quietly (Principle VII)
- [ ] T069 Confirm none of the rejected abstractions appeared: no `ITrainingLoadAggregator`, no `TrainingLoadSeries`, no `WeekRange`, no `partial` flag on `WeeklyTrainingLoad`, no athlete-profile type, no custom exception, no caching. Each was rejected with a recorded revisit trigger in [research.md](./research.md) and [data-model.md](./data-model.md)
- [ ] T070 Run the feature completion review from the constitution's Development Workflow: acceptance criteria satisfied, TDD followed, tests green, no unnecessary domain dependency, no unnecessary abstractions remaining, agreed error scenarios handled, AI-generated code human-reviewed, spec and implementation aligned

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: intentionally empty
- **User Story 1 (Phase 3)**: depends on Phase 1. **Blocks Stories 2 and 3** — both need `DateRange` and `TrainingLoadAggregator`
- **User Story 2 (Phase 4)**: depends on Phase 3
- **User Story 3 (Phase 5)**: depends on Phase 3 for the daily half (T047–T057) and additionally on Phase 4 for the weekly half (T058–T060)
- **Polish (Phase 6)**: depends on all three stories

### A note on story independence

The template's model assumes stories are mutually independent. These are not, and the spec says so:
User Story 2's totals are derived from the same day assignment as User Story 1, and User Story 3
qualifies the numbers both of them produce rather than producing new ones. Story 1 creates the types
the other two extend.

Stories 2 and 3 are **not** independent of each other either, unlike feature 001's: T058–T060 need
`AggregateWeekly` to exist. Two people could split Phase 5 — one on T047–T057, one waiting on Phase
4 — but there is little to gain.

### Within each cycle

RED must fail, and fail for the stated reason, before its GREEN task begins. A RED task that passes
on first run means the test is not testing what it claims — fix the test, do not proceed.

### Parallel Opportunities

Genuine parallelism is limited because most tasks in a story touch the same test file. Marked [P]
tasks are the ones that open work in a *different* file with no incomplete dependency:

- T020 (`DateRangeTests.cs`) alongside T009–T019 (`DailyAggregationTests.cs`)
- T029 (`IsoWeekTests.cs`) and T033 (`WeeklyAggregationTests.cs`) each open a new file
- T047 (`LoadBasisTests.cs`) opens the last new file
- T063 and T064 in Phase 6

Every other task either edits a file an earlier task is still in, or depends on the GREEN before it.

---

## Implementation Strategy

### MVP scope

Phases 1 + 3 (T001–T028). That delivers the gap-free daily series, which is what the
Fitness/Fatigue/Form feature actually consumes — those calculations decay day by day and cannot skip
missing days. Weekly totals are what the athlete reads; the daily series is what the product
computes from.

### Incremental delivery

1. Phase 1 → 001 green, nothing added
2. Phase 3 → daily totals, gap-free **(MVP)**
3. Phase 4 → weekly totals over whole ISO weeks
4. Phase 5 → every total says how far it can be trusted
5. Phase 6 → constitution compliance review

Stop at any checkpoint; each leaves a working, tested increment.

---

## Notes

- 70 tasks: 2 setup, 26 for US1, 18 for US2, 16 for US3, 8 polish
- **CONFIRM tasks are not filler.** They pin behaviour the design already implies, and a CONFIRM
  that unexpectedly fails has found a real defect. Do not "fix" one by adding production code until
  you have established which is wrong, the test or the design
- **T013 is the one CONFIRM that must be actively proven to discriminate.** `DateTimeOffset.Date` is
  offset-local, so the correct implementation is also the natural one — which means the test passes
  whether or not the implementer understood FR-004. Temporarily breaking it to UTC is the only way
  to know the test would catch a regression
- Three CONFIRM tasks exist specifically to stop a rule being added that the specification does not
  call for: T019 (duplicate identifiers both count), T068 (no year-9999 guard), and T062
  (`LoadProvenance` not widened). Each guards a decision recorded in research.md or the spec's
  Assumptions, which is where the reasoning lives
- The asymmetry in FR-013 — weekly totals reach outside the requested range, daily totals never do —
  is asserted in one test, T039, and re-checked in T046. If exactly one test fails during a later
  refactor, expect it to be that one
- Commit after each task or each RED/GREEN pair, keeping test commits ahead of production commits
- Do not create: an interface, a `TrainingLoadSeries`, a `WeekRange`, a `partial` flag, an
  athlete-profile type, a custom exception type, a mocking library, an assertion library, or a new
  project. Each was considered and rejected with a recorded revisit trigger in
  [research.md](./research.md) and [data-model.md](./data-model.md). If implementation makes one
  feel necessary, that is a signal to amend the plan deliberately — not to add it quietly
