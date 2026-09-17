namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   The judgement attached to one week's comparison against the week before it (FR-007).
/// </summary>
public enum TrendClassification
{
    /// <summary>The change did not clear both thresholds, including a change of zero.</summary>
    Steady,

    /// <summary>The week's rise cleared both thresholds.</summary>
    SignificantIncrease,

    /// <summary>The week's fall cleared both thresholds.</summary>
    SignificantDecrease,
}
