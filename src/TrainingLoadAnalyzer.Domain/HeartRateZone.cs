namespace TrainingLoadAnalyzer.Domain;

/// <summary>The Edwards TRIMP five-zone table, as a percentage of maximum heart rate (FR-009).</summary>
public static class HeartRateZone
{
    /// <summary>
    ///   The Edwards weight 0-5 for a heart rate: 0 below 50% of maximum, then 1-5 for the five
    ///   bands, with 5 covering everything at or above 90% including rates above the stated maximum.
    /// </summary>
    /// <remarks>
    ///   Classification is by integer cross-multiplication, so a rate sitting exactly on a boundary
    ///   lands in the upper zone deterministically with no floating-point rounding. Lower bound
    ///   inclusive, upper bound exclusive.
    /// </remarks>
    public static int WeightFor(int bpm, int maximumHeartRate)
    {
        var scaled = bpm * 100;

        if (scaled >= 90 * maximumHeartRate) return 5;
        if (scaled >= 80 * maximumHeartRate) return 4;
        if (scaled >= 70 * maximumHeartRate) return 3;
        if (scaled >= 60 * maximumHeartRate) return 2;
        if (scaled >= 50 * maximumHeartRate) return 1;

        return 0;
    }
}
