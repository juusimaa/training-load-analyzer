# Data Model: Training Activity Domain

**Feature**: 001-training-activity-domain | **Date**: 2026-09-16

Seven types, no interfaces, no base classes, no generics. Every type is immutable. Design rationale
for the choices below is in [research.md](./research.md); this document states *what* is built and
which requirement each part satisfies.

---

## ActivityType (enum)

```text
Running
Cycling
```

- FR-005: no other value is accepted. A value outside the defined set is refused at activity
  construction, which also covers the `default` / unset case.

## LoadProvenance (enum)

```text
Measured    // computed from heart-rate data per FR-009
Estimated   // computed from moving time per FR-013
```

- FR-014. Deliberately not a `bool` — a `bool IsEstimated` reads ambiguously at call sites and
  cannot grow a third case if a later feature adds one.

## HeartRateSample (readonly record struct)

| Field | Type | Meaning |
|-------|------|---------|
| `TimeFromStart` | `TimeSpan` | When the sample was taken, measured from the session's start |
| `Bpm` | `int` | Heart rate in beats per minute |

- FR-007. Times are relative to the session start rather than absolute, so the load calculation
  never touches dates or offsets (research R8).
- The sample itself validates nothing. Ordering and plausible range are invariants of the *series*,
  not of an individual sample, and FR-021 makes them a reason to refuse the session. Keeping one
  validation site avoids two places that both half-check the same data.
- Structural equality is wanted: tests compare expected and actual samples directly.

## HeartRateSeries (sealed class)

| Member | Type | Meaning |
|--------|------|---------|
| `Samples` | `IReadOnlyList<HeartRateSample>` | The samples, in ascending time order |
| `TrimpPoints(int maximumHeartRate)` | `decimal` | Edwards TRIMP points for this series |

**Invariants, all enforced in the constructor (FR-021):**

| Rule | Requirement | Refusal |
|------|-------------|---------|
| The sample list is not null and not empty | FR-021, FR-006 | `ArgumentException(paramName: "samples")` — "a heart-rate series must contain at least one sample" |
| Sample times are in ascending order | FR-021 | `ArgumentException(paramName: "samples")` — message names the ordering and the offending index |
| Every `Bpm` is within 20–250 inclusive | FR-021 | `ArgumentOutOfRangeException(paramName: "samples")` — message names the offending sample's value |

Equal timestamps are *not* ascending and are therefore refused, which resolves the "duplicated at
the same instant" edge case.

**`TrimpPoints` behaviour (FR-009, FR-010):**

Walks consecutive pairs of samples. Each pair contributes
`HeartRateZone.WeightFor(earlier.Bpm, maximumHeartRate) × (later.TimeFromStart - earlier.TimeFromStart).TotalMinutes`,
as `decimal`. The final sample has no successor and so contributes nothing — which makes a
single-sample series score 0, the edge case the specification calls out. Gaps are charged to the
sample preceding them, per the hold-until-next rule the spec fixed in its Assumptions.

## HeartRateZone (static class)

| Member | Signature | Meaning |
|--------|-----------|---------|
| `WeightFor` | `static int WeightFor(int bpm, int maximumHeartRate)` | The Edwards weight 0–5 for a heart rate |

- FR-009. Returns 0 below 50% of maximum, then 1–5 for the five bands, with 5 covering everything
  at or above 90% including rates above the stated maximum.
- Classification is by integer cross-multiplication — `bpm * 100 >= percent * maximumHeartRate` —
  so a sample sitting exactly on a boundary lands in the upper zone deterministically, with no
  floating-point rounding (research R10).
- A static class, not an injected strategy: the zone table is fixed by FR-009 and by the spec's
  Assumptions, and nothing selects between tables.

## TrainingLoad (readonly record struct)

| Field | Type | Meaning |
|-------|------|---------|
| `Points` | `decimal` | TRIMP points, never negative |
| `Provenance` | `LoadProvenance` | Whether measured or estimated |

- FR-008, FR-014, SC-007. The two fields are inseparable: there is no constructor, property, or
  conversion that yields the number alone, so a consumer cannot accidentally drop the provenance.
- `decimal` so that hand-computed expectations match exactly (SC-006, research R9).
- Structural equality supports SC-009 directly — a run and a ride with identical series produce
  two `TrainingLoad` values that are `==`.

## TrainingActivity (sealed class)

| Member | Type | Meaning |
|--------|------|---------|
| `ExternalId` | `string` | Opaque provider-neutral identifier |
| `StartedAt` | `DateTimeOffset` | Start instant plus the athlete's offset from UTC |
| `MovingTime` | `TimeSpan` | Time spent actually moving |
| `Type` | `ActivityType` | Running or cycling |
| `HeartRate` | `HeartRateSeries?` | The series, or `null` when none was recorded |
| `CalculateTrainingLoad` | `TrainingLoad CalculateTrainingLoad(int maximumHeartRate)` | This session's load |

