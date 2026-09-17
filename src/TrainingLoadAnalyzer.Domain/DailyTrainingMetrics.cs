namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   One calendar day's training-stress position (FR-001, FR-002).
/// </summary>
public readonly record struct DailyTrainingMetrics(DateOnly Day, double Fitness, double Fatigue)
{
    /// <summary>
    ///   The balance between the two: positive means fresher than the recent training would
    ///   suggest, negative means carrying accumulated work (FR-003).
    /// </summary>
    /// <remarks>
    ///   Computed on every read and never accumulated, so no instance can exist whose form
    ///   disagrees with the two figures it comes from. SC-003 therefore holds by construction.
    /// </remarks>
    public double Form => Fitness - Fatigue;
}
