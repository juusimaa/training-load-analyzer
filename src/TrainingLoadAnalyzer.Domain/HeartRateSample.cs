namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   One heart-rate measurement, timed from the session's start so that the load calculation
///   never touches dates or offsets (FR-007).
/// </summary>
/// <remarks>
///   The sample validates nothing. Ordering and plausible range are invariants of the series,
///   enforced in one place by <see cref="HeartRateSeries"/> (FR-021).
/// </remarks>
public readonly record struct HeartRateSample(TimeSpan TimeFromStart, int Bpm);
