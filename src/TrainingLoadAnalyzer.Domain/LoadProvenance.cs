namespace TrainingLoadAnalyzer.Domain;

/// <summary>How a training load was arrived at (FR-014).</summary>
public enum LoadProvenance
{
    /// <summary>Computed from heart-rate data per FR-009.</summary>
    Measured,

    /// <summary>Computed from moving time per FR-013.</summary>
    Estimated,
}
