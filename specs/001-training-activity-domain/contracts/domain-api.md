# Contract: TrainingLoadAnalyzer.Domain public API

**Feature**: 001-training-activity-domain | **Date**: 2026-09-16

This library's contract is its public type surface — there is no HTTP endpoint, CLI, or wire format
in this feature. Signatures are shown without bodies; semantics are in
[data-model.md](../data-model.md) and the numbered requirements in [spec.md](../spec.md).

Namespace: `TrainingLoadAnalyzer.Domain`

```csharp
public enum ActivityType
{
    Running,
    Cycling,
}

public enum LoadProvenance
{
    Measured,
    Estimated,
}

public readonly record struct HeartRateSample(TimeSpan TimeFromStart, int Bpm);

public readonly record struct TrainingLoad(decimal Points, LoadProvenance Provenance);

public static class HeartRateZone
{
    /// <summary>Edwards weight 0-5 for a heart rate, per FR-009.</summary>
    public static int WeightFor(int bpm, int maximumHeartRate);
}

public sealed class HeartRateSeries
{
    /// <exception cref="ArgumentException">
    ///   samples is null, empty, or not in ascending time order (FR-021).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   any sample's Bpm falls outside 20-250 inclusive (FR-021).
    /// </exception>
    public HeartRateSeries(IReadOnlyList<HeartRateSample> samples);

    public IReadOnlyList<HeartRateSample> Samples { get; }

    /// <summary>Edwards TRIMP points for this series, per FR-009 and FR-010.</summary>
    public decimal TrimpPoints(int maximumHeartRate);
}

public sealed class TrainingActivity
{
    /// <exception cref="ArgumentException">
    ///   externalId is null, empty, or whitespace (FR-020);
    ///   startedAt is default(DateTimeOffset) (FR-019).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   movingTime is zero or negative (FR-017);
    ///   activityType is not a defined ActivityType value (FR-018).
    /// </exception>
    public TrainingActivity(
        string externalId,
        DateTimeOffset startedAt,
        TimeSpan movingTime,
        ActivityType activityType,
        HeartRateSeries? heartRate = null);

    public string ExternalId { get; }
    public DateTimeOffset StartedAt { get; }
    public TimeSpan MovingTime { get; }
    public ActivityType Type { get; }
    public HeartRateSeries? HeartRate { get; }

    /// <summary>
    ///   Measured load from the heart-rate series when one is present (FR-009),
    ///   otherwise an estimate from moving time (FR-013). Always marked (FR-014).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   maximumHeartRate is zero or negative AND this activity has heart-rate data (FR-016).
    ///   Not thrown for an activity without heart-rate data, which never reads the value.
    /// </exception>
    public TrainingLoad CalculateTrainingLoad(int maximumHeartRate);
}
```

## Contract guarantees

| # | Guarantee | Requirement |
|---|-----------|-------------|
| C1 | No public setter, `init` accessor, or `with` expression exists on any type. An instance never changes after construction. | FR-024 |
| C2 | Every constructor either returns a fully valid instance or throws. No partially-constructed instance is observable. | FR-023 |
| C3 | Every throw carries a `ParamName` identifying the offending input. No guard throws a bare `Exception` or `InvalidOperationException`. | FR-022 |
| C4 | `CalculateTrainingLoad` is pure: same receiver and same argument yield an equal `TrainingLoad`, with no reads of `DateTime.Now`, environment, or static mutable state. | FR-011, SC-003 |
| C5 | `TrainingLoad.Points` is never negative, and the provenance cannot be obtained separately from the number. | FR-008, SC-007 |
| C6 | `ActivityType` is never read by any load calculation, so two activities differing only in type produce equal loads. | FR-012, SC-009 |
| C7 | The assembly has zero package references and zero project references, and no type, member, parameter, or comment names Strava or any other provider. | FR-025, SC-010 |

## Stability

This surface is internal to the solution and has no external consumers. It is expected to change as
later features arrive — persistence will need a way to rehydrate an activity, and aggregation will
need collections of loads. Neither is designed for here (Principle III); this contract describes
what feature 001 delivers, not a frozen public API.
