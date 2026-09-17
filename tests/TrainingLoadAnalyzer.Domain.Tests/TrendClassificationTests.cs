namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   User Story 2 — have the changes that matter called out.
/// </summary>
/// <remarks>
///   Every test here runs against <see cref="WeeklyLoadTrend"/> alone: no history, no range, no
///   calculator. That is possible because the classification is a derived property (research R6),
///   which is what makes this story independently testable rather than nominally so.
/// </remarks>
public class TrendClassificationTests
{
    private static readonly IsoWeek Week = IsoWeek.For(new DateOnly(2026, 3, 2));

    /// <summary>
    ///   One week's comparison, built from the two totals alone. Every test goes through this,
    ///   so members added to the record later change one line here rather than thirty.
    /// </summary>
    private static WeeklyLoadTrend Trend(decimal previous, decimal current) =>
        new(Week, current, previous);

    // T027 - User Story 2, scenario 1 (FR-008): a real step up is called out.
    [Fact]
    public void A_week_that_rose_sharply_is_a_significant_increase()
    {
        Assert.Equal(TrendClassification.SignificantIncrease, Trend(500m, 600m).Classification);
    }

    // T029 - User Story 2, scenario 2 (FR-009, FR-010): a wobble is not a ramp-up.
    [Fact]
    public void A_week_that_barely_moved_is_steady()
    {
        Assert.Equal(TrendClassification.Steady, Trend(500m, 520m).Classification);
    }

    // T031 - FR-010, the relative discriminator. +60 points clears the absolute floor, but 6%
    // of a big week is not a change of gear. Without this test an implementation applying only
    // the floor passes everything else in this class.
    [Fact]
    public void A_large_week_moving_a_small_share_of_itself_is_steady()
    {
        Assert.Equal(TrendClassification.Steady, Trend(1000m, 1060m).Classification);
    }

    // T033 - User Story 2, scenario 3 (FR-010), the floor discriminator. +30% reads alarming
    // until you notice it is six points. Paired with T031 above, this is what proves both
    // thresholds are applied: an implementation with only the relative test fails here, one
    // with only the floor fails there, and neither test alone catches both.
    [Fact]
    public void A_tiny_week_moving_a_large_share_of_itself_is_steady()
    {
        Assert.Equal(TrendClassification.Steady, Trend(20m, 26m).Classification);
    }

    // T034 - User Story 2, scenario 4 (FR-008): a collapse is called out on the same terms.
    [Fact]
    public void A_week_that_fell_sharply_is_a_significant_decrease()
    {
        Assert.Equal(TrendClassification.SignificantDecrease, Trend(600m, 400m).Classification);
    }

    // T036 - User Story 2, scenario 5 (FR-003, FR-008): a week of total rest after a real week
    // is a significant drop, and unlike the reverse case the proportion is perfectly defined.
    [Fact]
    public void Stopping_altogether_is_a_significant_decrease_of_the_whole_week()
    {
        var stopped = Trend(500m, 0m);

        Assert.Equal(TrendClassification.SignificantDecrease, stopped.Classification);
        Assert.Equal(-1m, stopped.RelativeChange);
    }

    // T037 - User Story 3, scenario 1 (FR-013, FR-014). Returning to training from an idle week
    // is exactly the jump worth flagging, yet there is no proportion to test it with. An
    // implementation that checks the relative threshold before the floor returns Steady here
    // and silently never flags a comeback.
    [Fact]
    public void Returning_from_an_idle_week_is_a_significant_increase_on_the_floor_alone()
    {
        var returned = Trend(0m, 400m);

        Assert.Null(returned.RelativeChange);
        Assert.Equal(TrendClassification.SignificantIncrease, returned.Classification);
    }

    // T039 - FR-014 and User Story 3, scenario 2. Resuming training is a real increase, but
    // resuming it gently is not a significant one: the floor applies whether or not there is a
    // proportion to go with it. Two idle weeks in a row are simply steady.
    [Fact]
    public void A_gentle_return_and_a_second_idle_week_are_both_steady()
    {
        var gentle = Trend(0m, 30m);
        var stillIdle = Trend(0m, 0m);

        Assert.Equal(TrendClassification.Steady, gentle.Classification);
        Assert.Null(gentle.RelativeChange);

        Assert.Equal(TrendClassification.Steady, stillIdle.Classification);
        Assert.Null(stillIdle.RelativeChange);
    }

    // T040 - FR-011, C35. A change sitting exactly on a threshold is significant: both tests
    // are "at least", never "more than". The two thresholds are pinned separately because
    // FR-011's own illustration - exactly 0.15 and exactly 50 points together - needs a
    // previous week of 50 / 0.15 = 333.333... and is not exactly representable. The
    // requirement is sound; only its example is unreachable.
    [Theory]
    [InlineData(400, 460, TrendClassification.SignificantIncrease)]  // relative exactly 0.15
    [InlineData(400, 459, TrendClassification.Steady)]               // just under
    [InlineData(200, 250, TrendClassification.SignificantIncrease)]  // absolute exactly 50
    [InlineData(200, 249, TrendClassification.Steady)]               // just under
    public void A_change_exactly_on_a_threshold_clears_it(
        int previous, int current, TrendClassification expected)
    {
        Assert.Equal(expected, Trend(previous, current).Classification);
    }

    // T041 - FR-007, C34, research R6. The discriminating check for this story: a caller cannot
    // construct a trend whose label contradicts its numbers, because there is no label to pass
    // in. That is what makes the classification rule structural rather than a convention the
    // calculator has to keep to.
    [Fact]
    public void The_classification_is_derived_and_cannot_be_set()
    {
        var constructed = typeof(WeeklyLoadTrend)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain(nameof(WeeklyLoadTrend.Classification), constructed);
        Assert.Null(typeof(WeeklyLoadTrend)
            .GetProperty(nameof(WeeklyLoadTrend.Classification))!.SetMethod);
    }

    // T042 - FR-012: the thresholds are applied to the unrounded changes, so nothing can be
    // rounded across a threshold on its way in.
    [Fact]
    public void The_thresholds_are_applied_without_rounding_first()
    {
        Assert.Equal(TrendClassification.Steady, Trend(200m, 249.9m).Classification);
        Assert.Equal(
            TrendClassification.SignificantIncrease, Trend(200m, 250.0000001m).Classification);
    }
}
