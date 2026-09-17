# Phase 0 Research: Training Load Aggregation

**Feature**: 002-training-load-aggregation | **Date**: 2026-09-17

The spec carries no `[NEEDS CLARIFICATION]` markers — both open questions were resolved by the
developer during `/speckit-specify` and written into FR-004 and FR-013. What remains are mechanical
decisions this plan must settle before tasks can be written. Each entry records the decision, why,
what was rejected, and what would make it worth revisiting.

Decisions carried over unchanged from
[feature 001's research](../001-training-activity-domain/research.md) — `net10.0`, xUnit v3 with
built-in `Assert.*`, no assertion library, no mocking library, `src/` + `tests/` layout — are not
re-argued here. Only what this feature adds is below.

---

## R1: Which project hosts the aggregation

**Decision**: `TrainingLoadAnalyzer.Domain`, the assembly feature 001 created. No new project.

**Rationale**: Aggregation is pure domain logic over domain types. It reads `TrainingActivity` and
`TrainingLoad` and produces new domain values; it touches no storage, no clock, no network (FR-024).
The preliminary project plan sketches a `TrainingLoadAnalyzer.Application` project, but nothing in
this feature needs a use-case layer to isolate — there is no caller, no transaction, no port. An
empty second assembly created now would be speculative structure, which Principle III names as a
defect.

**Alternatives considered**: a new `.Application` project (rejected: nothing to isolate yet); a
separate `.Domain.Aggregation` assembly (rejected: same argument, plus it would have to reference
the domain anyway).

**Revisit when**: a use case appears that orchestrates aggregation with something outside the
domain — the Strava import feature loading activities from storage and then aggregating them is the
likely trigger.

---

## R2: The shape of the entry point

**Decision**: a `static class TrainingLoadAggregator` with two public methods, `AggregateDaily` and
`AggregateWeekly`. No interface, no instance, no injected collaborator.

**Rationale**: The computation is a pure function of its arguments, so there is no state for an
instance to hold and nothing to substitute at a seam. This mirrors the existing `HeartRateZone`
static class rather than contradicting feature 001's rejection of a `TrainingLoadCalculator`: that
was rejected because the calculation had an obvious owner — the activity itself. Aggregation has no
such owner. It is a fold over a *collection* plus a range, and neither a `List<TrainingActivity>`
nor a `DateRange` is the natural place to put it.

**Alternatives considered**:

- Extension methods on `IEnumerable<TrainingActivity>` — rejected: it reads well at the call site
  but scatters the feature across a type the domain does not own, and makes the requirement trace
  harder to follow.
- An `ITrainingLoadAggregator` interface — rejected outright: one implementation, nothing to
  substitute, no seam a test needs. Principle III.
- A `TrainingLoadSeries` object owning both series and computing lazily — rejected: it adds
  lifetime and identity questions to something that is a value, and would obscure that each
  aggregation is a single pure call.

**Revisit when**: a second aggregation strategy must coexist and a caller must choose between them
at runtime, or aggregation acquires a dependency (a calendar, an athlete profile) that a test needs
to control.

---

## R3: What the aggregator is given

**Decision**: `IReadOnlyCollection<TrainingActivity>` plus a `DateRange` plus an `int
maximumHeartRate`.

**Rationale**: Day assignment needs each activity's `StartedAt` (FR-004), and the load needs the
maximum heart rate because feature 001 deliberately does not store it on the activity (its FR-015).
Passing activities keeps day assignment in exactly one place. If the aggregator were handed
pre-computed `(DateOnly, TrainingLoad)` pairs instead, the caller would own FR-004 — the single most
consequential rule in this feature — and it could not be tested here.

A consequence worth naming: feature 001's `CalculateTrainingLoad` throws when an activity has
heart-rate data and `maximumHeartRate` is not positive. That exception propagates out of
aggregation unchanged. This is feature 001's rule, not a new one, and no guard is added here to
re-state it (Principle VII).

**Alternatives considered**: pre-computed loads (rejected, above); an athlete profile object
carrying the maximum heart rate (rejected: no such type exists, and inventing one here repeats the
mistake feature 001's data model already rejected under "Types deliberately not created").

---

## R4: How a session's calendar day is determined

**Decision**: `DateOnly.FromDateTime(activity.StartedAt.DateTime)` — equivalently
`activity.StartedAt.Date` — which yields the offset-local calendar date.

**Rationale**: FR-004 requires the athlete's local day at the session's own recorded offset.
`DateTimeOffset.DateTime` returns the clock date-time as recorded with its offset, so this is a
direct expression of the requirement with no arithmetic of our own. Verified on .NET 10: a session
at `2026-03-02T00:30:00+02:00` yields local date `2026-03-02` while its UTC date is `2026-03-01` —
exactly the divergence FR-004 and SC-011 exist to pin down.

`DateOnly` is used for the day key rather than `DateTime` so that no time component exists to be
compared by accident, and so the daily series' element type cannot silently carry a midnight.

**Alternatives considered**: `UtcDateTime.Date` (rejected — it is the behaviour the developer
explicitly ruled out); `TimeZoneInfo.ConvertTime` against a configured zone (rejected — that is the
athlete-level-timezone option the developer rejected, and it would add an input the feature does not
otherwise need).

---

## R5: How ISO-8601 weeks are computed

**Decision**: `System.Globalization.ISOWeek` from the base class library —
`ISOWeek.GetYear(DateTime)`, `ISOWeek.GetWeekOfYear(DateTime)`, and
`ISOWeek.ToDateTime(year, week, DayOfWeek.Monday)`.

**Rationale**: ISO-8601 week numbering has a genuinely fiddly rule at the year boundary, and the
BCL already implements it correctly. Hand-rolling it would be inventing a wheel the platform ships,
and would be the kind of code TDD tests can agree with while both are wrong. Verified on .NET 10:
`2025-12-29` is ISO `2026-W01`, `2027-01-03` is ISO `2026-W53`, and `2027-01-04` opens `2027-W01` —
so 2026 is a 53-week ISO year, which makes it a useful year to write the boundary tests against.

`ISOWeek` takes `DateTime`, not `DateOnly`, on .NET 10 — confirmed, not assumed. Conversion is
`day.ToDateTime(TimeOnly.MinValue)` at the one place that needs it.

**Alternatives considered**: `Calendar.GetWeekOfYear` with
`CalendarWeekRule.FirstFourDayWeek` (rejected: culture-sensitive and only approximately ISO, a
classic source of off-by-one-week bugs); a hand-written Thursday rule (rejected: more code, more
tests, no benefit over a correct platform implementation).

---

## R6: How a week is identified and how weeks are enumerated

**Decision**: an `IsoWeek` value carrying its ISO week-numbering year and week number, with the
Monday and Sunday derivable from it. The weekly series is enumerated by **walking Mondays** — take
the Monday of the first week and add seven days until the last week's Monday is passed — deriving
each `IsoWeek` from its Monday.

**Rationale**: Walking Mondays sidesteps the 52-versus-53-week question entirely. Incrementing a
`(year, week)` pair requires knowing how many weeks that ISO year has, which is exactly the rule
most likely to be got wrong; adding seven days to a date cannot be. The 53-week year 2026 is in the
range this project's test data uses, so this is not a hypothetical concern.

Ordering (FR-010) follows from the Monday date. Ordering by `(year, week)` would give the same
answer, but ordering by the Monday makes it obviously true rather than requiring an argument about
the year boundary.

**Alternatives considered**: `(int Year, int Week)` tuples (rejected: no place to hang the Monday
and Sunday that the tests and FR-013 talk about, and easy to mix up with a calendar year); storing
the Monday alone and deriving the number on read (rejected: the week number is what the athlete and
the later trend feature name a week by, so it belongs in the value).

---

## R7: Whether a date range is its own type

**Decision**: yes — `DateRange`, a **sealed class** with a validating constructor, not a struct.

**Rationale**: FR-019 and FR-020 are refusals that must be impossible to bypass, and both
aggregation methods take a range, so a type keeps the guards in one place instead of duplicating
them at two entry points. The spec names Date Range as an entity with exactly these invariants.

It is a class rather than a `readonly record struct` because `default(SomeStruct)` bypasses every
constructor: a `default(DateRange)` would be an instance with an unbounded start and end that no
guard ever saw, breaking the contract guarantee that an instance is either valid or does not exist.
This is the same reason feature 001 made `HeartRateSeries` — its only other invariant-bearing type —
a sealed class while leaving `HeartRateSample` and `TrainingLoad` as structs. The rule the codebase
now follows is: invariants imply a class.

FR-020's "missing" bound is expressed as `default(DateOnly)` (0001-01-01), following feature 001's
treatment of a missing start time as `default(DateTimeOffset)` (its FR-019). This is a mechanical
default, not a new rule, and it is the only representation of "absent" a non-nullable value type
has.

**Alternatives considered**: two `DateOnly` parameters on each method (rejected: duplicates both
guards, and gives callers no way to pass a range around); a `readonly record struct` (rejected: the
`default` hole, above); nullable `DateOnly?` parameters to represent absence (rejected: it would
make "missing" expressible in the type system and then immediately refused, which is more surface
for the same behaviour).

---

## R8: How the weekly series relates to the daily one

**Decision**: compute the weekly series as **seven-day chunks of a daily series over the extended
range** — the Monday opening the range's first week through the Sunday closing its last.

**Rationale**: This makes two requirements true by construction rather than by testing them into
existence. FR-017 (a week's total equals the sum of its seven days) and SC-012 (every weekly entry
covers exactly seven days) are structural consequences of chunking a contiguous daily series, and
cannot drift apart later. The extension in FR-013 becomes one range calculation at the top of the
method instead of a special case threaded through the grouping.

It also keeps the daily-series rule (FR-009, FR-013's last sentence) honest: the *returned* daily
series is the caller's range, while the weekly series is built over a wider internal one. The two
are different ranges on purpose, and this shape makes that visible in one place.

Strict TDD still drives the increments — this is the shape the REFACTOR step is expected to converge
on, not a licence to write it before a test asks for it. The first GREEN for weekly aggregation will
be whatever the first failing test needs.

**Alternatives considered**: grouping activities directly by `IsoWeek` (rejected: FR-017 and SC-012
then become properties that have to be separately maintained, and a week containing no activities
has to be conjured back into the series by a second mechanism); computing weeks from the returned
daily series (rejected: cannot work, because the extended weeks reach outside it).

---

## R9: Numeric type for totals

**Decision**: `decimal`, summed with no rounding at any step.

**Rationale**: `TrainingLoad.Points` is already `decimal` (feature 001, research R9), chosen so a
reviewer can reproduce a value by hand. FR-022 and SC-004 extend that to totals. Summing `decimal`
is exact for these magnitudes, so a hand-checked total of 30 + 40 + 50 is `120`, not `119.99999`.
Any presentation rounding belongs to a later UI, per the spec's Assumptions.

**Alternatives considered**: `double` (rejected: reintroduces the drift feature 001 chose `decimal`
to avoid, and would make SC-004 untestable by exact equality); an integer points type (rejected:
`TrainingLoad.Points` is not an integer, and truncating at the aggregate would violate FR-022).

---

## R10: Representing how much a total can be trusted

**Decision**: a new `LoadBasis` enum with four members — `None`, `Measured`, `Estimated`, `Mixed` —
carried on each daily and weekly entry.

**Rationale**: FR-014 names exactly four states. `LoadProvenance` from feature 001 describes one
activity's load and has exactly two; widening it would change the meaning of an existing type and
force every 001 consumer to handle states a single activity can never be in. A separate enum for the
aggregate keeps both types honest about what they describe.

`None` exists because a rest day is genuinely different from a day whose sessions all scored zero
(FR-016, spec Assumptions). Ordering the members with `None` first means `default(LoadBasis)` is
`None`, which is the correct basis for the zero-load entry that dominates a sparse series.

**Alternatives considered**: a `bool ContainsEstimates` (rejected: cannot distinguish a rest day
from a fully measured one, and drops the all-estimated case); a proportion of estimated points
(rejected by the spec's Assumptions — a second number needing its own interpretation rules, and
addable later if the trend feature needs it); reusing `LoadProvenance?` with `null` for mixed
(rejected: `null` would have to mean both "mixed" and "no sessions").

---

## R11: Shape of the result values

**Decision**: `DailyTrainingLoad` and `WeeklyTrainingLoad` as `readonly record struct`s, returned as
`IReadOnlyList<T>` in ascending order.

**Rationale**: They are outputs with no invariants of their own to defend — the aggregator is
responsible for their internal consistency, and there is no public constructor path by which a
caller could build a nonsensical one that matters. Structural equality makes the tests read as
whole-value comparisons (`Assert.Equal(new DailyTrainingLoad(...), actual[0])`) instead of four
field assertions each, which keeps the given/when/then scenarios legible. This follows `TrainingLoad`
and `HeartRateSample` from feature 001; the invariant-bearing types in this feature are classes, per
R7.

`IReadOnlyList<T>` rather than `IEnumerable<T>` because FR-010's ordering and the tests' indexing
both want a materialised, positional result, and because a lazy sequence would let the current clock
or a mutated input leak into a value FR-012 requires to be fixed.

---

## R12: An absent or empty activity collection

**Decision**: an empty collection returns the full gap-free zero series (FR-018) and is not an
error. A `null` collection is refused with `ArgumentNullException`.

**Rationale**: FR-018 is explicit about empty. The spec's Edge Cases list "a collection that is
empty, and one that is absent altogether" as two cases, and null is the only way absence is
expressible for a reference parameter; refusing it names the problem (FR-021) rather than silently
treating it as empty, which would hide a caller's bug. `ArgumentNullException` carries the parameter
name mechanically, matching feature 001's contract guarantee C3.

---

## R13: Known boundary — extending the last week past the end of the calendar

**Decision**: accept the framework's exception; add no domain rule.

**Rationale**: FR-013 extends the weekly series forward to the Sunday closing the range's last week.
`DateOnly.MaxValue` is 9999-12-31, a Friday, so a range ending in that final week would extend to a
Sunday that does not exist, and `DateOnly.AddDays` throws `ArgumentOutOfRangeException` — verified,
not assumed. The specification places no upper bound on a range and states no rule for this, so
inventing one here would be exactly the improvisation Principle VII forbids.

This is recorded rather than hidden: a reviewer should know the boundary exists and that the failure
is a framework exception rather than a named refusal. It is not reachable from any realistic
training date.

**Revisit when**: a caller can supply an arbitrary end date from outside the system — the import or
API feature — at which point refusing an implausible range becomes that feature's input-validation
question, and it can be specified there.

---

## R14: Performance and scale

**Decision**: no performance requirement, no optimisation, no benchmark.

**Rationale**: The specification states none, and the realistic input is a few thousand activities
over a gap-free daily series measured in thousands of entries — a decade is 3,653 days. A single
grouping pass plus one walk over the range is comfortably enough. The one shape worth being
deliberate about is that the series is built by walking the range and looking up each day, rather
than by scanning all activities once per day, which would be quadratic for no reason.

---

## R15: What this feature deliberately does not build

| Candidate | Why not | Revisit when |
|-----------|---------|--------------|
| `IActivitySource` or any loading abstraction | Nothing in this feature loads anything; activities arrive as an argument. The project plan sketches this interface for the import feature, where it belongs. | Feature 005 (Strava import). |
| A `TrainingLoadSeries` type wrapping both series | Two method calls returning two lists is the whole feature. A wrapper would exist only to hold them together. | The Fitness/Form feature needs to pass both series around as one thing. |
| Caching or memoisation of totals | FR-012 makes recomputation cheap and deterministic; caching would add invalidation questions to a pure function. | A profiled, measured problem. |
| A `Month` or per-activity-type aggregation | Out of Scope in the spec. | A specification asks for it. |
| Custom exception types | Feature 001 settled this: `ArgumentException` and friends carry `ParamName`, which is what the refusal requirements ask for. | Batch import must distinguish "skip this one" from "the caller passed nonsense". |

---

## Resolved unknowns

No `NEEDS CLARIFICATION` markers remain in the Technical Context. FR-004 and FR-013 were resolved by
the developer during specification and are recorded in the spec, not here.

---

## Sources

- `System.Globalization.ISOWeek` and `DateOnly` behaviour verified directly on the installed SDK
  (.NET 10.0.100) on 2026-09-17, not from memory: the ISO year/week of 2025-12-29, 2026-01-01,
  2027-01-03 and 2027-01-04; that 2026 is a 53-week ISO year; that `ISOWeek` has no `DateOnly`
  overload; that `DateTimeOffset.Date` is offset-local; and that `DateOnly.MaxValue.AddDays(2)`
  throws `ArgumentOutOfRangeException`.
- [Feature 001 research](../001-training-activity-domain/research.md) for the framework, test,
  layout, and numeric decisions inherited unchanged.
- [Constitution v1.0.0](../../.specify/memory/constitution.md), Principles III and VII in particular.
