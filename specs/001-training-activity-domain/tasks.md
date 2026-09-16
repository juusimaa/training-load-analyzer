---

description: "Task list for feature 001 — Training Activity Domain"
---

# Tasks: Training Activity Domain

**Input**: Design documents from `/specs/001-training-activity-domain/`

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

Two projects at repository root, per [plan.md](./plan.md):
`src/TrainingLoadAnalyzer.Domain/` and `tests/TrainingLoadAnalyzer.Domain.Tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the solution and both projects. No production type is created here.

- [X] T001 Create solution and both projects by running the scaffolding block in [quickstart.md](./quickstart.md), producing `TrainingLoadAnalyzer.sln`, `src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj` (net10.0), and `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingLoadAnalyzer.Domain.Tests.csproj` (net10.0, xunit.v3)
- [X] T002 Add the project reference from the test project to the domain project, and delete the generated `src/TrainingLoadAnalyzer.Domain/Class1.cs`
- [X] T003 Confirm `src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj` contains zero `PackageReference` and zero `ProjectReference` elements — this is the mechanical guard for Principle II and FR-025, and it must stay true for the whole feature
- [X] T004 Establish the baseline: `dotnet build` succeeds and `dotnet test` reports zero tests. Zero is the correct starting point; the first real test is T005

**Checkpoint**: Solution builds, test harness runs, no domain code exists yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Intentionally empty.** There is no foundational production code in this feature. Every domain
type is created by a user story's GREEN step, because writing a type before a test demands it would
violate Principle I. The scaffolding in Phase 1 is the only shared prerequisite.

User Story 1 is the de facto foundation — `TrainingActivity` does not exist until T007, and Stories
2 and 3 both need it. This is recorded honestly in Dependencies below rather than papered over with
a claim of full story independence.

---

## Phase 3: User Story 1 - Record a completed training session (Priority: P1) 🎯 MVP

**Goal**: A completed run or ride can be recorded as an immutable, self-contained record and read
back with every detail intact, including the athlete's local start time and the heart-rate series.

**Independent Test**: Construct activities from known details and assert every property round-trips;
construct two activities with different external identifiers and assert they are distinct.

- [X] T005 [US1] RED: write the test for scenario 1 — given a run identified `"A-1"`, started `2026-03-01T07:30:00+02:00`, moving time 45 minutes, when recorded, then `ExternalId`, `StartedAt`, `MovingTime`, and `Type` read back unchanged. Confirm it fails to compile because `TrainingActivity` does not exist — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityCreationTests.cs`
- [X] T006 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/ActivityType.cs` with `public enum ActivityType { Running, Cycling }` — exactly these two members, per FR-005
- [X] T007 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/TrainingActivity.cs` as a `sealed class` with readonly properties `ExternalId` (string), `StartedAt` (DateTimeOffset), `MovingTime` (TimeSpan), `Type` (ActivityType), and `HeartRate` (HeartRateSeries?, defaulting to null), assigned from constructor parameters. **No validation yet** — no test demands it until Phase 5. No setters, no `init`, no `with` (FR-024)
- [X] T008 [US1] CONFIRM: add scenario 2 — a ride identified `"A-2"` classifies as `ActivityType.Cycling` and is a distinct instance from the run in T005. Expect no new production code — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityCreationTests.cs`
- [X] T009 [US1] CONFIRM: add scenario 3 — given a start time carrying `+02:00`, both the UTC instant (`StartedAt.UtcDateTime`) and the athlete's local time (`StartedAt.DateTime`, `StartedAt.Offset`) are recoverable (FR-003). Then add the SC-008 case in the same test class: two activities started at the same wall-clock time `2026-10-25T03:30:00`, one at `+03:00` and one at `+02:00` — the repeated hour of a daylight-saving transition. Assert they are two different instants, that each reports local calendar day `2026-10-25`, and that neither is ambiguous. `DateTimeOffset` carries an explicit offset rather than a timezone, which is precisely why the transition needs no special handling — this test pins that. Expect no new production code — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityCreationTests.cs`
- [X] T010 [US1] CONFIRM: add scenario 4 — an activity constructed without heart-rate data has `HeartRate` exactly `null`, distinguishable from any series instance (FR-006) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityCreationTests.cs`
- [X] T011 [US1] CONFIRM: assert that an external identifier with significant surrounding whitespace, such as `"  A-1  "`, round-trips **byte-for-byte including the padding**. FR-002 forbids interpreting or parsing the identifier, and a `.Trim()` added for tidiness would pass every other test in this feature — this is the only test that catches it — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityCreationTests.cs`
- [X] T012 [US1] CONFIRM: assert that a start time in the future relative to `DateTimeOffset.UtcNow` is **accepted**, not refused. The spec's Assumptions state that guarding against upstream clock skew is not this feature's job; this test exists so no later change quietly adds a rule the specification does not call for (Principle VII) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityCreationTests.cs`
- [X] T013 [US1] CONFIRM: assert FR-024 by reflection — `typeof(TrainingActivity).GetProperties()` yields no property with a public setter, and the type is `sealed`. Immutability is currently only an instruction in T007; this makes it a guarded property. Use plain reflection, not an architecture-test package — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityCreationTests.cs`
- [X] T014 [P] [US1] RED: write the test for scenario 5 — a series built from samples `(0 min, 150)`, `(10 min, 175)`, `(20 min, 160)` returns those samples in that order with those values. Confirm it fails because `HeartRateSample` and `HeartRateSeries` do not exist — file: `tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateSeriesTests.cs`
- [X] T015 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/HeartRateSample.cs` as `public readonly record struct HeartRateSample(TimeSpan TimeFromStart, int Bpm)` — times relative to session start, per research R8. The sample validates nothing
- [X] T016 [US1] GREEN: create `src/TrainingLoadAnalyzer.Domain/HeartRateSeries.cs` as a `sealed class` exposing `IReadOnlyList<HeartRateSample> Samples`, assigned from the constructor. **No validation yet**
- [X] T017 [US1] CONFIRM: add the case where an activity is constructed *with* a series and `HeartRate.Samples` round-trips unchanged. Also assert that a series whose last sample lies **beyond** the activity's moving time is accepted — moving time excludes stops while a monitor keeps recording through them, so this is ordinary data, and the spec's Assumptions state sample times are not validated against moving time — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityCreationTests.cs`
- [X] T018 [US1] VERIFY: run `dotnet test` — all green. Then confirm the discipline held: `src/` contains exactly four files, none of them has a guard clause or a load calculation, and no interface, service class, or exception type was created

