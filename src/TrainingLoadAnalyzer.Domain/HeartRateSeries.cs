namespace TrainingLoadAnalyzer.Domain;

/// <summary>The measured heart-rate record for one session (FR-007).</summary>
public sealed class HeartRateSeries
{
    public HeartRateSeries(IReadOnlyList<HeartRateSample> samples)
    {
        Samples = samples;
    }

    /// <summary>The samples, in ascending time order.</summary>
    public IReadOnlyList<HeartRateSample> Samples { get; }
}
