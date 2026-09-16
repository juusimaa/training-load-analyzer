namespace TrainingLoadAnalyzer.Domain.Tests;

public class HeartRateZoneTests
{
    // FR-009: below 50% of maximum contributes nothing. 99/200 is 49.5%.
    [Fact]
    public void A_heart_rate_below_fifty_percent_of_maximum_has_weight_zero()
    {
        Assert.Equal(0, HeartRateZone.WeightFor(bpm: 99, maximumHeartRate: 200));
    }

    // FR-009, at maximum 200 so every boundary lands on a whole bpm.
    // Lower bound inclusive, upper bound exclusive.
    [Theory]
    [InlineData(99, 0)]    // 49.5% — below zone 1
    [InlineData(100, 1)]   // 50.0% — zone 1 lower bound, inclusive
    [InlineData(119, 1)]   // 59.5% — zone 1 upper end
    [InlineData(120, 2)]   // 60.0% — zone 2 lower bound
    [InlineData(139, 2)]   // 69.5%
    [InlineData(140, 3)]   // 70.0% — zone 3 lower bound
    [InlineData(159, 3)]   // 79.5%
    [InlineData(160, 4)]   // 80.0% — zone 4 lower bound
    [InlineData(179, 4)]   // 89.5%
    [InlineData(180, 5)]   // 90.0% — zone 5 lower bound
    [InlineData(199, 5)]   // 99.5%
    [InlineData(200, 5)]   // 100.0% — still zone 5
    public void Each_zone_boundary_classifies_exactly(int bpm, int expectedWeight)
    {
        Assert.Equal(expectedWeight, HeartRateZone.WeightFor(bpm, maximumHeartRate: 200));
    }
}
