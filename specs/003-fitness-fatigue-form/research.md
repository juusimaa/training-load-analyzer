# Phase 0 Research: Fitness, Fatigue, and Form

**Feature**: 003-fitness-fatigue-form | **Date**: 2026-09-17

The spec carries no `[NEEDS CLARIFICATION]` markers — the three load-model questions were answered by
the developer during `/speckit-specify` and written into FR-005, FR-012, and FR-014. What remains are
mechanical decisions this plan must settle before tasks can be written, plus **two places where the
specification turned out to be ambiguous or silent** (R10, R11). Under Principle VII those were put to
the developer rather than decided quietly in code; both were answered and the specification amended —
see each entry for the outcome.

Decisions carried over unchanged from
[feature 001's research](../001-training-activity-domain/research.md) and
[feature 002's](../002-training-load-aggregation/research.md) — `net10.0`, xUnit v3 with built-in
`Assert.*`, no assertion library, no mocking library, `src/` + `tests/` layout, `ArgumentException`
family for refusals — are not re-argued here. Only what this feature adds is below.

---

## R1: Which project hosts the calculation

**Decision**: `TrainingLoadAnalyzer.Domain`. No new project.

**Rationale**: Same argument as feature 002's R1, and it has not weakened. This is a pure function
over domain values: it reads `DailyTrainingLoad` and produces new domain values, touching no storage,
no clock, and no network (FR-007). The project plan's section 22.2 records that
`TrainingLoadAnalyzer.Application` is not created until a use case earns it, and this feature has no
use case to orchestrate — it has a function.

**Revisit when**: unchanged from 002 — feature 005 (Strava import) loading activities from storage
and then driving aggregation and metrics in sequence is the likely trigger.

---

## R2: Numeric type for the three metrics

**Decision**: **`double`** for Fitness, Fatigue, and Form. The daily loads entering the calculation
stay `decimal` and are converted at one place.

This is a deliberate departure from features 001 and 002, which are `decimal` throughout, and it is
the most consequential decision in this plan.

**Rationale**: `decimal` was chosen in feature 001 (its R9) for one reason: a reviewer must be able to
reproduce a points total by hand, exactly. FR-005's smoothing factor `1 − e^(−1/N)` is irrational, so
that reason does not survive into this feature — the spec acknowledges as much, replacing exactness
with a stated tolerance of 0.0001 points in FR-029. Once exactness is off the table, `decimal` buys
nothing here and misrepresents an irrational quantity as exact.

The recurrence is also numerically forgiving in a way worth stating precisely, because it is the
reason FR-028 and SC-012 can be met without special care. `new = previous + (load − previous) × α` is
a contraction: any error in `previous` is multiplied by `(1 − α)` on the next step, so error **decays**
rather than accumulating. Measured on the installed SDK over a 3,653-day (10-year) series of varied
daily loads, the largest difference between the `double` calculation and the same recurrence carried
in 28-digit `decimal` was:

| Comparison | Max difference over 10 years |
|------------|------------------------------|
| `double` vs 28-digit `decimal`, same α | 5.7 × 10⁻¹⁴ points |
| `double` with `Math.Exp` α vs 28-digit `decimal` with exact α | 9.9 × 10⁻¹⁴ points |

That is nine orders of magnitude inside FR-029's 0.0001-point tolerance, and it does not grow with
the length of the history. SC-012 is therefore satisfied by the arithmetic itself, not by mitigation.

**Alternatives considered**:

- **`decimal` with α as a high-precision literal constant** — rejected, though it is the closest call
  in this plan. It would keep one numeric type across the whole domain, and `decimal` cannot compute
  `exp`, so α would have to be a transcribed literal that no reviewer can verify by reading it. It
  would buy an exactness the quantity does not have, at roughly ten times the arithmetic cost, to
  produce an answer differing from `double`'s in the fourteenth decimal place.
- **`float`** — rejected: ~7 significant digits would put accumulated representation error within
  sight of the tolerance, for no benefit.
- **Keeping the result in `decimal` while computing in `double`** — rejected: converting back at the
  end would imply a precision the figure does not have, and would invite exact-equality assertions in
  tests that FR-029 says must not be relied on.

**Consequence to carry forward**: tests must not assert these three figures by exact equality. See
R7.

---

## R3: Where the smoothing factors come from

**Decision**: compute them once, in code, as `1.0 - Math.Exp(-1.0 / 42.0)` and `1.0 - Math.Exp(-1.0 /
7.0)`, held as private `static readonly double` fields. Not transcribed literals.

**Rationale**: FR-029a requires the factors at ten or more significant figures. Verified on the
installed SDK (.NET 10.0.100), `Math.Exp` yields:

| Factor | `Math.Exp` result | True value (40-digit) | Error |
|--------|-------------------|------------------------|-------|
| α_fitness | `0.023528313347756735` | `0.02352831334775676990…` | 3.5 × 10⁻¹⁷ |
| α_fatigue | `0.1331221002498184` | `0.13312210024981837249…` | 2.8 × 10⁻¹⁷ |

Both are correct to ~17 significant figures — about one unit in the last place — so FR-029a is met
with seven digits to spare. Writing the formula rather than a literal means a reviewer sees FR-005
directly in the code and there is no transcription to get wrong; the 11th-digit slip made while
drafting this research is the exact failure mode avoided.

`Math.Exp` is not contractually bit-identical across every platform .NET runs on, so this is recorded
rather than assumed. Any such variation is bounded by roughly one ulp, ~10⁻¹⁷ on α, which moves a
metric figure by far less than the 10⁻¹³ already measured — immaterial against a 10⁻⁴ tolerance.
FR-027's determinism is about the clock and external services, and is unaffected.

**Verification**: a test pins both factors to their ten-significant-figure values through observable
behaviour rather than by exposing them — one day of load 100 from a zero seed yields Fitness
`2.352831335` and Fatigue `13.31221002`, which are α × 100.

---

## R4: The shape of the entry point

**Decision**: a `static class TrainingMetricsCalculator` with one public method, `Calculate`. No
interface, no instance, no injected collaborator.

**Rationale**: Identical reasoning to feature 002's R2, and the codebase now has a precedent to match
rather than a choice to make. The computation is a pure function of its arguments; there is no state
for an instance to hold and no seam a test needs.

One method rather than three matters here. The project plan's section 8 sketches `FitnessCalculator`,
`FatigueCalculator`, and `FormCalculator` as separate services. Three classes would each walk the same
history, recomputing the same day sequence, and Form would then have to reach into the other two —
turning FR-003's "Form is derived, never accumulated" from a structural fact into a convention. Both
metrics advance over one pass, and Form falls out of them, so one pass and one class is what the
requirements actually describe.

**Alternatives considered**: three calculators per the project plan sketch (rejected, above — and the
plan explicitly says not all of those types should automatically be implemented); an instance class
holding the time constants as configuration (rejected: FR-004 fixes them, and configurability would
need a valid range, a default, and a rule for changing mid-history, none of which the MVP needs);
extension methods on `IReadOnlyList<DailyTrainingLoad>` (rejected for feature 002's reasons).

**Revisit when**: a second metric model must coexist and a caller must choose between them at
runtime, or the calculation acquires a dependency a test needs to control.

---

## R5: What the calculator is given

**Decision**: `Calculate(IReadOnlyList<DailyTrainingLoad> history, DateRange range)`.

**Rationale**: The history is feature 002's `AggregateDaily` output, consumed as-is. This is what the
spec's Assumptions describe, and it has a pleasant consequence: no `maximumHeartRate` parameter is
needed. The loads are already calculated, so the value feature 001 deliberately refuses to own does
not have to be threaded through a third feature.

`IReadOnlyList<T>` rather than feature 002's `IReadOnlyCollection<T>` because this feature is
**positional**: FR-023 refuses a history that is out of order or has a gap, and FR-010 requires each
day to be computed from the one before it. Both need indexing. An `IEnumerable<T>` would additionally
allow a lazily-generated sequence whose contents could differ between two enumerations, which FR-027
forbids.

`DateRange` is reused unchanged from feature 002. Its constructor already enforces FR-021's two
refusals (unbounded and inverted ranges) — this feature adds nothing to it and needs nothing from it
that it does not already have.

---

## R6: Whether the history is its own type

**Decision**: no. `IReadOnlyList<DailyTrainingLoad>` is passed directly; the continuity guards live in
the calculator.

**Rationale**: This is the one place where feature 002's `DateRange` reasoning (its R7, "invariants
imply a class") pulls toward creating a `DailyLoadHistory`, and the spec does name Daily Load History
as a Key Entity. It is rejected on two grounds.

First, there is one call site, not two. `DateRange` earned its existence because both aggregation
methods needed the same guards; one entry point does not have that problem.

Second, and more decisive: a history type could only enforce its *internal* invariant (continuous,
ascending, no duplicates — FR-023). The other refusal, FR-022, is about the history's relationship to
the requested range, and cannot live in either type alone. Splitting the refusals across two places
would mean two error-reporting styles for one concept, when FR-026 wants each refusal to name its
rule clearly. Keeping all four guards at one entry point is simpler and easier to test.

**Alternatives considered**: a `sealed class DailyLoadHistory` validating in its constructor
(rejected, above); making `AggregateDaily` return such a type (rejected: it would change feature 002's
contract to serve this feature's convenience, and C8 already guarantees the series is gap-free —
every history this codebase produces is valid by construction, and the guards exist for histories
assembled by hand or by a future importer).

**Revisit when**: a second entry point needs the same continuity guard — the dashboard feature asking
for "today's metrics only" without the full series is the plausible trigger — or a caller needs to
pass a validated history around rather than straight into one call.

---

## R7: The shape of the result

**Decision**: `DailyTrainingMetrics`, a `readonly record struct`, returned as
`IReadOnlyList<DailyTrainingMetrics>` in ascending date order. `Form` and `FormBasis` are **computed
properties**, not stored fields.

**Rationale**: Follows feature 002's R11 for the same reasons — an output value with no invariants of
its own, whose consistency is the calculator's responsibility.

Making `Form` a property (`Fitness - Fatigue`) rather than a constructor parameter is what makes
FR-003 structural: there is no way to construct an instance whose Form disagrees with its two
components, so SC-003 holds by construction rather than by test. The same applies to `FormBasis` under
R10's recommended reading.

**One consequence must be recorded**, because it departs from feature 002's testing style. That
feature's whole-value assertions (`Assert.Equal(new DailyTrainingLoad(...), actual[0])`) work because
`decimal` equality is exact. Here, `Fitness` and `Fatigue` are `double` and FR-029 explicitly states a
tolerance, so record-struct equality — which compares doubles exactly — must not be used to assert
them. Tests assert `Day`, `IsReliable`, and the bases exactly, and the two figures with
`Assert.Equal(expected, actual, tolerance: 0.0001)`. Verified available in the installed
`xunit.v3.assert` 4.0.1.

---

## R8: Representing whether a figure is yet reliable

**Decision**: a `bool IsReliable` on each entry.

**Rationale**: FR-013 names exactly two states. An enum would be the right call at three (which is why
`LoadBasis` is one), but a two-state enum is ceremony around a boolean. Declared so that `false` means
"not yet reliable", which makes `default(DailyTrainingMetrics)` the cautious value — the same
reasoning that put `None` first in `LoadBasis` (feature 002's R10).

The boundary is computed as `day >= history[0].Day.AddDays(42)`, per FR-014: the first day of the
history and the 41 days after it are not yet reliable, the 43rd day onward is.

**Alternatives considered**: `MetricsReliability { WarmingUp, Reliable }` (rejected: no third state
exists or is foreseen); a `DaysOfHistory` count for the caller to threshold itself (rejected: it
would move a rule the spec fixes into every caller); omitting or nulling the figures during warm-up
(rejected outright — FR-015 requires them produced and qualified, not withheld).

---

## R9: Computing the basis windows

**Decision**: for each day, scan backward over the bounded window — 42 days for Fitness, 7 for
Fatigue, each including the day itself — and combine the bases of the days that contributed load. A
day contributes exactly when its own `Basis` is not `None`. No rolling-window state, no optimisation.

**Rationale**: FR-019 fixes the window lengths. FR-018 fixes what contributes: a day that carried no
training contributes nothing, while a day whose sessions totalled exactly zero points does contribute,
because it carries a real measurement or a real estimate. Feature 002's guarantee C12 makes that test
a single comparison — `Basis == None` if and only if `ActivityCount == 0` — so no new concept is
needed to express it.

Where the window would reach back past the start of the supplied history, it is truncated to the days
actually present. Those earlier days contributed no load, so by FR-018 they contribute no basis; this
is a consequence of the stated rules rather than a new one.

The combination rule is feature 002's, unchanged and reused: any measured plus any estimated is
`Mixed`, all of one kind is that kind, nothing is `None`.

**Cost**: O(n × 49) comparisons — about 179,000 for a 10-year series, on values already in memory. The
rolling-counter version would be O(n) but needs two counters maintained in step with a sliding index,
which is more to get wrong for a saving nobody asked for. Feature 002's R14 applies unchanged: the
specification states no performance requirement.

**Revisit when**: a profiled, measured problem appears.

---

## R10: ✅ RESOLVED — Form's basis when Fatigue's window is empty

**Status**: resolved 2026-09-17. The developer chose reading A; the specification was amended,
adding **FR-019a** and **FR-019b** and an acceptance scenario (User Story 3, scenario 6).

**The problem**: FR-019's last sentence reads "Form's basis MUST combine the two, being measured only
when both are measured and mixed when they disagree or when either is mixed." Taken literally, a day
whose Fitness basis is `Measured` and whose Fatigue basis is `None` — an athlete who trained with a
heart-rate monitor three weeks ago and has rested since — has two bases that *disagree*, and so would
report Form as `Mixed`. Nothing was mixed. The figure rests entirely on measured work.

The literal reading also contradicts FR-018's principle, which is that a day contributing no load
contributes nothing to a basis, and sits awkwardly against FR-020, which reserves `None` for the case
where nothing in the window carried training.

**Chosen reading (A)**, now FR-019a: treat `None` as contributing nothing, exactly as FR-018 treats a
rest day. Form's basis is the combination of the bases of every day that fed **either** metric.

**Why it was worth stating in the spec rather than just implementing**: reading A has a consequence the
spec did not anticipate, now written into FR-019b. Fatigue's 7-day window is a strict subset of Fitness's 42-day window — both
end on the same day and both truncate at the history's start — so the union is always exactly
Fitness's window. **Under reading A, Form's basis always equals Fitness's basis**, on every day,
necessarily. That makes it a derived property rather than a third computed value, which is how
[data-model.md](./data-model.md) models it. A reader of the spec should be told that, rather than
discovering it in the code.

**The alternative (B)** is the literal text: `Measured` only when both are `Measured`, `Mixed` on any
disagreement. It makes Form's basis an independent value, and reports `Mixed` for the rested athlete
above. It is defensible only if "mixed" is read as "these two figures rest on different evidence",
which is not what `LoadBasis` means anywhere else in the codebase.

**Had B been chosen**: `FormBasis` would have become a stored field with its own combination rule and
one extra test case per disagreement pairing. It was not.

---

## R11: ✅ RESOLVED — a history that stops before the range's last day

**Status**: resolved 2026-09-17. The developer chose to refuse; the specification was amended, adding
**FR-022a**.

**The problem**: the spec's Edge Cases list "a history that stops before the requested range's last
day, leaving days at the end with no load to consume", but no requirement resolves it. FR-022 refuses
a history that starts too late; there is no mirror for one that ends too early. FR-008 requires an
entry for every day in the range, which such a history cannot supply.

**Decision**: **refuse**, naming the shortfall, as FR-022 does at the other end. The spec's own
Assumptions already settle the principle for the analogous case — "A gap in the supplied history is
refused rather than filled with zeroes… this feature cannot tell a genuinely absent day from an
aggregation defect" — and a missing tail is a gap at the end. Silently treating the missing days as
rest would invent training history and produce a decaying curve that looks real.

Added as **FR-022a**: *The system MUST refuse a request whose supplied history ends before the last day
of the requested range, and the refusal MUST identify the shortfall.*

**Alternative**: truncate the result to the days the history covers. Rejected — it would silently
return fewer entries than the caller's range asked for, which is the one thing FR-008 and SC-001 are
written to prevent.

---

## R12: Where the calculation starts and what it emits

**Decision**: seed both metrics at `0`, iterate from `history[0].Day` through `range.End`, and add an
entry to the result only for days inside `range`.

**Rationale**: FR-012 fixes the seed. FR-011 requires the metrics to be built forward from the
history's first day so the range's first day already carries the effect of everything before it, and
FR-024 requires days before the range to be used but not reported. Iterating the history and filtering
the output is the direct expression of both; there is no second pass and no separate warm-up phase.

Days of the history after `range.End` are simply not reached, which satisfies the other half of
FR-024 at no cost.

---

## R13: Refusals

**Decision**: the `ArgumentException` family with accurate `ParamName`, as in features 001 and 002. No
custom exception type.

| Condition | Refusal | Requirement |
|-----------|---------|-------------|
| `history` is null | `ArgumentNullException(paramName: "history")` | FR-026 |
| `range` is null | `ArgumentNullException(paramName: "range")` | FR-026 |
| `history` is empty | `ArgumentException(paramName: "history")` | FR-022 |
| `history` starts after `range.Start` | `ArgumentException(paramName: "history")` | FR-022 |
| `history` ends before `range.End` | `ArgumentException(paramName: "history")` | FR-022a |
| `history` has a gap, duplicate, or is out of order | `ArgumentException(paramName: "history")` | FR-023 |

Five refusals share the `ParamName` `"history"`, which makes feature 001's contract guarantee C3 —
distinct messages, so a test for one cannot pass against another — load-bearing rather than
decorative. Each message names the offending day where there is one: the first day of the history and
the range's start for FR-022, the position and dates of the break for FR-023.

An empty history is refused via FR-022 rather than as a separate rule, because an empty history
trivially fails to reach back to the range's first day. The message must say so in those terms rather
than reporting a nonexistent date.

---

## R14: What this feature deliberately does not build

| Candidate | Why not | Revisit when |
|-----------|---------|--------------|
| `DailyLoadHistory` type | One call site; the guards cannot all live in it anyway (R6). | A second entry point needs the same continuity guard. |
| Separate `FitnessCalculator` / `FatigueCalculator` / `FormCalculator` | Three passes over one history, and Form's derivation would stop being structural (R4). | Never, on current requirements. |
| `ITrainingMetricsCalculator` | One implementation, nothing to substitute — the case Principle III calls a defect. | A second metric model must be chosen at runtime. |
| Configurable time constants | FR-004 fixes 42 and 7. Configurability needs a valid range, a default, and a mid-history change rule. | A specification asks for a second athlete profile or a different model. |
| A `TrainingLoadSeries` wrapping daily loads and metrics together | Feature 002 recorded this with "the Fitness/Form feature needs to pass both series around as one thing" as the trigger. It turned out not to: this feature consumes the daily series alone and returns its own. | The dashboard needs loads and metrics as one value. |
| Exposing the smoothing factors publicly | Nothing outside the calculator needs them; a test pins them through behaviour (R3). | A second consumer must apply the same decay. |
| Caching metrics between calls | FR-007 makes recomputation pure and cheap; caching would add invalidation to a pure function. | A profiled, measured problem. |
| "Latest metrics only" convenience overload | Nothing needs it yet; `Calculate(...)[^1]` is available to any caller. | The dashboard is built and the full series is genuinely unwanted. |

---

## R15: Performance and scale

**Decision**: no performance requirement, no optimisation, no benchmark. One pass over the history for
the recurrence, plus a bounded backward scan per day for the bases (R9).

**Rationale**: Unchanged from feature 002's R14. The realistic input is a few years of daily entries;
a decade is 3,653. Both the recurrence and the basis scan are linear in that with a small constant.

---

## Resolved and unresolved

FR-005, FR-012, and FR-014 were resolved by the developer during `/speckit-specify`. No
`NEEDS CLARIFICATION` markers remain in the Technical Context.

Two specification gaps were found while designing — **R10** (Form's basis when the Fatigue window is
empty) and **R11** (a history ending before the range does). Neither was settled in code: both were
put to the developer, both were answered on 2026-09-17, and the specification was amended with
FR-019a, FR-019b, and FR-022a before tasks were generated. Nothing in this plan is now pending.

---

## Sources

- Arithmetic behaviour verified directly on the installed SDK (.NET 10.0.100) on 2026-09-17, not from
  memory: `Math.Exp`-derived values of both smoothing factors and their error against a 40-digit
  reference; the maximum divergence between the `double` recurrence and the same recurrence in
  28-digit `decimal` over a 3,653-day series; that the recurrence settles to within 0.02% of a
  sustained load after 365 days (SC-004); that Fitness reaches 63.21%, 86.47%, and 95.02% of a
  sustained load after 42, 84, and 126 days (the figures behind FR-014's warm-up choice); and that
  `Assert.Equal(double, double, tolerance:)` exists in `xunit.v3.assert` 4.0.1.
- The 40-digit reference values of `1 − e^(−1/42)` and `1 − e^(−1/7)` were computed independently of
  .NET, at 40-digit precision, for the comparison above.
- [Feature 001 research](../001-training-activity-domain/research.md) and
  [feature 002 research](../002-training-load-aggregation/research.md) for the framework, test,
  layout, refusal, and (now departed-from) numeric decisions.
- [Constitution v1.0.0](../../.specify/memory/constitution.md), Principles III and VII in particular.
