namespace TrainingLoadAnalyzer.Domain;

/// <summary>The measured heart-rate record for one session (FR-007).</summary>
public sealed class HeartRateSeries
{
    private const int MinimumPlausibleBpm = 20;
    private const int MaximumPlausibleBpm = 250;

    public HeartRateSeries(IReadOnlyList<HeartRateSample> samples)
    {
        for (var i = 1; i < samples.Count; i++)
        {
            if (samples[i].TimeFromStart <= samples[i - 1].TimeFromStart)
            {
                throw new ArgumentException(
                    $"Heart-rate sample times must be in ascending order; the sample at index {i} "
                        + $"({samples[i].TimeFromStart}) does not follow the one before it "
                        + $"({samples[i - 1].TimeFromStart}).",
                    nameof(samples));
            }
        }

        foreach (var sample in samples)
        {
            if (sample.Bpm is < MinimumPlausibleBpm or > MaximumPlausibleBpm)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(samples),
                    sample.Bpm,
                    $"A heart-rate sample of {sample.Bpm} bpm is outside the plausible range of "
                        + $"{MinimumPlausibleBpm}-{MaximumPlausibleBpm} bpm.");
            }
        }

        Samples = samples;
    }

    /// <summary>The samples, in ascending time order.</summary>
    public IReadOnlyList<HeartRateSample> Samples { get; }

    /// <summary>Edwards TRIMP points for this series (FR-009, FR-010).</summary>
    /// <remarks>
    ///   Each consecutive pair contributes the earlier sample's zone weight multiplied by the
    ///   minutes until the next sample, so a gap is charged to the sample preceding it. The final
    ///   sample has no successor and contributes nothing, which makes a single-sample series
    ///   score zero. Arithmetic is decimal throughout so a hand-computed expectation matches
    ///   exactly (SC-006).
    /// </remarks>
    public decimal TrimpPoints(int maximumHeartRate)
    {
        var points = 0m;

        for (var i = 0; i < Samples.Count - 1; i++)
        {
            var earlier = Samples[i];
            var later = Samples[i + 1];

            var weight = HeartRateZone.WeightFor(earlier.Bpm, maximumHeartRate);
            var minutes = (decimal)(later.TimeFromStart - earlier.TimeFromStart).Ticks / TimeSpan.TicksPerMinute;

            points += weight * minutes;
        }

        return points;
    }
}
