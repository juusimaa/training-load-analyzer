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

    // FR-010: the final sample contributes no time, so one sample scores nothing.
    [Fact]
    public void A_single_sample_series_yields_zero_points()
    {
        var series = new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 175),
        });

        Assert.Equal(0m, series.TrimpPoints(maximumHeartRate: 190));
    }

    // Spec Assumptions: the hold-until-next rule charges a gap to the sample before it.
    [Fact]
    public void A_long_gap_is_charged_to_the_sample_preceding_it()
    {
        var series = new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),    // zone 3 at maximum 190
            new HeartRateSample(TimeSpan.FromMinutes(60), 100),   // 60-minute gap held at 150
            new HeartRateSample(TimeSpan.FromMinutes(70), 100),   // below 50% — weight 0
        });

        // 60 minutes at weight 3 = 180; the 10 minutes at 100 bpm (52.6%, weight 1) = 10.
        Assert.Equal(190m, series.TrimpPoints(maximumHeartRate: 190));
    }

    // User Story 3, scenario 6 (FR-021): the session is refused, not the sample dropped.
    [Fact]
    public void A_sample_outside_the_plausible_range_is_refused_and_named()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(0), 150),
            new HeartRateSample(TimeSpan.FromMinutes(10), 300),
        }));

        Assert.Equal("samples", refusal.ParamName);
        Assert.Equal(300, refusal.ActualValue);

        // Asserted on the guard's own wording, not on "300": ArgumentOutOfRangeException appends
        // "Actual value was 300." by itself, so a message assertion on the number alone would
        // pass against any of the three rules that share ParamName "samples".
        Assert.Contains("plausible range", refusal.Message);
    }

    // User Story 3, scenario 7 (FR-021). Equal timestamps are not ascending, which resolves the
    // "duplicated at the same instant" edge case. The message assertion matters: all three
    // HeartRateSeries rules report ParamName "samples", so without it this test could pass
    // against the wrong guard.
    [Theory]
    [InlineData(10, 5)]     // decreasing
    [InlineData(10, 10)]    // equal, and therefore not ascending
    public void Samples_that_are_not_in_ascending_time_order_are_refused(int firstMinute, int secondMinute)
    {
        var refusal = Assert.Throws<ArgumentException>(() => new HeartRateSeries(new[]
        {
            new HeartRateSample(TimeSpan.FromMinutes(firstMinute), 150),
            new HeartRateSample(TimeSpan.FromMinutes(secondMinute), 160),
        }));

        Assert.Equal("samples", refusal.ParamName);
        Assert.Contains("ascending", refusal.Message);
    }

    // User Story 3, scenario 8 (FR-021, FR-006): an empty series is a refusal, not a synonym
    // for absent heart-rate data. See the ordering test above for why the message is asserted.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_null_or_empty_sample_list_is_refused(bool isNull)
    {
        var samples = isNull ? null : Array.Empty<HeartRateSample>();

        var refusal = Assert.Throws<ArgumentException>(() => new HeartRateSeries(samples!));

        Assert.Equal("samples", refusal.ParamName);
        Assert.Contains("at least one sample", refusal.Message);
    }
}
