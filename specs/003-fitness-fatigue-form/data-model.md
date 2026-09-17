# Data Model: Fitness, Fatigue, and Form

**Feature**: 003-fitness-fatigue-form | **Date**: 2026-09-17

**Two** new types in `TrainingLoadAnalyzer.Domain`, alongside the twelve features 001 and 002 created.
Nothing existing changes — this feature is additive, and `DailyTrainingLoad`, `LoadBasis`, and
`DateRange` are consumed exactly as they are.

Semantics below trace to the numbered requirements in [spec.md](./spec.md); the decisions behind the
shapes are in [research.md](./research.md).

> Two details below depend on specification amendments that have not been made yet, and are marked
> **⚠️ pending R10** and **⚠️ pending R11**. Everything else is settled.

---

## DailyTrainingMetrics (readonly record struct)

One calendar day's position: where the athlete stands, and how far the three figures can be trusted.

| Member | Type | Stored? | Meaning |
|--------|------|---------|---------|
| `Day` | `DateOnly` | stored | The calendar day these figures describe |
| `Fitness` | `double` | stored | Chronic training load — a 42-day exponentially weighted average of daily load |
| `Fatigue` | `double` | stored | Acute training load — the same over 7 days |
| `IsReliable` | `bool` | stored | Whether `Day` lies past the 42-day warm-up from the history's first day |
| `FitnessBasis` | `LoadBasis` | stored | How much of the last 42 days' load was measured |
| `FatigueBasis` | `LoadBasis` | stored | The same over the last 7 days |
| `Form` | `double` | **derived** | `Fitness - Fatigue`, computed on every read |
| `FormBasis` | `LoadBasis` | **derived** | `FitnessBasis` — see below ⚠️ pending R10 |

- FR-001, FR-002, FR-003, FR-013, FR-016, FR-019.
- `Form` is a property rather than a constructor parameter, so no instance can exist whose Form
  disagrees with its two components. SC-003 holds by construction, not by test (FR-003, research R7).
- `FormBasis` is likewise derived. Under R10's recommended reading, Form's basis is the combination
  over every day feeding either metric — and because Fatigue's 7-day window is always a strict subset
  of Fitness's 42-day window, that combination is *always* exactly `FitnessBasis`. If the alternative
  reading is chosen instead, this becomes a seventh stored field with its own rule and nothing else
  in this model moves.
- `double`, not `decimal`, for the three figures — the smoothing factor is irrational and FR-029
  states a tolerance rather than exactness (research R2). The daily loads feeding the calculation
  remain `decimal` and are converted at one place inside the calculator.