**Invariants, all enforced in the constructor:**

| Rule | Requirement | Refusal |
|------|-------------|---------|
| `ExternalId` is not null, empty, or whitespace | FR-020 | `ArgumentException(paramName: "externalId")` |
| `StartedAt` is not `default(DateTimeOffset)` | FR-019 | `ArgumentException(paramName: "startedAt")` |
| `MovingTime` is strictly positive | FR-017 | `ArgumentOutOfRangeException(paramName: "movingTime")` |
| `Type` is a defined `ActivityType` value | FR-018 | `ArgumentOutOfRangeException(paramName: "activityType")` |

Because every check runs before the object exists, a refused construction leaves nothing behind —
FR-023 holds by construction rather than by cleanup. All state is readonly, satisfying FR-024
without a `with`-expression escape hatch (research R11).

**`CalculateTrainingLoad` behaviour:**

| Condition | Result | Requirement |
|-----------|--------|-------------|
| `HeartRate` is not null, `maximumHeartRate > 0` | `TrainingLoad(HeartRate.TrimpPoints(maximumHeartRate), Measured)` | FR-009, FR-011, FR-012 |
| `HeartRate` is not null, `maximumHeartRate <= 0` | `ArgumentOutOfRangeException(paramName: "maximumHeartRate")` | FR-016 |
| `HeartRate` is null | `TrainingLoad(MovingTime.TotalMinutes × 2, Estimated)` | FR-013 |

The third row does **not** validate `maximumHeartRate`: the estimate never reads it, so refusing a
session that needs no maximum heart rate would invent a rule the specification does not state
(Principle VII). This asymmetry is deliberate and is worth its own test.

The method lives on the activity rather than in a calculator service, and takes the maximum heart
rate as an argument rather than owning it — FR-015, and research R12 for why no service or interface
is introduced.

---

## Types deliberately *not* created

| Candidate | Why not | Revisit when |
|-----------|---------|--------------|
| `MaximumHeartRate` value object | It would validate at its own construction, so FR-016's "refuse to *compute* a measured load" and User Story 3 scenario 9 could never be exercised as written. A plain `int` parameter matches the specification literally and adds no type. | A later feature stores an athlete profile and the value starts being passed around rather than supplied at one call site. |
| `InvalidTrainingActivityException` or similar | `ArgumentException` / `ArgumentOutOfRangeException` already carry a `ParamName` that names the violated field mechanically, which is exactly what FR-022 asks for — and tests assert on `ParamName` rather than on message text. A custom type adds one more thing without making any refusal clearer. | The Strava import feature needs to distinguish "this activity broke a domain rule, skip it and continue" from "the caller passed nonsense", which is a genuine behavioural difference a shared type cannot express. |
| `TrainingLoadCalculator` / `ITrainingLoadCalculator` | One method, one implementation, nothing to substitute — the case Principle III names as a defect. | A second load method must coexist with Edwards TRIMP, or a caller must choose one at runtime. |
| `Result<T>` or a validation-error collection | FR-023 is satisfied by a throwing constructor; nothing currently consumes more than the first violation. | Batch import must reject one activity and carry on (research R7). |

---

## Requirement trace

| Requirement | Realised by |
|-------------|-------------|
| FR-001 | `TrainingActivity` fields |
| FR-002 | `TrainingActivity.ExternalId`, stored verbatim and never parsed |
| FR-003 | `StartedAt` as `DateTimeOffset` |
| FR-004 | `MovingTime` as `TimeSpan`; no elapsed-time field exists |
| FR-005 | `ActivityType` enum + constructor check |
| FR-006 | `HeartRate` is nullable; `null` ≠ an empty series, which FR-021 refuses |
| FR-007 | `HeartRateSeries` + `HeartRateSample` |
| FR-008 | `CalculateTrainingLoad` returns a `TrainingLoad` for every valid activity |
| FR-009 | `HeartRateZone.WeightFor` + `HeartRateSeries.TrimpPoints` |
| FR-010 | Consecutive-pair walk in `TrimpPoints` |
| FR-011 | Pure function of activity state + one argument; no clock, no ambient state |
| FR-012 | Weight depends only on bpm and maximum; `ActivityType` is never read by the calculation |
| FR-013 | Null-heart-rate branch of `CalculateTrainingLoad` |
| FR-014 | `TrainingLoad.Provenance` |
| FR-015 | `maximumHeartRate` parameter; no field on `TrainingActivity` |
| FR-016 | Guard in the measured branch |
| FR-017 – FR-020 | `TrainingActivity` constructor guards |
| FR-021 | `HeartRateSeries` constructor guards |
| FR-022 | Exception type + `ParamName` per guard |
| FR-023 | Guards precede object creation |
| FR-024 | All state readonly; no setters, no `with` |
| FR-025 | Type names and the `.csproj`'s empty dependency list |