**Checkpoint**: A training session can be recorded and read back. Nothing validates, nothing calculates.

---

## Phase 4: User Story 2 - Know how demanding a session was (Priority: P2)

**Goal**: Every recorded session yields one TRIMP-point value, computed by Edwards TRIMP when
heart-rate data exists and estimated from moving time when it does not, always marked as which.

**Independent Test**: Compute loads for hand-constructed series and compare against values worked
out by hand from the FR-009 zone table; compute a load for a session with no heart-rate data and
assert both the number and the `Estimated` marking.

**Depends on**: Phase 3 (needs `TrainingActivity` and `HeartRateSeries` to exist).

### Zone classification

- [X] T019 [P] [US2] RED: assert that with maximum 200, a heart rate of 99 bpm (49.5%) has weight 0. Confirm it fails because `HeartRateZone` does not exist — file: `tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateZoneTests.cs`
- [X] T020 [US2] GREEN: create `src/TrainingLoadAnalyzer.Domain/HeartRateZone.cs` as a `public static class` with `public static int WeightFor(int bpm, int maximumHeartRate)`, returning 0 for now
- [X] T021 [US2] RED: extend with an xUnit `[Theory]` covering the whole FR-009 table at maximum 200, including every boundary exactly — 100 bpm (50%) → 1, 119 → 1, 120 (60%) → 2, 140 (70%) → 3, 160 (80%) → 4, 180 (90%) → 5, 199 → 5. Lower bound inclusive, upper exclusive. Confirm the new rows fail — file: `tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateZoneTests.cs`
- [X] T022 [US2] GREEN: implement the five-zone table in `WeightFor` using integer cross-multiplication — `bpm * 100 >= percent * maximumHeartRate` — so boundary samples classify exactly, with no floating-point comparison anywhere (research R10) — file: `src/TrainingLoadAnalyzer.Domain/HeartRateZone.cs`
- [X] T023 [US2] CONFIRM: add scenario 6 — with maximum 190, a sample of 200 bpm (above the stated maximum) has weight 5, since the top band is unbounded above — file: `tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateZoneTests.cs`

