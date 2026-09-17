# Phase 0 Research: Load Trends

**Feature**: 004-load-trends | **Date**: 2026-09-17 | **Plan**: [plan.md](./plan.md)

Decisions taken before any code is written, each with its rationale, the alternatives weighed, and —
where the decision could reasonably be revisited — the trigger that should reopen it.

Two items were put to the developer rather than settled here, under Principle VII and Principle III
respectively. Both were answered on 2026-09-17 and are recorded at **R11** and **R12**.

The shorthand below: *history* is the supplied run of `WeeklyTrainingLoad`, *range* is the requested
`DateRange`, and *the entry* is one `WeeklyLoadTrend` in the result.

---

## R1: Which project hosts the calculation

**Decision**: `TrainingLoadAnalyzer.Domain`, the assembly features 001, 002, and 003 already build.
No new project, no new package reference, no change to any `.csproj`.

**Rationale**: This is pure domain logic over two in-memory arguments. It has no use case to
orchestrate, no port to isolate, and no caller outside the domain — the three conditions the project
plan's section 22.2 sets for creating `TrainingLoadAnalyzer.Application`. None has fired, and this
feature does not move any of them closer.

**Alternatives considered**: a separate `TrainingLoadAnalyzer.Analysis` project, on the grounds that
"analysis" is conceptually distinct from "aggregation". Rejected: an assembly boundary that isolates
nothing buys nothing, and the project plan's own section 7 warns against treating its sketch as a
template. The domain project holds fourteen types today and is nowhere near unwieldy.

**Revisit trigger**: the domain assembly growing past the point where a reviewer can hold it in mind,
or a genuine need to ship analysis separately from the model. Neither is near.

---

## R2: Numeric type for the two changes

**Decision**: `decimal` for both the absolute change and the relative change — the same type feature
002's `WeeklyTrainingLoad.Points` already uses. This feature does **not** repeat feature 003's
departure to `double`.

**Rationale**: FR-033 requires the absolute change to be reproducible exactly by subtracting the two
weekly totals, with no tolerance allowed. That is a stronger requirement than feature 003's FR-029,
which had to concede a tolerance of 0.0001 because its smoothing factors are irrational. Nothing here
is irrational: a subtraction and a division of two decimals.

Measured, on realistic TRIMP totals (zone weight × minutes, so non-integer totals are the norm rather
than the exception):

| previous | current | `double` subtraction | `decimal` subtraction | exact? |
|---------:|--------:|---------------------:|----------------------:|:------:|
| 505.7 | 610.4 | `104.69999999999999` | `104.7` | ❌ |
| 482.25 | 553.5 | `71.25` | `71.25` | ✅ |
| 1017.3 | 864.9 | `-152.39999999999998` | `-152.4` | ❌ |
| 233.1 | 268.05 | `34.95000000000002` | `34.95` | ❌ |

Three of four realistic cases fail FR-033 under `double`. That settles it on its own.

A second, independent reason: FR-004 forbids representing an absent relative change as an infinity.
`double` division by zero yields `Infinity` **silently**, so the forbidden representation is what the
language hands you by default and the guard is easy to forget. `decimal` division by zero throws
`DivideByZeroException`, so the guard cannot be omitted without a test going red. The type that makes
the specified behaviour the path of least resistance is the right one.

**A claim that did not survive measurement.** The design initially argued that `double` would also
break FR-011 — significance at *exactly* the threshold — because the literal `0.15` is not
representable in binary and rounds to `0.1499999999999999944…`. Checked across all 184 pairs of whole
point totals where the change is exactly +15% and clears the 50-point floor, **zero** failed a
`>= 0.15` test in `double`: the division rounds to the same double as the literal. The argument is
recorded as rejected rather than quietly dropped, because a reviewer is likely to reach for it too.
`decimal` still represents `0.15` exactly, which is a real if secondary benefit — the threshold
comparison needs no reasoning about rounding at all.

**Alternatives considered**: `double` throughout, for symmetry with feature 003 — rejected above, and
the symmetry is false anyway, since 003's choice was forced by a constraint this feature does not
share. `double` for the relative change only, keeping `decimal` for the absolute — rejected: it
reintroduces the silent `Infinity` for no gain, and a type that changes between two members of the
same record invites exactly the confusion the split is meant to avoid.

