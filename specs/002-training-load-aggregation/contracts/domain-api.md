# Contract: TrainingLoadAnalyzer.Domain public API — aggregation additions

**Feature**: 002-training-load-aggregation | **Date**: 2026-09-17

This library's contract is its public type surface; there is no HTTP endpoint, CLI, or wire format
in this feature. Below is only what feature 002 **adds**. Feature 001's surface is unchanged and is
documented in [its contract](../../001-training-activity-domain/contracts/domain-api.md); its
guarantees C1–C7 continue to hold.

Signatures are shown without bodies; semantics are in [data-model.md](../data-model.md) and the
numbered requirements in [spec.md](../spec.md).

Namespace: `TrainingLoadAnalyzer.Domain`

```csharp
public enum LoadBasis
{
    None,       // no session contributed — default
    Measured,
    Estimated,
    Mixed,
}

public sealed class DateRange
{
    /// <exception cref="ArgumentException">
    ///   start is default(DateOnly) (FR-020);
    ///   end is default(DateOnly) (FR-020);
    ///   end is earlier than start (FR-019).
    /// </exception>
    public DateRange(DateOnly start, DateOnly end);

    public DateOnly Start { get; }
    public DateOnly End { get; }

    /// <summary>Every day from Start to End inclusive, ascending (FR-005).</summary>
    public IEnumerable<DateOnly> Days { get; }
}

public readonly record struct IsoWeek
{
    /// <summary>The ISO-8601 week containing the given day (FR-002).</summary>
    public static IsoWeek For(DateOnly day);

    /// <summary>ISO week-numbering year — not necessarily the calendar year of its days.</summary>
    public int Year { get; }

    /// <summary>ISO week number, 1-53.</summary>
    public int Week { get; }

    public DateOnly Monday { get; }
    public DateOnly Sunday { get; }
}

public readonly record struct DailyTrainingLoad(
    DateOnly Day,
    decimal Points,
    int ActivityCount,
    LoadBasis Basis);

public readonly record struct WeeklyTrainingLoad(
    IsoWeek Week,
    decimal Points,
    int ActivityCount,
    LoadBasis Basis);

public static class TrainingLoadAggregator
{
    /// <summary>
    ///   One entry per calendar day in range, ascending, including days with no
    ///   activity (FR-001, FR-005, FR-007). Activities outside range are excluded (FR-009).
    /// </summary>
    /// <exception cref="ArgumentNullException">activities or range is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Propagated from TrainingActivity.CalculateTrainingLoad when an activity has
    ///   heart-rate data and maximumHeartRate is not positive (001 FR-016).
    /// </exception>
    public static IReadOnlyList<DailyTrainingLoad> AggregateDaily(
        IReadOnlyCollection<TrainingActivity> activities,
        DateRange range,
        int maximumHeartRate);

    /// <summary>
    ///   One entry per ISO week touched by range, ascending, each covering all seven
    ///   of its days — including days outside range (FR-002, FR-006, FR-013).
    /// </summary>
    /// <exception cref="ArgumentNullException">activities or range is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   As AggregateDaily; also if range ends in the final ISO week of year 9999,
    ///   whose closing Sunday exceeds DateOnly.MaxValue (research R13).
    /// </exception>
    public static IReadOnlyList<WeeklyTrainingLoad> AggregateWeekly(
        IReadOnlyCollection<TrainingActivity> activities,
        DateRange range,
        int maximumHeartRate);
}
```

## Contract guarantees

| # | Guarantee | Requirement |
|---|-----------|-------------|
| C8 | `AggregateDaily` returns exactly one entry per calendar day in the range, ascending, with no gaps and no duplicates — for every valid range, including a one-day range. | FR-005, FR-010, SC-001 |
| C9 | Every `WeeklyTrainingLoad` covers all seven days of its ISO week, Monday through Sunday, for every range — including ranges shorter than a week and ranges beginning or ending mid-week. No weekly entry is ever partial. | FR-013, SC-012 |
| C10 | For any ISO week lying wholly inside the range, its `Points` equals the sum of the `Points` of its seven `DailyTrainingLoad` entries. For an extended edge week, the difference equals exactly the load of the extended days outside the range, and nothing else. | FR-017, SC-003 |
| C11 | An activity is attributed to `DateOnly.FromDateTime(StartedAt.DateTime)` — its offset-local day — and to no other day. No athlete-level timezone is consulted and `UtcDateTime` is never read. | FR-003, FR-004, SC-011 |
| C12 | `Basis` is `None` if and only if `ActivityCount` is zero; `Mixed` whenever at least one measured and one estimated load contributed. A total is never obtainable without both `Basis` and `ActivityCount`. | FR-014, FR-015, FR-016, SC-007 |
| C13 | Both methods are pure: the same activities, range, and maximum heart rate yield equal results on every call, in any supplied order, on any date. Neither reads a clock, storage, the environment, or static mutable state. | FR-011, FR-012, FR-024, SC-005 |
| C14 | `Points` totals are exact `decimal` sums of the contributing `TrainingLoad.Points`, with no rounding or truncation at any step. | FR-022, SC-004 |
| C15 | Two activities sharing an `ExternalId` both contribute. No identifier comparison exists in the aggregation. | FR-023 |
| C16 | An empty `activities` collection returns the full gap-free zero series, never an empty list or an exception. | FR-018, SC-002 |
| C17 | A `DateRange` instance is either valid or does not exist: there is no `default`, no setter, and no `with`. | FR-019, FR-020, 001 C1–C2 |
| C18 | The domain assembly still has zero package and project references, and no new type, member, parameter, or comment names Strava or any other provider. | FR-025, SC-010 |

## Stability

This surface is internal to the solution and has no external consumers. It is expected to change as
later features arrive: the Fitness/Fatigue/Form feature will consume the daily series and may want
both series from one call, and the trend feature will compare weekly entries pairwise. Neither is
designed for here (Principle III). This contract describes what feature 002 delivers, not a frozen
public API.