### TRIMP points for a series

- [X] T024 [P] [US2] RED: assert the worked example from scenario 2 — maximum 190, samples `(0 min, 150)`, `(10 min, 175)`, `(20 min, 160)` yields exactly `80m` points. By hand: 10 min at weight 3 = 30, 10 min at weight 5 = 50, final sample contributes nothing. Confirm it fails because `TrimpPoints` does not exist — file: `tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateSeriesTests.cs`
- [X] T025 [US2] GREEN: add `public decimal TrimpPoints(int maximumHeartRate)` to `HeartRateSeries`, walking consecutive sample pairs and summing `WeightFor(earlier.Bpm, maximumHeartRate) * (later.TimeFromStart - earlier.TimeFromStart).TotalMinutes` as `decimal`. Use `decimal` throughout — a `double` here makes the assertion in T024 fail by a rounding error (research R9) — file: `src/TrainingLoadAnalyzer.Domain/HeartRateSeries.cs`
- [X] T026 [US2] CONFIRM: a single-sample series yields `0m` points — the final sample has no successor. This is the edge case the spec names — file: `tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateSeriesTests.cs`
- [X] T027 [US2] CONFIRM: a series with a long gap charges the gap to the sample preceding it, per the hold-until-next rule fixed in the spec's Assumptions — file: `tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateSeriesTests.cs`

### Measured load on an activity

- [X] T028 [P] [US2] RED: assert scenarios 1 and 2 together — a run carrying the T024 series, with maximum 190, returns a load of `80m` points marked `Measured`. Confirm it fails because `TrainingLoad`, `LoadProvenance`, and `CalculateTrainingLoad` do not exist — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MeasuredTrainingLoadTests.cs`
- [X] T029 [US2] GREEN: create `src/TrainingLoadAnalyzer.Domain/LoadProvenance.cs` with `public enum LoadProvenance { Measured, Estimated }`, and `src/TrainingLoadAnalyzer.Domain/TrainingLoad.cs` as `public readonly record struct TrainingLoad(decimal Points, LoadProvenance Provenance)`. There must be no way to obtain `Points` without `Provenance` (FR-014, SC-007)
- [X] T030 [US2] GREEN: add `public TrainingLoad CalculateTrainingLoad(int maximumHeartRate)` to `TrainingActivity`, returning `new TrainingLoad(HeartRate.TrimpPoints(maximumHeartRate), LoadProvenance.Measured)` for the heart-rate case only. **No null branch and no maximum-heart-rate guard yet** — no test demands either — file: `src/TrainingLoadAnalyzer.Domain/TrainingActivity.cs`
- [X] T031 [US2] CONFIRM: scenario 3 — two activities whose series have identical intensity, one lasting twice as long, and the longer one's load is strictly greater (SC-004) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MeasuredTrainingLoadTests.cs`
- [X] T032 [US2] CONFIRM: scenario 4 — with maximum 200, a session held entirely at 100 bpm (zone 1) against one held entirely at 180 bpm (zone 5) for the same duration gives a load exactly five times greater — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MeasuredTrainingLoadTests.cs`
- [X] T033 [US2] CONFIRM: the second clause of SC-005 — with maximum 200, two sessions of equal duration held at 141 bpm and at 159 bpm produce **equal** loads, because both sit in zone 3. SC-005 promises "greater **or equal**", and this is the surprising half: Edwards TRIMP is a step function, so intensity differences inside a band are invisible. A reviewer will question it, and this test is the answer — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MeasuredTrainingLoadTests.cs`
- [X] T034 [US2] CONFIRM: scenario 5 — a session held entirely below 50% of maximum yields `0m` points marked `Measured`, not `Estimated` and not an error — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MeasuredTrainingLoadTests.cs`
- [X] T035 [US2] CONFIRM: scenario 7 — calling `CalculateTrainingLoad` repeatedly on the same activity with the same maximum returns equal values, and the implementation reads no clock or ambient state (SC-003, contract C4) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MeasuredTrainingLoadTests.cs`
- [X] T036 [US2] CONFIRM: scenario 8 — a `Running` activity and a `Cycling` activity with identical series and identical maximum produce `TrainingLoad` values that are `==`. This is SC-009 and contract C6; if it fails, `ActivityType` has leaked into the calculation — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MeasuredTrainingLoadTests.cs`

### Estimated load

- [X] T037 [P] [US2] RED: assert scenario 9 — an activity with no heart-rate data and 40 minutes of moving time returns `80m` points marked `Estimated`. Confirm it fails (currently a null dereference on `HeartRate`) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/EstimatedTrainingLoadTests.cs`
- [X] T038 [US2] GREEN: add the null-heart-rate branch to `CalculateTrainingLoad`, returning `new TrainingLoad((decimal)MovingTime.TotalMinutes * 2, LoadProvenance.Estimated)` per FR-013. The weight 2 is fixed by the spec's Assumptions — do not make it a parameter or a constant to be tuned — file: `src/TrainingLoadAnalyzer.Domain/TrainingActivity.cs`
- [X] T039 [US2] CONFIRM: scenario 10 — the measured `80m` from T028 and the estimated `80m` from T037 are distinguishable. Assert on `Provenance`, not only on `Points`; a test that passes while asserting only the number is not testing FR-014 — file: `tests/TrainingLoadAnalyzer.Domain.Tests/EstimatedTrainingLoadTests.cs`
- [X] T040 [US2] VERIFY: run `dotnet test` — all green. REFACTOR if `TrimpPoints` or `CalculateTrainingLoad` has grown unclear, keeping tests green throughout. Confirm no `TrainingLoadCalculator`, no `ITrainingLoadCalculator`, and no strategy type was introduced (research R12)

