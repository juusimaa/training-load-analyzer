# Data Model: Load Trends

**Feature**: 004-load-trends | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

Two new types and one static calculator, all in namespace `TrainingLoadAnalyzer.Domain`. Nothing
existing is modified.

The governing decision is [research R6](./research.md#r6-which-members-are-stored-and-which-are-derived):
the entry **stores five values and derives three**. Everything below follows from that.

---

## TrendClassification (enum)

The judgement attached to one week's comparison (FR-007).

| Member | Meaning | Requirement |
|--------|---------|-------------|
| `Indeterminate` | The week is partial within the requested range, so the comparison is not like for like and no judgement is offered. | FR-017 |
| `Steady` | A complete week whose change did not clear both thresholds, including a change of exactly zero. | FR-009 |
| `SignificantIncrease` | A complete week whose upward change cleared both thresholds. | FR-008 |
| `SignificantDecrease` | A complete week whose downward change cleared both thresholds. | FR-008 |

`Indeterminate` is deliberately first, so it is `default`. An uninitialised classification then reads
as "no judgement available", which is the safe reading — the same reasoning that put `None` first in
feature 002's `LoadBasis`.

Exactly one member applies to any entry, by construction: the derivation of R6 is a single expression
with one result.

---

## WeeklyLoadTrend (readonly record struct)

One ISO week's comparison against the week before it (FR-030).

### Stored

| Member | Type | Meaning | Requirement |
|--------|------|---------|-------------|
| `Week` | `IsoWeek` | The week being reported on. Feature 002's type, unchanged. | FR-028 |
| `Points` | `decimal` | That week's own total training load, exactly as feature 002 produced it. | FR-006, FR-030 |
| `PreviousPoints` | `decimal` | The preceding ISO week's total, so the entry can be read without consulting the weekly series alongside it. | FR-030 |
| `IsComplete` | `bool` | Whether all seven of the week's days fall inside the requested range. | FR-015, FR-016 |
| `Basis` | `LoadBasis` | How far the comparison can be trusted. Feature 002's type, unchanged. | FR-020, FR-021 |

### Derived

| Member | Type | Definition | Requirement |
|--------|------|------------|-------------|
| `AbsoluteChange` | `decimal` | `Points - PreviousPoints` | FR-002, FR-033 |
| `RelativeChange` | `decimal?` | `PreviousPoints == 0 ? null : AbsoluteChange / PreviousPoints` | FR-003, FR-004 |
| `Classification` | `TrendClassification` | The rule below | FR-007 – FR-009, FR-013, FR-014, FR-017 |

None of the three has storage of its own, so none can disagree with what it comes from. This is
feature 003's `Form` treatment (its FR-003 and guarantee C20), applied to three members instead of
one.

### The classification rule

Evaluated on every read, in this order:

```text
1.  not IsComplete                        -> Indeterminate        (FR-017)
2.  |AbsoluteChange| < 50                 -> Steady               (FR-009, FR-010)
3.  RelativeChange is null                -> the absolute floor alone decided it,
                                             so fall through on sign              (FR-013, FR-014)
4.  |RelativeChange| < 0.15               -> Steady               (FR-009, FR-010)
5.  AbsoluteChange > 0                    -> SignificantIncrease  (FR-008)
6.  otherwise                             -> SignificantDecrease  (FR-008)
```

Three things about this order are load-bearing:

- **Completeness is checked first**, so no threshold is ever consulted for a partial week. FR-017 is
  therefore unreachable-by-construction rather than a rule to be maintained.
- **The absolute floor is checked before the relative test**, which is what makes FR-013 fall out
  rather than needing a branch of its own: when there is no relative change to test, step 2 has
  already applied the only test that exists, and steps 5 and 6 read the sign. A week of 0 followed by
  400 points reaches step 5 and is a significant increase; a week of 0 followed by 30 points stops at
  step 2 and is steady (FR-014).
- **Both comparisons are `<`, not `<=`**, so a change sitting exactly on a threshold passes through
  to significance. FR-011 says exactly that, and stating it as the negative test is the form least
  likely to be written the wrong way round.

Magnitudes are compared, not signed values, so an increase and a decrease of the same size are
treated alike.

### Validation

None. Every stored member is a value the calculator has already established, and the type is
constructed nowhere else in production code. Adding guards to a record struct that only one method
builds would be ceremony: the calculator's own refusals (below) are where invalid input is stopped.

A record struct cannot prevent a *test* from constructing an entry directly, and tests will — that is
how User Story 2's threshold cases are most cheaply expressed, without building a history at all.
What a test cannot do is construct an entry whose classification contradicts its points, because
there is no classification to pass in (research R6).

---

## TrainingLoadTrendCalculator (static class)

One public method (research R4):

```csharp
public static IReadOnlyList<WeeklyLoadTrend> Calculate(
    IReadOnlyList<WeeklyTrainingLoad> history,
    DateRange range);
```

### What it does

1. Refuse bad input (below).
2. Find the first ISO week touching `range` — `IsoWeek.For(range.Start)` — and locate it in
   `history` by its `Monday` (research R9).
3. Walk forward to `IsoWeek.For(range.End)`, emitting one entry per week, each comparing
   `history[i]` against `history[i - 1]`.
4. Return the entries, ascending, one per week touching the range (FR-028).

Weeks of history before or after the range contribute nothing but the one preceding comparison and
are not returned (FR-023).

### Completeness

```text
IsComplete  ==  week.Monday >= range.Start  &&  week.Sunday <= range.End
```

Determined from the range alone, never from the totals (FR-016). The reasoning, and the case it
knowingly treats conservatively, are in [research R8](./research.md#r8-completeness-is-a-property-of-the-range-not-of-the-data).

### Basis

The two weeks' bases are combined by feature 002's rule, with `None` contributing nothing (FR-021):

| previous \ current | `None` | `Measured` | `Estimated` | `Mixed` |
|---|---|---|---|---|
| **`None`** | `None` | `Measured` | `Estimated` | `Mixed` |
| **`Measured`** | `Measured` | `Measured` | `Mixed` | `Mixed` |
| **`Estimated`** | `Estimated` | `Mixed` | `Estimated` | `Mixed` |
| **`Mixed`** | `Mixed` | `Mixed` | `Mixed` | `Mixed` |

The table is symmetric, which is worth asserting in a test: the order of the two weeks must not
change the answer. Implemented as a private method local to this calculator — the third copy of this
rule in the solution, a decision taken by the developer and recorded at
[research R12](./research.md#r12--resolved--combining-two-bases-on-its-third-occurrence).

### Refusals

Five, in this order (research R13). The order is part of the behaviour: a later check must not
pre-empt the diagnosis a more specific earlier one would have given.

| # | Condition | Exception | Message names | Requirement |
|---|-----------|-----------|---------------|-------------|
| 1 | `history` or `range` is null | `ArgumentNullException` | the parameter | 001 C3 |
| 2 | `history` is empty | `ArgumentException("history")` | the range's first week | FR-022 |
| 3 | `history` starts on or after the range's first week | `ArgumentException("history")` | the week that should precede it | FR-022 |
| 4 | `history` ends before the range's last week | `ArgumentException("history")` | the shortfall | FR-022b |
| 5 | `history` is not contiguous ascending | `ArgumentException("history")` | the offending index and both weeks | FR-024 |

Refusals 2–5 share a `ParamName`, so each message must differ enough that a test for one fails
against the others (feature 003's C28, same trap).

An inverted or unbounded range is refused by `DateRange`'s constructor before this method runs and is
not re-checked (FR-026).

### Purity

No clock, no storage, no environment, no static mutable state — the two `const` thresholds are
compile-time values. The same arguments yield equal results on every call and on any date (FR-005,
FR-031).

---

## Types deliberately *not* created

Each is a type a reader might expect, with the reason it is absent and the trigger that would create
it. Full reasoning in [research R14](./research.md#r14-what-this-feature-deliberately-does-not-build).

| Not created | Why | Trigger |
|-------------|-----|---------|
| `WeeklyLoadHistory` | Feature 002 rejected the daily equivalent; feature 003 re-rejected it when its trigger came round. A validated `IReadOnlyList` needs no wrapper. | A third consumer needing the same validation |
| `LoadSpikeDetector` / `WeeklyLoadAnalyzer` | The project plan sketches both, but FR-029 forbids a separate detection pass and one list needs one pass. | Never — a requirement, not a preference |
| `TrendThresholds` / an options record | FR-010 fixes both values. An abstraction with one possible value is a defect under Principle III. | A specification amendment |
| `LoadBases.Combine` shared helper | Third occurrence, three different accumulation shapes. | A fourth occurrence, or feature 006 |
| A `RelativeChange` value type | `decimal?` already says "may not exist" in the language's own vocabulary. | Never foreseen |

---

## Requirement trace

Every requirement in the specification, and where it is discharged.

| Requirement | Discharged by |
|---|---|
| FR-001 | `Calculate` compares `history[i]` against `history[i - 1]` only |
| FR-002, FR-033 | `AbsoluteChange`, derived as a `decimal` subtraction |
| FR-003 | `RelativeChange`, derived as a `decimal` division |
| FR-004 | `RelativeChange` is `decimal?`; `decimal` has no infinity and division by zero throws (research R2) |
| FR-005, FR-031 | Purity, above |
| FR-006 | `Points` and `PreviousPoints` copied from the supplied totals, never recomputed |
| FR-007 | `TrendClassification`, four members |
| FR-008, FR-009 | Classification rule, steps 2, 4, 5, 6 |
| FR-010 | Two `private const decimal` on `WeeklyLoadTrend` (research R7) |
| FR-011 | Both threshold comparisons are `<`, so equality passes through |
| FR-012 | The rule reads the derived members, which are never rounded |
| FR-013, FR-014 | Classification rule, step 2 before step 3 |
| FR-015, FR-016 | `IsComplete`, from the range alone |
| FR-017 | Classification rule, step 1 |
| FR-018 | The changes are derived members and are produced regardless of `IsComplete` |
| FR-019 | `PreviousPoints` is the preceding total as supplied; nothing is scaled |
| FR-020, FR-021 | `Basis` and the combination table |
| FR-022 | Refusals 2 and 3 |
| FR-022b | Refusal 4 |
| FR-023 | Emission is bounded to the weeks touching the range |
| FR-024 | Refusal 5 |
| FR-025 | Enforced indirectly: an absent week is a gap, which refusal 5 catches |
| FR-026 | `DateRange`'s constructor (feature 002) |
| FR-027 | Refusals 2–5 each name their own rule and offending week |
| FR-028 | Step 3 of `Calculate`, walking weeks rather than history entries |
| FR-029 | One method, one result type; no second entry point exists to filter |
| FR-030 | The five stored and three derived members |
| FR-032 | No rounding is applied anywhere; `decimal` carries the values as given |
| FR-034 | No new type, member, or parameter names a provider |
