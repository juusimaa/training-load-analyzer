namespace TrainingLoadAnalyzer.Domain.Tests;

public class HeartRateZoneTests
{
    // FR-009: below 50% of maximum contributes nothing. 99/200 is 49.5%.
    [Fact]
    public void A_heart_rate_below_fifty_percent_of_maximum_has_weight_zero()
    {
        Assert.Equal(0, HeartRateZone.WeightFor(bpm: 99, maximumHeartRate: 200));
    }
}
