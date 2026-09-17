# Contract: TrainingLoadAnalyzer.Domain public API — load trend additions

**Feature**: 004-load-trends | **Date**: 2026-09-17

This library's contract is its public type surface; there is no HTTP endpoint, CLI, or wire format in
this feature. Below is only what feature 004 **adds**. Features 001, 002, and 003 are unchanged and
are documented in their own contracts
([001](../../001-training-activity-domain/contracts/domain-api.md),
[002](../../002-training-load-aggregation/contracts/domain-api.md),
[003](../../003-fitness-fatigue-form/contracts/domain-api.md)); guarantees C1–C30 continue to hold.

Signatures are shown without bodies; semantics are in [data-model.md](../data-model.md) and the
numbered requirements in [spec.md](../spec.md).

Namespace: `TrainingLoadAnalyzer.Domain`

```csharp
public enum TrendClassification
{
    /// <summary>The week is partial within the range, so no judgement is offered (FR-017).</summary>
    Indeterminate,

    /// <summary>A complete week whose change did not clear both thresholds (FR-009).</summary>
    Steady,

    /// <summary>A complete week whose rise cleared both thresholds (FR-008).</summary>
    SignificantIncrease,

    /// <summary>A complete week whose fall cleared both thresholds (FR-008).</summary>
    SignificantDecrease,
}

public readonly record struct WeeklyLoadTrend(
    IsoWeek Week,
    decimal Points,
    decimal PreviousPoints,
    bool IsComplete,
    LoadBasis Basis)
{
    /// <summary>This week's points minus the preceding week's, exact (FR-002, FR-033).</summary>
    public decimal AbsoluteChange { get; }

    /// <summary>
    ///   That change as a proportion of the preceding week. Null — never zero, never an
    ///   infinity — when the preceding week carried no load (FR-003, FR-004).
    /// </summary>
    public decimal? RelativeChange { get; }

    /// <summary>
    ///   Derived from the two changes and IsComplete on every read, never stored, so it cannot
    ///   contradict them (FR-007 – FR-009, FR-013, FR-014, FR-017).
    /// </summary>
    public TrendClassification Classification { get; }
}

public static class TrainingLoadTrendCalculator
{
    /// <summary>
    ///   One entry per ISO week touching range, ascending, with no gaps (FR-028). Each week is
    ///   compared against the week immediately before it, which must be present in history even
    ///   when it falls outside range (FR-022); weeks of history outside range are not returned
    ///   (FR-023).
    /// </summary>
    /// <exception cref="ArgumentNullException">history or range is null.</exception>
    /// <exception cref="ArgumentException">
    ///   history is empty, or does not reach the week before range's first week (FR-022);
    ///   history ends before range's last week (FR-022b);
    ///   history has a gap, a repeated week, or is not in ascending week order (FR-024).
    /// </exception>
    public static IReadOnlyList<WeeklyLoadTrend> Calculate(
        IReadOnlyList<WeeklyTrainingLoad> history,
        DateRange range);
}
```

## Contract guarantees

