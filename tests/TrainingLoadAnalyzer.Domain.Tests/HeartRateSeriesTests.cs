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

    // User Story 2, scenario 2 — the worked example (FR-009, FR-010). At maximum 190:
    // 150 bpm is 78.9% (zone 3, weight 3) held 10 minutes = 30; 175 bpm is 92.1%
    // (zone 5, weight 5) held 10 minutes = 50; the final sample has no successor.
    [Fact]
    public void The_worked_example_yields_exactly_eighty_trimp_points()
    {
        var series = new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),
            new HeartRateSample(TimeSpan.FromMinutes(10), 175),
            new HeartRateSample(TimeSpan.FromMinutes(20), 160),
        });

        Assert.Equal(80m, series.TrimpPoints(maximumHeartRate: 190));
    }
}
