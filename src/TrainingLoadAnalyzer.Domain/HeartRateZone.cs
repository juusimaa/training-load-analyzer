namespace TrainingLoadAnalyzer.Domain;

/// <summary>The Edwards TRIMP five-zone table, as a percentage of maximum heart rate (FR-009).</summary>
public static class HeartRateZone
{
    /// <summary>The Edwards weight 0-5 for a heart rate.</summary>
    public static int WeightFor(int bpm, int maximumHeartRate) => 0;
}