| # | Guarantee | Requirement |
|---|-----------|-------------|
| C31 | `Calculate` returns exactly one entry per ISO week touching `range`, ascending, with no gaps and no duplicates — for every valid history and range, including a range lying inside a single week. | FR-028, SC-002 |
| C32 | `AbsoluteChange` equals `Points - PreviousPoints` on every entry, reproducible by hand **exactly**, with no tolerance. It is computed on read and has no storage of its own. | FR-002, FR-032, FR-033, SC-003 |
| C33 | `RelativeChange` equals `AbsoluteChange / PreviousPoints` when `PreviousPoints` is non-zero, and is `null` exactly when `PreviousPoints` is zero. It is never `0` in place of absent, and no decimal infinity or NaN exists for it to become. | FR-003, FR-004, SC-004 |
| C34 | `Classification` is `SignificantIncrease` or `SignificantDecrease` only when the week is complete **and** `\|AbsoluteChange\| >= 50` **and** (`RelativeChange` is null or `\|RelativeChange\| >= 0.15`). Every other complete week is `Steady`. | FR-007 – FR-010, FR-013, SC-005 |
| C35 | A change sitting exactly on either threshold is significant: 400 → 460 points (exactly +0.15) and 200 → 250 points (exactly +50) both classify as `SignificantIncrease`, while 400 → 459 and 200 → 249 are `Steady`. | FR-011, FR-012 |
| C36 | A week following a week of zero points is `SignificantIncrease` when it carries at least 50 points and `Steady` when it carries fewer, with `RelativeChange` null in both cases. | FR-013, FR-014 |
| C37 | Every entry whose week is not wholly inside `range` has `IsComplete` false and `Classification` `Indeterminate`, and still reports both changes. No partial week is ever `SignificantDecrease`. | FR-015 – FR-018, SC-006 |
| C38 | `IsComplete` depends only on `Week` and `range`, never on the totals, so the same week and range give the same answer on any date and for any history. | FR-016, FR-031 |
| C39 | `PreviousPoints` is the preceding week's total exactly as supplied. Neither week is scaled, pro-rated, or extrapolated, whatever `IsComplete` says. | FR-006, FR-019 |
| C40 | `Basis` combines the two compared weeks' bases: `Mixed` when one is measured and the other estimated, `None` only when neither week carried training, and a `None` week contributes nothing — so `None` + `Measured` is `Measured`. The combination is symmetric in its two arguments. | FR-020, FR-021, SC-007 |
| C41 | Every refusal names its own rule in its message, with the offending week where there is one. Four refusals share the `ParamName` `"history"`, so their messages must tell them apart — a test for one must not pass against another. | FR-027, SC-008, 001 C3 |
| C42 | No partial result accompanies a refusal: `Calculate` either returns a complete series for the whole range or throws. | FR-022, FR-022b, FR-024, SC-008 |
| C43 | `Calculate` is pure: the same history and range yield equal results on every call, on any date. It reads no clock, no storage, no environment, and no static mutable state. | FR-005, FR-031, SC-009, SC-010 |
| C44 | A history in which every week carries zero points returns a full series of `Steady` entries with null relative changes, never an empty result and never an exception. | FR-004, FR-009 |
| C45 | The domain assembly still has zero package and project references, and no new type, member, parameter, or comment names Strava or any other provider. | FR-034, SC-010 |

## Notes on the surface

**Three derived members, no stored ones for them.** `AbsoluteChange`, `RelativeChange`, and
`Classification` are computed properties. A caller cannot construct a `WeeklyLoadTrend` whose
classification disagrees with its points, because there is no classification to pass in. This is
feature 003's `Form` and `FormBasis` treatment (C20, C24a) applied more widely, and it is what makes
C32, C34, and C37 structural rather than behavioural — see [research R6](../research.md#r6-which-members-are-stored-and-which-are-derived).

**`decimal`, not `double`.** Feature 003 departed to `double` because its smoothing factors are
irrational and exact reproduction was unachievable. Nothing here is irrational, C32 demands exactness,
and `double` fails it on ordinary TRIMP totals — `610.4 - 505.7` yields `104.69999999999999`. The
measurements are in [research R2](../research.md#r2-numeric-type-for-the-two-changes). This feature
therefore returns to the `decimal` exactness of features 001 and 002, and whole-value equality is the
right assertion style again, unlike in feature 003.

**Thresholds are not on the surface.** `0.15` and `50` are private constants. They are deliberately
not exposed, so that a test asserting on them is impossible to write — such a test would assert only
that the code agrees with itself ([research R7](../research.md#r7-where-the-thresholds-live)).

## Stability

This surface is internal to the solution and has no external consumers. It is expected to change as
later features arrive: the dashboard (006) will read the most recent entry to show a trend indicator
and may want the thresholds made public in order to explain a classification. That is not designed
for here (Principle III).

One element above began as a specification gap found during planning — a history stopping before the
range's last week, now FR-022b and guarantee C42. It was put to the developer and resolved on
2026-09-17 before this contract was finalised; nothing here is provisional
([research R11](../research.md#r11--resolved--a-history-that-stops-before-the-ranges-last-week)).