**Revisit trigger**: none foreseeable. It would take the specification giving up FR-033's exactness.

---

## R3: Representing an absent relative change

**Decision**: `decimal?`, null exactly when the preceding week's points are zero.

**Rationale**: FR-004 requires the absence to be readable *as* an absence, and forbids zero, an
infinity, a sentinel, and omitting the entry. `Nullable<decimal>` is the language's own way to say
"this value may not exist", it is what a reader expects, and — with R2's choice of `decimal` — the
alternative encodings are not merely discouraged but unreachable: there is no decimal infinity and no
decimal NaN to accidentally produce.

**Alternatives considered**: a separate `bool HasRelativeChange` beside a non-nullable `decimal` —
rejected, because it permits the two members to disagree and puts the burden on every caller to check
the flag first. A `RelativeChange` of `decimal.Zero` with the absence inferred from
`PreviousPoints == 0` — rejected outright by FR-004, and it would make an idle week look identical to
an unchanged one.

---

## R4: The shape of the entry point

**Decision**: one `public static class TrainingLoadTrendCalculator` with one method:

```csharp
public static IReadOnlyList<WeeklyLoadTrend> Calculate(
    IReadOnlyList<WeeklyTrainingLoad> history,
    DateRange range);
```

**Rationale**: it mirrors `TrainingMetricsCalculator.Calculate` exactly, which in turn mirrors
`TrainingLoadAggregator`. A reader who has seen one has seen all three. The calculation has no state
between calls and no collaborator, so an instance would carry nothing.

The name follows the existing `TrainingLoad*` prefix, so the three calculation entry points sort
together and read as a family.

**Alternatives considered**: the project plan's section 8 sketches both `WeeklyLoadAnalyzer` and
`LoadSpikeDetector` as separate services. Rejected — two types for one pass over one list is the
speculative structure Principle III exists to prevent, and FR-029 forbids a separate detection pass
outright. An instance class taking thresholds in its constructor — rejected under R7. An extension
method on `IReadOnlyList<WeeklyTrainingLoad>` — rejected: it would attach this feature's vocabulary
to a general collection type and make the entry point harder to find.

---

## R5: What the calculator is given

**Decision**: the weekly history and a `DateRange` of calendar days, reusing feature 002's type
unchanged.

**Rationale**: FR-016 defines completeness by whether all seven of a week's days fall inside the
requested range, so the range must be expressed in days. A hypothetical week-granular range could not
express the question at all. Reusing `DateRange` also makes FR-026 free: its constructor already
refuses an inverted range and an unbounded one, with the messages feature 002 tested (001 C3, 002's
`DateRangeTests`).

**Alternatives considered**: taking a first and last `IsoWeek` — rejected, it makes every week
complete by construction and deletes User Story 3's third scenario. Taking the activities and
aggregating internally — rejected, it duplicates feature 002 and is forbidden by FR-006.

---

## R6: Which members are stored and which are derived

**Decision**: the entry stores five values — the week, its points, the preceding week's points,
whether it is complete, and the basis — and **derives** the other three: the absolute change, the
relative change, and the classification.

**Rationale**: this is the single most load-bearing decision in the design, and it is feature 003's
`Form` lesson applied twice over. A derived member cannot disagree with what it is derived from,
which turns four requirements from things an implementation must remember into things it cannot get
wrong:

| Requirement | How derivation enforces it |
|---|---|
| FR-002, FR-033 — absolute change is the exact difference | It *is* `Points - PreviousPoints`; there is no second place to store a stale value |
| FR-003, FR-004 — relative change, absent when the divisor is zero | One expression, one guard, evaluated on read |
| FR-007 – FR-009 — exactly one classification, from the two changes | Derived from the same two expressions, so a hand-set classification is unrepresentable |
| FR-017 — every partial week is indeterminate | The derivation reads `IsComplete` first and returns before any threshold is consulted |

