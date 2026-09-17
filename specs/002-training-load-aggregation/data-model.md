# Data Model: Training Load Aggregation

**Feature**: 002-training-load-aggregation | **Date**: 2026-09-17

Five new types in `TrainingLoadAnalyzer.Domain`, alongside the seven feature 001 created. Nothing
from feature 001 changes — this feature is additive, and `TrainingActivity`, `TrainingLoad`, and
`LoadProvenance` are consumed exactly as they are.

Semantics below trace to the numbered requirements in [spec.md](./spec.md); the decisions behind the
shapes are in [research.md](./research.md).

---

## LoadBasis (enum)

| Member | Meaning |
|--------|---------|
| `None` | No session contributed to this total |
| `Measured` | Every contributing session's load was measured |
| `Estimated` | Every contributing session's load was estimated |
| `Mixed` | Both kinds contributed |

- FR-014, FR-015, SC-007. Exactly the four states the specification names.
- `None` is declared first so `default(LoadBasis)` is `None`, which is the correct basis for the
  zero-load entries that dominate a sparse series.
- Distinct from feature 001's `LoadProvenance`, which describes one activity and has two states. A
  single activity can never be `Mixed` or `None`, so widening the existing enum was rejected
  (research R10).

**Derivation from the contributing activities:**

| Contributing loads | Basis |
|--------------------|-------|
| none | `None` |
| all `LoadProvenance.Measured` | `Measured` |
| all `LoadProvenance.Estimated` | `Estimated` |
| at least one of each | `Mixed` |

A single estimated load among many measured ones yields `Mixed` (FR-015) — the proportion is not
recorded.

---

## DateRange (sealed class)

| Member | Type | Meaning |
|--------|------|---------|
| `Start` | `DateOnly` | First day of the requested range, included |
| `End` | `DateOnly` | Last day of the requested range, included |
| `Days` | `IEnumerable<DateOnly>` | Every day from `Start` to `End` inclusive, ascending |

**Invariants, enforced in the constructor:**

| Rule | Requirement | Refusal |
|------|-------------|---------|
| `Start` is not `default(DateOnly)` | FR-020 | `ArgumentException(paramName: "start")` |
| `End` is not `default(DateOnly)` | FR-020 | `ArgumentException(paramName: "end")` |
| `End` is not earlier than `Start` | FR-019 | `ArgumentException(paramName: "end")` |

Both endpoint days are included (FR-009), so a range whose `Start` equals its `End` is one day long
and is valid. The two `end` refusals share a `ParamName`, so — per feature 001's contract guarantee
C3 — their messages must tell them apart, and a test for one must not pass against the other.

A sealed class rather than a `readonly record struct` because `default(DateRange)` would otherwise
be an instance no guard ever saw (research R7).

---

## IsoWeek (readonly record struct)

| Member | Type | Meaning |
|--------|------|---------|
| `Year` | `int` | ISO-8601 **week-numbering** year, which may differ from the calendar year of its days |
| `Week` | `int` | ISO-8601 week number, 1–53 |
| `Monday` | `DateOnly` | First day of the week |
| `Sunday` | `DateOnly` | Last day of the week |

- FR-002, User Story 2 scenario 6. `Year` is emphatically not a calendar year: 2025-12-29 belongs to
  `2026-W01`, and 2027-01-03 belongs to `2026-W53`.
- Constructed from any day via a `For(DateOnly)` factory that delegates to
  `System.Globalization.ISOWeek` (research R5). `Monday` and `Sunday` are derived, not stored
  independently, so they cannot disagree with `Year`/`Week`.
- Structural equality and a well-defined order make the weekly series assertable as whole values.
  Ordering is by `Monday`; ordering by `(Year, Week)` gives the same answer but needs an argument
  about the year boundary to see why.
- 2026 is a 53-week ISO year, which is why the weekly series is enumerated by walking Mondays rather
  than by incrementing a week number (research R6).

---

## DailyTrainingLoad (readonly record struct)

| Field | Type | Meaning |
|-------|------|---------|
| `Day` | `DateOnly` | The calendar day, in the athlete's local time at each session's own offset |
| `Points` | `decimal` | Sum of the loads of every session attributed to this day |
| `ActivityCount` | `int` | How many sessions contributed |
| `Basis` | `LoadBasis` | How much the total can be trusted |

- FR-001, FR-007, FR-008, FR-014, FR-016.
- A rest day is `(day, 0m, 0, None)`. A day whose sessions all scored zero is `(day, 0m, n,
  Measured)` — the count and basis are what keep them distinguishable (SC-002, spec Edge Cases).
- `decimal` so a total is exactly reproducible by hand (FR-022, SC-004).

---

## WeeklyTrainingLoad (readonly record struct)

| Field | Type | Meaning |
|-------|------|---------|
| `Week` | `IsoWeek` | The ISO week, Monday to Sunday |
| `Points` | `decimal` | Sum of the loads of every session in all seven of the week's days |
| `ActivityCount` | `int` | How many sessions contributed |
| `Basis` | `LoadBasis` | How much the total can be trusted |

- FR-002, FR-006, FR-013, FR-014, FR-016.
- Always covers all seven days of its week, including days outside the requested range (FR-013,
  SC-012). There is deliberately no "partial" flag: the developer's decision was to extend rather
  than to mark, so no weekly entry is ever partial and a flag would always be false.

