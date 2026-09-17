namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   How far a daily or weekly total can be trusted, given where the loads behind it came from
///   (FR-014).
/// </summary>
/// <remarks>
///   Distinct from <see cref="LoadProvenance"/>, which describes one activity and has two states:
///   a single activity can never be <see cref="Mixed"/> or <see cref="None"/>.
/// </remarks>
public enum LoadBasis
{
    /// <summary>No activity contributed. The default, which is what a rest day reports.</summary>
    None,

    /// <summary>Every contributing activity's load was measured from heart-rate data.</summary>
    Measured,

    /// <summary>Every contributing activity's load was estimated from moving time.</summary>
    Estimated,

    /// <summary>Both kinds contributed, so the total is weaker than a measured one.</summary>
    Mixed,
}