The specification says so in as many words: the Trend Classification entity is "derived from the two
changes and the week's completeness on every occasion it is read, never accumulated or stored
independently of them." Storing it would make FR-017 a rule to be maintained rather than a fact.

**Alternatives considered**: storing all eight members in the constructor — rejected, it makes every
invariant above a convention, and a record struct's primary constructor would let a caller build an
entry whose classification contradicts its changes. Computing the changes in the calculator and
passing them in — the same objection, one step removed.

**Consequence for testing**: noted here because it shapes the tasks — a test cannot construct a
`WeeklyLoadTrend` with a contradictory classification in order to assert that it is rejected, because
the type makes it unconstructable. That is the design working, not a gap in coverage.

---

## R7: Where the thresholds live

**Decision**: two `private const decimal` fields on the entry type — `0.15m` and `50m` — reachable by
the derivation of R6 and by nothing else.

**Rationale**: FR-010 fixes both values in the specification and offers no way to vary them. A
constructor parameter, a settings object, or an options record would each be an abstraction with one
possible value, which Principle III treats as a defect. They live on the entry type rather than on
the calculator because that is where the classification is derived.

**The tautology hazard, recorded because it is the likeliest way these tests go wrong**: if the
constants were public and the tests asserted against them, the tests would assert that the code
agrees with itself and would keep passing if both the constant and the behaviour changed together.
Tests **must** use the literals `0.15` and `50` — or, better, concrete point totals either side of
them, as the spec's acceptance scenarios already do. This is the reason to keep the constants
private: it removes the temptation rather than relying on discipline.

**Alternatives considered**: public constants, so the dashboard can one day explain *why* a week was
flagged — rejected for now under Principle III, and recorded as the revisit trigger below.

**Revisit trigger**: feature 006 needing to render the threshold in an explanation, or a second
caller wanting a different one. The first would make the constants public; the second is a
specification amendment, not an implementation change.

---

## R8: Completeness is a property of the range, not of the data

**Decision**: a week is complete exactly when `week.Monday >= range.Start && week.Sunday <=
range.End`. Nothing about the weekly total itself is consulted.

**Rationale, and the objection a reviewer will raise**: feature 002's `AggregateWeekly` extends its
range out to whole ISO weeks before totalling, so **every** `WeeklyTrainingLoad` covers seven days of
data. It follows that a week this feature marks "partial" may carry a perfectly complete seven-day
total — and FR-017 then suppresses a classification that would have been valid.

That is real, and it is accepted deliberately, because the two cases cannot be told apart from the
inputs:

- **The in-progress week.** The athlete asks for trends up to today, a Wednesday. The week's total
  covers Monday to Sunday, but Thursday onward have not happened and contribute zero. The total is
  genuinely short and the comparison genuinely misleading. Marking it partial is correct.
- **The historical week cut by the range.** The athlete asks about a window ending last March. The
  days past the range's end are in the past and did carry training, which the total includes. The
  comparison would have been sound.

Distinguishing them needs either the clock — forbidden by FR-005 and FR-031, and the reason feature
003's figures are reproducible on any date — or the daily series behind each week, which is not
supplied and which FR-006 forbids re-deriving. Given inputs that cannot answer the question, the
conservative answer is the only honest one: never present a comparison that *might* be against a
short week as significant.

The cost is bounded and the caller controls it entirely. Asking for a range that starts on a Monday
and ends on a Sunday makes every week complete, which is the natural way to ask the question anyway.

**Alternatives considered**: comparing `ActivityCount` against some expected number — rejected,
there is no such expectation and a genuine rest week has zero. Pro-rating a partial week up to seven
days — rejected explicitly by FR-019: scaling three days of training into a notional week invents
load the athlete did not do, and would turn a light Monday into a fictional ramp-up.

---

## R9: Locating the range's weeks inside the history

**Decision**: compute `IsoWeek.For(range.Start)` and `IsoWeek.For(range.End)`, find the first by its
`Monday` in the history, and walk forward to the last. Ordering comparisons use `Monday`, never
`(Year, Week)`.