- `IsReliable` is `false` for "warming up", so `default(DailyTrainingMetrics)` is the cautious value,
  matching `LoadBasis.None` being `default` (feature 002's R10).
- A rest day still produces an entry: figures decayed from the day before, `Basis` of whatever the
  window holds (FR-009).

**Assertion note.** Unlike feature 002's result types, this one must **not** be compared by whole-value
equality in tests: record-struct equality compares `double` exactly, and FR-029 states a tolerance.
Assert `Day`, `IsReliable`, and the bases exactly; assert `Fitness`, `Fatigue`, and `Form` with
`Assert.Equal(expected, actual, tolerance: 0.0001)` (research R7).

---

## TrainingMetricsCalculator (static class)

| Member | Signature |
|--------|-----------|
| `Calculate` | `IReadOnlyList<DailyTrainingMetrics> Calculate(IReadOnlyList<DailyTrainingLoad> history, DateRange range)` |

No `maximumHeartRate` parameter: the loads arrive already calculated, so the value feature 001
deliberately does not own (its FR-015) is not threaded through a third feature (research R5).

### The recurrence

Applied once per day, in ascending order, from the first day of `history`:

```text
fitness = fitness + (load − fitness) × α_fitness      α_fitness = 1 − e^(−1/42) ≈ 0.02352831335
fatigue = fatigue + (load − fatigue) × α_fatigue      α_fatigue = 1 − e^(−1/7)  ≈ 0.1331221002
```

| Rule | Requirement |
|------|-------------|
| Both metrics start at `0` on the notional day before `history[0].Day` | FR-012 |
| `load` is that day's `DailyTrainingLoad.Points`, including the day's own training | FR-006 |
| A day with no training is `load = 0` and is advanced across, not skipped | FR-009 |
| Each day is computed from the day before it — no day without all its predecessors | FR-010 |
| The factors are computed as written, at ≥10 significant figures, never as `1 ÷ N` | FR-005, FR-029a |
| No intermediate figure is rounded | FR-028 |

`Form` is never advanced by this loop; it does not exist as state (FR-003).

### Reliability

| Condition | `IsReliable` | Requirement |
|-----------|--------------|-------------|
| `Day < history[0].Day.AddDays(42)` | `false` | FR-013, FR-014 |
| `Day >= history[0].Day.AddDays(42)` | `true` | FR-013, FR-014 |

The history's first day and the 41 days after it are not yet reliable; the 43rd day onward is. The
figures are always produced and always qualified — never withheld or blanked (FR-015).

### Basis windows

| Metric | Window (inclusive of the day itself) | Requirement |
|--------|--------------------------------------|-------------|
| `FitnessBasis` | the last 42 days | FR-019 |
| `FatigueBasis` | the last 7 days | FR-019 |
| `FormBasis` | derived — see `DailyTrainingMetrics` ⚠️ pending R10 | FR-019 |

Within a window, each day contributes its own `LoadBasis` exactly when that basis is not `None` —
which by feature 002's guarantee C12 is exactly when the day had at least one session (FR-018). A
window reaching back past the start of the history is truncated to the days present; those absent days
contributed no load, so by FR-018 they contribute no basis.

The combination is feature 002's, reused unchanged:

| Contributing days in the window | Basis |
|---------------------------------|-------|
| none | `None` |
| all `Measured` | `Measured` |
| all `Estimated` | `Estimated` |
| at least one of each | `Mixed` |

A single estimated day makes the whole window `Mixed` (FR-017), and `None` means nothing in the window
carried training (FR-020) — distinguishable from a window of real zero-point sessions, which reports
the basis those sessions carried.

### Refusals

| Condition | Result | Requirement |
|-----------|--------|-------------|
| `history` is null | `ArgumentNullException(paramName: "history")` | FR-026 |
| `range` is null | `ArgumentNullException(paramName: "range")` | FR-026 |
| `history` is empty | `ArgumentException(paramName: "history")` | FR-022 |
| `history[0].Day` is later than `range.Start` | `ArgumentException(paramName: "history")` | FR-022 |
| `history[^1].Day` is earlier than `range.End` | `ArgumentException(paramName: "history")` | ⚠️ pending R11 (proposed FR-022a) |
| `history` has a gap, a repeated day, or is out of order | `ArgumentException(paramName: "history")` | FR-023 |
| `range` is unbounded or inverted | `DateRange`'s own `ArgumentException` | FR-021 |

Five refusals share the `ParamName` `"history"`, so feature 001's guarantee C3 is load-bearing here:
each message must name its own rule and the offending day, and a test for one must not pass against
another (FR-026, research R13).

A history where every day carries zero load is **not** a refusal — it produces a full series decaying
toward zero (FR-025).

### Purity

No clock, no storage, no network, no static mutable state, no dependence on anything but the two
arguments (FR-007, FR-027, SC-010). The same history and range yield equal results on every call, on
any date.

---

## Types deliberately *not* created

| Candidate | Why not | Revisit when |
|-----------|---------|--------------|
| `DailyLoadHistory` | One call site, and FR-022's guard is about the history's relation to the range, so it could not live there anyway (research R6). | A second entry point needs the same continuity guard. |
| `FitnessCalculator` / `FatigueCalculator` / `FormCalculator` | Three passes over one history, and Form's derivation would stop being structural (research R4). | Never, on current requirements. |
| `ITrainingMetricsCalculator` | One implementation, nothing to substitute, no seam a test needs. | A second metric model must be chosen at runtime. |
| A `Fitness` / `Fatigue` / `Form` value type each | They are three `double`s on one entry, all on the daily-load points scale. Three wrapper types would add conversion noise and no invariant. | One of them acquires a rule the others do not share. |
| `MetricsReliability` enum | Two states; a boolean says it (research R8). | A third state appears. |
| A `TrainingLoadSeries` holding loads and metrics together | Feature 002 recorded this with *this* feature as its trigger. The trigger did not fire: the calculation takes one series and returns another. | The dashboard needs both as one value. |
| Configurable time constants | FR-004 fixes 42 and 7. | A specification asks for a second model. |

---

## Requirement trace

| Requirement | Realised by |
|-------------|-------------|
| FR-001 | `DailyTrainingMetrics.Fitness`, advanced with α_fitness |
| FR-002 | `DailyTrainingMetrics.Fatigue`, advanced with α_fatigue |
| FR-003 | `Form` as a computed property; no Form state exists in the loop |
| FR-004 | The two time constants, private and fixed |
| FR-005 | `1.0 - Math.Exp(-1.0 / N)`, computed not transcribed (research R3) |
| FR-006 | The day's own `Points` enters that day's step |
| FR-007 | Pure static method; no clock, storage, or ambient state |
| FR-008 | One entry appended per day of `range`, in iteration order |
| FR-009 | A day of `0` points takes the same path as any other day |
| FR-010 | Single forward pass; each step reads the previous step's values |
| FR-011 | Iteration starts at `history[0].Day`, not at `range.Start` |
| FR-012 | Both accumulators initialised to `0` |
| FR-013 | `IsReliable` on every entry |
| FR-014 | `history[0].Day.AddDays(42)` as the boundary |
| FR-015 | Figures are always produced; `IsReliable` qualifies them |
| FR-016 | `FitnessBasis`, `FatigueBasis`, `FormBasis` on every entry |
| FR-017 | Feature 002's combination rule, reused |
| FR-018 | A day contributes iff its `Basis` is not `None` |
| FR-019 | 42-day and 7-day backward windows; Form derived (⚠️ pending R10) |
| FR-020 | Empty window yields `None` |
| FR-021 | `DateRange`'s existing constructor guards |
| FR-022 | Guard on `history[0].Day` against `range.Start` |
| FR-022a ⚠️ | Guard on `history[^1].Day` against `range.End` (proposed, research R11) |
| FR-023 | Contiguity scan over `history` before any calculation |
| FR-024 | Iteration spans the history; output filtered to `range` |
| FR-025 | An all-zero history takes the ordinary path |
| FR-026 | Exception type + `ParamName` + a distinct message per rule |
| FR-027 | No clock read; pure function of two arguments |
| FR-028 | `double` accumulators carried unrounded between days |
| FR-029 | Tolerance of 0.0001 points, asserted as a tolerance in tests |
| FR-029a | `Math.Exp` gives ~17 significant figures, verified (research R3) |
| FR-030 | Type names and the domain `.csproj`'s empty dependency list |
