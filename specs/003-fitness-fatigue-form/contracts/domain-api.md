# Contract: TrainingLoadAnalyzer.Domain public API — fitness, fatigue, and form additions

**Feature**: 003-fitness-fatigue-form | **Date**: 2026-09-17

This library's contract is its public type surface; there is no HTTP endpoint, CLI, or wire format in
this feature. Below is only what feature 003 **adds**. Features 001 and 002 are unchanged and are
documented in their own contracts
([001](../../001-training-activity-domain/contracts/domain-api.md),
[002](../../002-training-load-aggregation/contracts/domain-api.md)); guarantees C1–C18 continue to
hold.

Signatures are shown without bodies; semantics are in [data-model.md](../data-model.md) and the
numbered requirements in [spec.md](../spec.md).

Namespace: `TrainingLoadAnalyzer.Domain`

```csharp
public readonly record struct DailyTrainingMetrics(
    DateOnly Day,
    double Fitness,
    double Fatigue,
    bool IsReliable,
    LoadBasis FitnessBasis,
    LoadBasis FatigueBasis)
{
    /// <summary>Fitness minus Fatigue, derived on every read and never accumulated (FR-003).</summary>
    public double Form { get; }

    /// <summary>
    ///   The basis of Form. Equal to FitnessBasis, because the 7-day Fatigue window is always a
    ///   subset of the 42-day Fitness window (FR-019, research R10).
    /// </summary>
    public LoadBasis FormBasis { get; }
}

public static class TrainingMetricsCalculator
{
    /// <summary>
    ///   One entry per calendar day in range, ascending, with no gaps (FR-008). Metrics are built
    ///   forward from history's first day, seeded at zero (FR-011, FR-012); days of history before
    ///   range contribute but are not returned (FR-024).
    /// </summary>
    /// <exception cref="ArgumentNullException">history or range is null.</exception>
    /// <exception cref="ArgumentException">
    ///   history is empty, or starts after range.Start (FR-022);
    ///   history ends before range.End (proposed FR-022a, research R11);
    ///   history has a gap, a repeated day, or is not in ascending date order (FR-023).
    /// </exception>
    public static IReadOnlyList<DailyTrainingMetrics> Calculate(
        IReadOnlyList<DailyTrainingLoad> history,
        DateRange range);
}
```

## Contract guarantees

| # | Guarantee | Requirement |
|---|-----------|-------------|
| C19 | `Calculate` returns exactly one entry per calendar day in `range`, ascending, with no gaps and no duplicates — for every valid history and range, including a one-day range. | FR-008, SC-001 |
| C20 | `Form` equals `Fitness - Fatigue` on every entry, with zero discrepancy, because it is computed on read and has no storage of its own. | FR-003, SC-003 |
| C21 | Each day's `Fitness` and `Fatigue` equal the previous day's values advanced by that day's load under FR-005's recurrence, to within 0.0001 points. The first day of the history advances from a seed of exactly `0`. | FR-005, FR-010, FR-012, FR-029, SC-002 |
| C22 | A day carrying no training produces an entry and decays the metrics across it. A rest day and a day whose sessions totalled exactly zero points yield identical `Fitness` and `Fatigue`, differing only in their bases. | FR-009, SC-006 |
| C23 | For a history beginning on day D, every entry from D to D+41 has `IsReliable` false and every entry from D+42 onward has it true. Figures are produced for both — never withheld, blanked, or refused. | FR-013, FR-014, FR-015, SC-007 |
| C24 | Every entry carries a basis for each of the three figures. A basis is `None` exactly when no day in that metric's window carried training, and `Mixed` whenever that window holds at least one measured and one estimated day. A figure is not obtainable without its basis and its `IsReliable`. | FR-016 – FR-020, SC-008, SC-009 |
| C25 | A day's basis depends only on days inside that metric's own window: a day that was estimated stops affecting the basis once it falls more than 42 days back for Fitness, or 7 days back for Fatigue. | FR-019, SC-009 |
| C26 | `Calculate` is pure: the same history and range yield equal results on every call, on any date. It reads no clock, no storage, no environment, and no static mutable state. | FR-007, FR-027, SC-010 |
| C27 | No intermediate figure is rounded; over a 10-year history the divergence from the same recurrence carried at 28-digit precision stays within 0.0001 points. The smoothing factors are applied at no fewer than ten significant figures. | FR-028, FR-029, FR-029a, SC-012 |
| C28 | Every refusal names its own rule in its message, with the offending day where there is one. Five refusals share the `ParamName` `"history"`, so their messages must tell them apart — a test for one must not pass against another. | FR-026, 001 C3 |
| C29 | A history in which every day carries zero load returns a full series decaying toward zero, never an empty result or an exception. | FR-025 |
| C30 | The domain assembly still has zero package and project references, and no new type, member, parameter, or comment names Strava or any other provider. | FR-030, SC-013 |

## Departures from the existing surface

Two, both deliberate and both traceable to FR-005's irrational smoothing factor:

1. **`double`, not `decimal`.** Every numeric value in features 001 and 002 is `decimal`, chosen so a
   total can be reproduced by hand exactly. These three figures cannot be reproduced exactly by
   anyone, so FR-029 states a tolerance instead and the type follows (research R2). The
   `DailyTrainingLoad.Points` values entering the calculation are still `decimal`.
2. **Whole-value equality is not the assertion style.** Feature 002's result types are compared with
   `Assert.Equal(expected, actual)`; record-struct equality compares `double` exactly, which FR-029
   says not to rely on. Tests assert `Day`, `IsReliable`, and the bases exactly and the figures with a
   tolerance (research R7). The type is still a record struct — for deconstruction, `ToString`, and
   the exactly-comparable members — but callers should not compare two instances whole.

## Stability

This surface is internal to the solution and has no external consumers. It is expected to change as
later features arrive: the trend feature (004) compares loads week against week and may or may not
want these figures, and the dashboard (006) will read the last entry of the series and plot the rest.
Neither is designed for here (Principle III). This contract describes what feature 003 delivers, not a
frozen public API.

Two elements above depend on specification amendments not yet made — `FormBasis` being derived
(research R10) and the refusal of a history ending before the range (research R11, proposed FR-022a).
Both are marked at their point of use.