**Rationale**: `IsoWeek` is a record struct with no ordering of its own, so an ordering has to be
chosen. `Monday` is a `DateOnly` and totally ordered by construction. `(Year, Week)` would also work
but invites the ISO year-boundary bug the type's own remarks warn about — 2025-12-29 belongs to
2026-W01, and 2026 is a 53-week year — and there is no reason to take that risk when an unambiguous
key is already on the type.

Because FR-024 requires the history to be continuous, the first week's index also fixes every later
one; no searching per entry is needed.

**Alternatives considered**: a dictionary keyed by `IsoWeek` — rejected, it is a data structure
bought to solve a problem a validated contiguous list does not have, and it would silently tolerate
the gaps FR-024 exists to refuse.

---

## R10: Validating that the history is continuous

**Decision**: one check over consecutive pairs — `history[i].Week.Monday` must equal
`history[i - 1].Week.Monday.AddDays(7)` — refusing on the first violation.

**Rationale**: a gap, a repeated week, and an inversion are the same violation seen from three sides,
and one comparison catches all three. This is feature 003's continuity check with `AddDays(1)`
changed to `AddDays(7)`, deliberately so: the same shape of input error gets the same shape of
message, and a reader of one recognises the other.

Refused rather than repaired, for the reason FR-024 gives: filling a gap with a zero week would
silently compare a week against the wrong predecessor and manufacture a significant decrease out of
missing data. This is exactly the failure mode the developer rejected at R11, arriving from a
different direction.

---

## R11: ✅ RESOLVED — a history that stops before the range's last week

**The gap**: FR-022 refused a history that starts too late and FR-024 refused a gap in the middle,
but nothing covered a history that simply stops early, while FR-028 demanded one entry for every week
touching the range. The two could not both be satisfied and the specification did not say which gave
way.

This is structurally identical to the gap feature 003 hit at its own R11, which is itself the
argument for planning as a phase: it is invisible while writing requirements and unmissable while
designing against them.

**Put to the developer** on 2026-09-17 with three options — refuse; truncate the series; zero-fill
the missing weeks — and **answered: refuse, naming the shortfall.**

**Rationale as accepted**: zero-filling is the dangerous option, and specifically so in this feature.
A fabricated zero week is indistinguishable from a genuine rest week, so the very first thing the
feature would do with it is classify it as a significant decrease and tell the athlete their training
collapsed in a week for which there is simply no data. Truncating is safer but silently returns fewer
entries than were asked for, contradicting FR-028 and SC-002 and pushing the caller into checking the
result's length against their own range.

**Written into the specification** as **FR-022b**, with User Story 1 scenario 6, a new edge case, and
SC-008 widened to name both ends of the history. Done before this plan was finalised; nothing here is
provisional.

---

## R12: ✅ RESOLVED — combining two bases, on its third occurrence

**The question**: the rule that measured and estimated together make mixed, and that `None`
contributes nothing, now appears in `TrainingLoadAggregator` (002), in
`TrainingMetricsCalculator.BasisOver` (003), and is needed again here. Rule of three says extract;
Principle III says do not abstract without a concrete need; and extracting means editing two
completed, signed-off features.

**Put to the developer** on 2026-09-17 and **answered: keep a local copy in feature 004, record the
extraction trigger.**

**Rationale as accepted**: the three sites are less alike than the shared switch statement makes them
look. Feature 002 folds over the activities of a day and then over the days of a week; feature 003
folds over a bounded 42- or 7-day window; this feature combines exactly two values. Only the final
`(anyMeasured, anyEstimated)` switch is common, and a helper that captures just that leaves each call
site still doing its own accumulation — a small, awkward abstraction rather than a clean one. Against
that, refactoring two green features widens this feature's diff into code that has already been
reviewed and merged, for no behavioural gain.

**Revisit trigger**: a fourth occurrence, or feature 006 needing the same combination for display. At
that point the right extraction is probably `LoadBasis Combine(LoadBasis, LoadBasis)` used as a fold
seed, which would serve all four sites; it is not worth doing for three, two of which are already
written and tested.

**Recorded as the one Principle III judgement call in this feature**, so that the completion review
looks at it rather than discovering it.

---

## R13: Refusals

**Decision**: five refusals, each naming its own rule, three of them sharing the `ParamName`
`"history"`.

