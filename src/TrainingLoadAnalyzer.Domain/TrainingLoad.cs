namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   How demanding one session was, in TRIMP points, together with whether that number was
///   measured or estimated (FR-008, FR-014).
/// </summary>
/// <remarks>
///   The two parts are inseparable: there is no member or conversion that yields the number
///   without its provenance, so a consumer cannot accidentally drop it (SC-007).
/// </remarks>
public readonly record struct TrainingLoad(decimal Points, LoadProvenance Provenance);
