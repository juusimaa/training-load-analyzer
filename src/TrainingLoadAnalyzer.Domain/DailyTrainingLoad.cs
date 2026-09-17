namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   One calendar day's total training load, in the athlete's local time at each contributing
///   activity's own recorded offset (FR-001, FR-004).
/// </summary>
public readonly record struct DailyTrainingLoad(DateOnly Day, decimal Points);