| # | Condition | Exception | Requirement |
|---|-----------|-----------|-------------|
| 1 | `history` or `range` is null | `ArgumentNullException` | 001 C3 convention |
| 2 | `history` is empty | `ArgumentException("history")` | FR-022 |
| 3 | `history` does not reach the week before the range's first week | `ArgumentException("history")` | FR-022 |
| 4 | `history` ends before the range's last week | `ArgumentException("history")` | FR-022b |
| 5 | `history` has a gap, a repeat, or is out of order | `ArgumentException("history")` | FR-024 |

The inverted and unbounded range (FR-026) is refused by `DateRange`'s own constructor before this
feature sees it, and is not re-checked — a second check would be a second place for the message to
drift.

**Ordering matters**: the empty check must precede anything that indexes the history, and the
reach-back check must precede the continuity scan, so that the most specific diagnosis wins rather
than the first one the loop happens to trip over. Feature 003 ordered its guards for the same reason.

**The messages must differ**, because refusals 2 through 5 share a `ParamName` and a test asserting
on `ParamName` alone would pass against the wrong one. Each message names the offending week and what
was expected. This is guarantee C28's requirement in feature 003, restated here because the same trap
is present.

---

## R14: What this feature deliberately does not build

Recorded so the completion review can check that none of it appeared:

| Not built | Why not | Revisit trigger |
|-----------|---------|-----------------|
| A separate "significant weeks" list or detector | FR-029 forbids it outright; a caller filters the series | Never — it is a requirement, not a preference |
| Configurable thresholds | FR-010 fixes both values; one possible value is not a parameter | A specification amendment, not an implementation change |
| A `WeeklyLoadHistory` wrapper type | Feature 002 rejected the daily equivalent and feature 003 re-rejected it when the trigger came round; the same reasoning holds | A third consumer needing the same validation |
| Trends over fitness, fatigue, or form | Out of scope by the spec's Assumptions; feature 003's series already describes the athlete's state | A specification saying so |
| Daily or monthly trends | MVP item 11 and plan section 15 both frame this week over week | A specification saying so |
| Explanatory prose from a run of trends | The classification is a label, not a sentence; commentary belongs to feature 006 | Feature 006 |
| A shared basis-combination helper | R12, answered by the developer | A fourth occurrence |

---

## R15: Performance and scale

**Decision**: no performance work, no benchmark, no caching.

One pass over the history to validate continuity, one pass over the weeks touching the range to emit
entries; both O(n) in the number of weeks, with the derived members costing one subtraction, one
division, and three comparisons each. Ten years of history is 522 weeks, so a full-history call is
roughly a thousand iterations of arithmetic on stack-allocated structs.

For comparison, feature 003's basis windows cost about 179,000 comparisons over the same span and
needed no attention either. There is nothing here to measure.

---

## Resolved and unresolved

| Item | Status |
|---|---|
| R1 – R10, R13 – R15 | Decided here, with rationale and revisit triggers |
| R11 — history stopping short of the range | ✅ Put to the developer, answered, spec amended (FR-022b) |
| R12 — basis combination on its third occurrence | ✅ Put to the developer, answered, trigger recorded |

Nothing is outstanding. No `NEEDS CLARIFICATION` marker remains anywhere in the specification or in
this document, and no decision has been left for implementation to make.

---

## Sources

- [spec.md](./spec.md) — the numbered requirements every decision above traces to
- [constitution.md](../../.specify/memory/constitution.md) v1.0.0 — Principles I, III, and VII did
  the work in this feature
- [002 research](../002-training-load-aggregation/research.md) — R1 on the application layer, R8 on
  folding a week from its own days
- [003 research](../003-fitness-fatigue-form/research.md) — R2 on numeric type, R11 on the same
  shortfall gap arriving one feature earlier, R13 on refusal ordering
- [training-load-analyzer-plan.md](../../training-load-analyzer-plan.md) — section 15 (feature scope),
  section 22.2 (the application layer)
- Numeric behaviour in R2 measured directly against IEEE-754 binary64 and decimal arithmetic on
  2026-09-17; both tables are reproducible from the values shown
