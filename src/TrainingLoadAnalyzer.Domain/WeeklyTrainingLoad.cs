namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   One ISO-8601 week's total training load (FR-002).
/// </summary>
public readonly record struct WeeklyTrainingLoad(
    IsoWeek Week,
    decimal Points,
    int ActivityCount,
    LoadBasis Basis);
