namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   The judgement attached to one week's comparison against the week before it (FR-007).
/// </summary>
public enum TrendClassification
{
    /// <summary>
    ///   The week is partial within the requested range, so the comparison is not like for like
    ///   and no judgement is offered. First, and therefore the default: an uninitialised
    ///   classification reads as "no judgement available", which is the safe reading.
    /// </summary>
    Indeterminate,

    /// <summary>The change did not clear both thresholds, including a change of zero.</summary>
    Steady,

    /// <summary>The week's rise cleared both thresholds.</summary>
    SignificantIncrease,

    /// <summary>The week's fall cleared both thresholds.</summary>
    SignificantDecrease,
}