**Checkpoint**: Every valid session yields a marked load. Nothing is validated yet.

---

## Phase 5: User Story 3 - Refuse to record a malformed session (Priority: P3)

**Goal**: Malformed session details are refused at construction with the violated rule named, and no
partially-formed record survives.

**Independent Test**: Attempt construction with exactly one rule violated per case, and assert both
the exception type and its `ParamName`.

**Depends on**: Phase 3. Independent of Phase 4 except T056–T058, which guard the load calculation.

### TrainingActivity constructor guards

- [ ] T041 [P] [US3] RED: assert scenario 1 — a moving time of `TimeSpan.Zero` throws `ArgumentOutOfRangeException` whose `ParamName` is `"movingTime"`. Confirm it fails because no guard exists — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityValidationTests.cs`
- [ ] T042 [US3] GREEN: add the moving-time guard to the `TrainingActivity` constructor, throwing `ArgumentOutOfRangeException(nameof(movingTime), ...)` when it is not strictly positive (FR-017) — file: `src/TrainingLoadAnalyzer.Domain/TrainingActivity.cs`
- [ ] T043 [US3] CONFIRM: scenario 2 — a negative moving time throws the same way — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityValidationTests.cs`
- [ ] T044 [US3] RED: assert scenario 3 — an `ActivityType` value outside the defined set, cast from `(ActivityType)99`, throws `ArgumentOutOfRangeException` with `ParamName` `"activityType"`. Confirm it fails — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityValidationTests.cs`
- [ ] T045 [US3] GREEN: add the activity-type guard using `Enum.IsDefined`, per FR-018. This also covers the unset `default` case — file: `src/TrainingLoadAnalyzer.Domain/TrainingActivity.cs`
- [ ] T046 [US3] RED: assert scenario 4 — `default(DateTimeOffset)` as the start time throws `ArgumentException` with `ParamName` `"startedAt"`. Confirm it fails — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityValidationTests.cs`
- [ ] T047 [US3] GREEN: add the start-time guard, per FR-019. Guard only against the `default` value — T012 asserts that a future start time is accepted — file: `src/TrainingLoadAnalyzer.Domain/TrainingActivity.cs`
- [ ] T048 [US3] RED: assert scenario 5 as a `[Theory]` over `null`, `""`, and `"   "` — each throws `ArgumentException` with `ParamName` `"externalId"`. Confirm all three fail — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityValidationTests.cs`
- [ ] T049 [US3] GREEN: add the external-identifier guard using `string.IsNullOrWhiteSpace`, per FR-020. Store the value verbatim — do not trim, parse, or interpret it (FR-002), which T011 asserts — file: `src/TrainingLoadAnalyzer.Domain/TrainingActivity.cs`

### HeartRateSeries constructor guards

- [ ] T050 [P] [US3] RED: assert scenario 6 — a series containing a sample of 300 bpm throws `ArgumentOutOfRangeException` with `ParamName` `"samples"`, and the message names the offending value. Confirm it fails — file: `tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateSeriesTests.cs`
- [ ] T051 [US3] GREEN: add the plausibility guard to the `HeartRateSeries` constructor, rejecting any `Bpm` outside 20–250 inclusive. Refuse the series — do not silently drop the sample (FR-021, Principle VI) — file: `src/TrainingLoadAnalyzer.Domain/HeartRateSeries.cs`
- [ ] T052 [US3] RED: assert scenario 7 as a `[Theory]` over two cases — samples at `(10 min, 150)` then `(5 min, 160)` (decreasing), and samples at `(10 min, 150)` then `(10 min, 160)` (equal timestamps, which are not ascending). Each throws `ArgumentException` with `ParamName` `"samples"`, **and** the message contains `"ascending"`. The ParamName alone is `"samples"` for both this rule and T054's, so without the message assertion this test would also pass against the empty-list guard and could go green for the wrong reason — confirm both fail, and fail on the ordering — file: `tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateSeriesTests.cs`
- [ ] T053 [US3] GREEN: add the ascending-order guard, treating equal timestamps as not ascending — this is what resolves the duplicate-instant edge case — file: `src/TrainingLoadAnalyzer.Domain/HeartRateSeries.cs`
- [ ] T054 [US3] RED: assert scenario 8 as a `[Theory]` over a null list and an empty list — each throws `ArgumentException` with `ParamName` `"samples"`, **and** the message contains `"at least one sample"`. See T052: these two rules share a ParamName, so the message is what makes each test fail for its own reason. Confirm both fail — file: `tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateSeriesTests.cs`
- [ ] T055 [US3] GREEN: add the non-null, non-empty guard, per FR-021. An empty series is a refusal, not a synonym for absent heart-rate data (FR-006) — file: `src/TrainingLoadAnalyzer.Domain/HeartRateSeries.cs`

### Load calculation guard

- [ ] T056 [US3] RED: assert scenario 9 as a `[Theory]` over maximum heart rates of `0` and `-1` — an activity **with** heart-rate data throws `ArgumentOutOfRangeException` with `ParamName` `"maximumHeartRate"`. Confirm it fails — file: `tests/TrainingLoadAnalyzer.Domain.Tests/MeasuredTrainingLoadTests.cs`
- [ ] T057 [US3] GREEN: add the guard to the measured branch of `CalculateTrainingLoad` only, per FR-016 — file: `src/TrainingLoadAnalyzer.Domain/TrainingActivity.cs`
- [ ] T058 [US3] CONFIRM: an activity **without** heart-rate data and a maximum of `0` does **not** throw and returns its estimate normally. The estimate never reads the maximum, so guarding it there would invent a rule the spec does not state. This asymmetry is deliberate (Principle VII) and this test is what protects it — file: `tests/TrainingLoadAnalyzer.Domain.Tests/EstimatedTrainingLoadTests.cs`

### Refusal completeness

- [ ] T059 [US3] CONFIRM: scenario 10 — after each refused construction, no instance is observable. Assert that `Assert.Throws` is what returns, and that no out-parameter, static registry, or partially-populated object exists to inspect (FR-023, contract C2) — file: `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingActivityValidationTests.cs`
- [ ] T060 [US3] VERIFY: run `dotnet test` — all green. Confirm every guard throws `ArgumentException` or `ArgumentOutOfRangeException` with a `ParamName`, that none throws a bare `Exception` or `InvalidOperationException`, and that no custom exception type was created (data-model, Types deliberately not created). Then check contract C3's distinctness clause by hand: for each of the three `HeartRateSeries` rules, swap in the wrong guard's message and confirm the corresponding test goes red. Rules sharing a `ParamName` must be told apart by their message

**Checkpoint**: All three user stories complete and independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T061 [P] Work through the four hand-checks in [quickstart.md](./quickstart.md) — the worked TRIMP example, the colliding estimate, boundary classification at maximum 200 and 140 bpm, and cross-modality — confirming each by hand against the spec rather than against the code
- [ ] T062 [P] Run the constitution checks from [quickstart.md](./quickstart.md): `cat src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj` shows no references, `grep -ril "strava" src/ tests/` returns nothing, and `grep -rl -E "Moq|NSubstitute|FakeItEasy" tests/` returns nothing
- [ ] T063 Run `dotnet build -warnaserror` and resolve any warning
- [ ] T064 Review the feature's git history and confirm each test commit precedes the production commit that makes it pass. Commit order is the evidence that Principle I was followed; coverage percentage is not, and must not be used as a substitute
- [ ] T065 Complete the Principle II semantic review by reading every type, member, and parameter name in `src/` for provider vocabulary — no `SufferScore`, `RelativeEffort`, `athlete_id`, or similar. No tool catches this; it is the half of Principle II that only a human reviewer can check (SC-010)
- [ ] T066 Run the feature completion review from the constitution's Development Workflow: acceptance criteria satisfied, TDD followed, tests green, no unnecessary domain dependency, no unnecessary abstractions remaining, agreed error scenarios handled, AI-generated code human-reviewed, spec and implementation aligned

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: intentionally empty
- **User Story 1 (Phase 3)**: depends on Phase 1. **Blocks Stories 2 and 3** — both need `TrainingActivity`
- **User Story 2 (Phase 4)**: depends on Phase 3
- **User Story 3 (Phase 5)**: depends on Phase 3. T056–T058 additionally depend on Phase 4
- **Polish (Phase 6)**: depends on all three stories

### A note on story independence

The template's model assumes stories are mutually independent. These are not, and the spec says so:
User Story 2's "Why this priority" states it is "meaningless without User Story 1". Story 1 creates
the type the other two operate on. Stories 2 and 3 are independent *of each other* — apart from
T056–T058 — and could be worked in parallel by two people once Phase 3 is done.

### Within each cycle

RED must fail, and fail for the stated reason, before its GREEN task begins. A RED task that passes
on first run means the test is not testing what it claims — fix the test, do not proceed.

### Parallel Opportunities

Genuine parallelism is limited because most tasks in a story touch the same test file. Marked [P]
tasks are the ones that open work in a *different* file with no incomplete dependency:

- T014 (HeartRateSeriesTests.cs) alongside T008–T013 (TrainingActivityCreationTests.cs)
- T019 (HeartRateZoneTests.cs), T024 (HeartRateSeriesTests.cs), T028 (MeasuredTrainingLoadTests.cs), T037 (EstimatedTrainingLoadTests.cs) each open a new file
- T041 (TrainingActivityValidationTests.cs) and T050 (HeartRateSeriesTests.cs) can proceed in parallel
- T061 and T062 in Phase 6

Every other task either edits a file an earlier task is still in, or depends on the GREEN before it.

---

## Implementation Strategy

### MVP scope

Phases 1 + 3 (T001–T018). That delivers a trustworthy, immutable session record — the thing every
later feature reads from. It does not calculate or validate anything, which is honest: an MVP that
records sessions correctly is more useful than one that calculates loads on records it cannot trust.

### Incremental delivery

1. Phase 1 → harness runs, zero tests
2. Phase 3 → sessions record and read back **(MVP)**
3. Phase 4 → every session yields a marked load
4. Phase 5 → malformed sessions are refused
5. Phase 6 → constitution compliance review

Stop at any checkpoint; each leaves a working, tested increment.

---

## Notes

- 66 tasks: 4 setup, 14 for US1, 22 for US2, 20 for US3, 6 polish
- **CONFIRM tasks are not filler.** They pin behaviour the design already implies, and a CONFIRM
  that unexpectedly fails has found a real defect. Do not "fix" one by adding production code until
  you have established which is wrong, the test or the design
- Four CONFIRM tasks exist specifically to stop a rule being added that the specification does not
  call for: T011 (identifier not trimmed), T012 (future start time accepted), T017 (samples beyond
  moving time accepted), and T058 (no maximum-heart-rate guard on the estimate). Each guards an
  Assumption in spec.md, which is where the reasoning lives
- Commit after each task or each RED/GREEN pair, keeping test commits ahead of production commits
- Do not create: an interface, a service or calculator class, a `Result<T>`, a custom exception
  type, a `MaximumHeartRate` value object, a mocking library, an assertion library, or an
  architecture-test package. Each was considered and rejected with a recorded revisit trigger in
  [research.md](./research.md) and [data-model.md](./data-model.md). If implementation makes one
  feel necessary, that is a signal to amend the plan deliberately — not to add it quietly
