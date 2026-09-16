namespace TrainingLoadAnalyzer.Domain.Tests;

public class HeartRateSeriesTests
{
    // User Story 1, scenario 5 (FR-007)
    [Fact]
    public void A_series_returns_its_samples_in_their_original_order_with_their_original_values()
    {
        var samples = new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),
            new HeartRateSample(TimeSpan.FromMinutes(10), 175),
            new HeartRateSample(TimeSpan.FromMinutes(20), 160),
        };

        var series = new HeartRateSeries(samples);

        Assert.Equal(samples, series.Samples);
    }
}