---

## TrainingLoadAggregator (static class)

| Member | Signature |
|--------|-----------|
| `AggregateDaily` | `IReadOnlyList<DailyTrainingLoad> AggregateDaily(IReadOnlyCollection<TrainingActivity> activities, DateRange range, int maximumHeartRate)` |
| `AggregateWeekly` | `IReadOnlyList<WeeklyTrainingLoad> AggregateWeekly(IReadOnlyCollection<TrainingActivity> activities, DateRange range, int maximumHeartRate)` |

**Behaviour:**

| Condition | Result | Requirement |
|-----------|--------|-------------|
| `activities` is null | `ArgumentNullException(paramName: "activities")` | FR-021, research R12 |
| `range` is null | `ArgumentNullException(paramName: "range")` | FR-021 |
| `activities` is empty | Full gap-free zero series for the range | FR-018 |
| A session's local day is inside the range | Contributes to that day's total | FR-003, FR-004, FR-009 |
| A session's local day is outside the range | Contributes to no daily total | FR-009, SC-006 |
| A session's local day is outside the range but inside an extended edge week | Contributes to that weekly total only | FR-013, SC-006 |
| A session has heart-rate data and `maximumHeartRate <= 0` | Feature 001's `ArgumentOutOfRangeException` propagates unchanged | 001 FR-016, research R3 |

**Day assignment** is `DateOnly.FromDateTime(activity.StartedAt.DateTime)` — the offset-local date,
per FR-004. The session is attributed wholly to that one day (FR-003); nothing is split, because an
activity has a start time but no end time.

**The weekly series** is built over an extended range — the Monday of the range's first week through
the Sunday of its last — and chunked into sevens, which makes FR-017 and SC-012 true by construction
(research R8). The daily series is never extended and stays strictly inside the requested range.

Both methods are pure: no clock, no storage, no static mutable state, no dependence on the order in
which activities were supplied (FR-011, FR-012, FR-024, SC-005).

---

## Types deliberately *not* created

| Candidate | Why not | Revisit when |
|-----------|---------|--------------|
| `ITrainingLoadAggregator` | One implementation, nothing to substitute, no seam any test needs — the case Principle III calls a defect. | A second aggregation strategy must be chosen at runtime. |
| `TrainingLoadSeries` wrapping both series | Two calls returning two lists is the entire feature; a wrapper would exist only to hold them adjacent. | The Fitness/Form feature needs both series passed as one value. |
| `WeekRange` or an extended-range type | The extension is three lines inside one method, not a concept the caller ever sees. | A caller needs to ask which days a weekly series actually covered. |
| A `partial` flag on `WeeklyTrainingLoad` | Under the developer's FR-013 decision no week is ever partial, so the flag would be permanently false. | FR-013 is ever revisited toward option (a), reporting partial weeks instead of extending. |
| `AthleteProfile` holding the maximum heart rate | Feature 001 already rejected owning this value; it is an argument, per its FR-015. | An athlete profile exists for its own reasons and the value starts being passed through many layers. |
| A custom aggregation exception | `ArgumentException` / `ArgumentNullException` carry `ParamName`, which is what FR-021 asks for. | Import must distinguish "skip this activity" from "the caller passed nonsense". |

---

## Requirement trace

| Requirement | Realised by |
|-------------|-------------|
| FR-001 | `TrainingLoadAggregator.AggregateDaily` → `DailyTrainingLoad` |
| FR-002 | `TrainingLoadAggregator.AggregateWeekly` → `WeeklyTrainingLoad`, `IsoWeek` |
| FR-003 | One day key per activity; no splitting logic exists |
| FR-004 | `DateOnly.FromDateTime(StartedAt.DateTime)` — offset-local |
| FR-005 | Series built by walking `DateRange.Days`, not by grouping activities |
| FR-006 | Weekly series built by walking Mondays across the extended range |
| FR-007 | Days and weeks with no activity yield `0m`, count `0`, basis `None` |
| FR-008 | `decimal` sum per day key; `ActivityType` never read |
| FR-009 | Day-key membership test against `range`; both endpoints inclusive |
| FR-010 | Series built in range order; no sort of caller input needed |
| FR-011 | Grouping by day key, not by input position |
| FR-012 | Pure static methods; no clock, no ambient state |
| FR-013 | Extended range from first week's Monday to last week's Sunday |
| FR-014 | `LoadBasis` on both result types |
| FR-015 | Basis derivation table above — any estimate plus any measurement is `Mixed` |
| FR-016 | `ActivityCount` on both result types |
| FR-017 | Weekly totals are chunks of a contiguous daily series (research R8) |
| FR-018 | Empty collection takes the same path as a sparse one |
| FR-019 | `DateRange` constructor guard |
| FR-020 | `DateRange` constructor guards on `default(DateOnly)` |
| FR-021 | Exception type + `ParamName` per guard; distinct messages where `ParamName` is shared |
| FR-022 | `decimal` throughout; no rounding at any step |
| FR-023 | No identifier comparison exists anywhere in the aggregation |
| FR-024 | No storage, no network, no clock read |
| FR-025 | Type names and the domain `.csproj`'s empty dependency list |
