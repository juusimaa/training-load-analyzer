namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   One calendar day's training-stress position (FR-001, FR-002).
/// </summary>
public readonly record struct DailyTrainingMetrics(
    DateOnly Day,
    double Fitness,
    double Fatigue,
    bool IsReliable,
    LoadBasis FitnessBasis,
    LoadBasis FatigueBasis)
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

    /// <summary>
    ///   The basis of <see cref="Form"/>: the combined basis of every day feeding either metric
    ///   (FR-019a).
    /// </summary>
    /// <remarks>
    ///   Always equal to <see cref="FitnessBasis"/>, and derived rather than stored so it cannot
    ///   be made to differ. Fatigue's 7-day window always sits inside fitness's 42-day one — both
    ///   end on the same day and both truncate at the start of the history — so their union is
    ///   exactly the fitness window (FR-019b). A basis of <see cref="LoadBasis.None"/> contributes
    ///   nothing to the combination rather than counting as disagreement, exactly as a rest day
    ///   contributes nothing under FR-018.
    /// </remarks>
    public LoadBasis FormBasis => FitnessBasis;
}
