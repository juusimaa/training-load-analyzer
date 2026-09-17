namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   User Story 1 — see how this week compares to last week.
/// </summary>
public class WeeklyLoadTrendTests
{
    private static readonly DateOnly W10 = new(2026, 3, 2);

    private static IsoWeek Week(int offsetWeeks) => IsoWeek.For(W10.AddDays(7 * offsetWeeks));

    /// <summary>A contiguous run of weeks from 2026-W10, one per total supplied.</summary>
    private static WeeklyTrainingLoad[] History(params decimal[] points) =>
        [.. points.Select((p, i) => new WeeklyTrainingLoad(Week(i), p, p == 0 ? 0 : 4,
            p == 0 ? LoadBasis.None : LoadBasis.Measured))];

    private static DateRange WholeWeek(int offsetWeeks) =>
        new(Week(offsetWeeks).Monday, Week(offsetWeeks).Sunday);

    // User Story 1, scenario 1 (FR-002): the week's own points less the week before it.
    [Fact]
    public void A_week_reports_how_many_points_it_moved_against_the_week_before()
    {
        var history = new[]
        {
            new WeeklyTrainingLoad(Week(0), 500m, 4, LoadBasis.Measured),
            new WeeklyTrainingLoad(Week(1), 600m, 5, LoadBasis.Measured),
        };
        var week11 = new DateRange(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 15));

        var trends = TrainingLoadTrendCalculator.Calculate(history, week11);

        Assert.Equal(100m, Assert.Single(trends).AbsoluteChange);
    }

    // User Story 1, scenario 1 (FR-003): the same move as a share of the week before it.
    [Fact]
    public void A_week_reports_that_move_as_a_proportion_of_the_week_before()
    {
        var history = new[]
        {
            new WeeklyTrainingLoad(Week(0), 500m, 4, LoadBasis.Measured),
            new WeeklyTrainingLoad(Week(1), 600m, 5, LoadBasis.Measured),
        };
        var week11 = new DateRange(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 15));

        var trends = TrainingLoadTrendCalculator.Calculate(history, week11);

        Assert.Equal(0.2m, Assert.Single(trends).RelativeChange);
    }

    // User Story 3, scenario 1 (FR-004): coming back from an idle week there is nothing to
    // divide by, so no proportion exists. It is absent - not zero, not an infinity.
    [Fact]
    public void A_week_following_an_idle_one_reports_no_proportion_at_all()
    {
        var history = new[]
        {
            new WeeklyTrainingLoad(Week(0), 0m, 0, LoadBasis.None),
            new WeeklyTrainingLoad(Week(1), 400m, 4, LoadBasis.Measured),
        };
        var week11 = new DateRange(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 15));

        var trends = TrainingLoadTrendCalculator.Calculate(history, week11);

        Assert.Null(Assert.Single(trends).RelativeChange);
    }

    // T010 - User Story 1, scenarios 2 and 3 (FR-002, FR-003). A fall, and no change at all.
    // A zero change still has a proportion: only a missing divisor makes one absent.
    [Fact]
    public void A_fall_and_a_flat_week_both_report_their_change()
    {
        var fall = TrainingLoadTrendCalculator.Calculate(History(600m, 450m), WholeWeek(1));
        var flat = TrainingLoadTrendCalculator.Calculate(History(500m, 500m), WholeWeek(1));

        Assert.Equal(-150m, Assert.Single(fall).AbsoluteChange);
        Assert.Equal(-0.25m, Assert.Single(fall).RelativeChange);

        Assert.Equal(0m, Assert.Single(flat).AbsoluteChange);
        Assert.Equal(0m, Assert.Single(flat).RelativeChange);
        Assert.NotNull(Assert.Single(flat).RelativeChange);
    }

    // T011 - FR-033, C32: the change is reproducible by subtracting the two totals, exactly.
    // This is the test that catches a double slipping in: 610.4 - 505.7 in binary floating
    // point is 104.69999999999999, which would fail here (research R2).
    [Fact]
    public void The_absolute_change_is_exact_with_no_tolerance_allowed()
    {
        var trends = TrainingLoadTrendCalculator.Calculate(History(505.7m, 610.4m), WholeWeek(1));

        Assert.Equal(104.7m, Assert.Single(trends).AbsoluteChange);
    }

    // T012 - FR-002, FR-003, C32, C33, research R6. The discriminating check: every test above
    // would pass just as well against stored fields the calculator kept in step by hand. Only
    // this one establishes that the two changes cannot drift from the points they come from.
    [Fact]
    public void The_two_changes_are_derived_and_cannot_be_set()
    {
        var constructed = typeof(WeeklyLoadTrend)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain(nameof(WeeklyLoadTrend.AbsoluteChange), constructed);
        Assert.DoesNotContain(nameof(WeeklyLoadTrend.RelativeChange), constructed);

        Assert.Null(typeof(WeeklyLoadTrend)
            .GetProperty(nameof(WeeklyLoadTrend.AbsoluteChange))!.SetMethod);
        Assert.Null(typeof(WeeklyLoadTrend)
            .GetProperty(nameof(WeeklyLoadTrend.RelativeChange))!.SetMethod);
    }

    // T013 - FR-023, FR-028: the range decides what comes back. Weeks of history outside it are
    // used to compare against and are not themselves reported.
    [Fact]
    public void Only_the_weeks_touching_the_requested_range_are_returned()
    {
        var history = History(400m, 500m, 600m, 700m);

        var trends = TrainingLoadTrendCalculator.Calculate(history, WholeWeek(3));

        var only = Assert.Single(trends);
        Assert.Equal(Week(3), only.Week);
        Assert.Equal(700m, only.Points);
        Assert.Equal(600m, only.PreviousPoints);
    }

    // T015 - User Story 1, scenario 4 (FR-028, C31): one entry per week, ascending, each
    // comparing itself to the week immediately before it.
    [Fact]
    public void Every_week_in_the_range_reports_once_in_ascending_order()
    {
        var history = History(400m, 500m, 600m, 700m, 800m);
        var fourWeeks = new DateRange(Week(1).Monday, Week(4).Sunday);

        var trends = TrainingLoadTrendCalculator.Calculate(history, fourWeeks);

        Assert.Equal(4, trends.Count);
        Assert.Equal([Week(1), Week(2), Week(3), Week(4)], trends.Select(t => t.Week));
        Assert.Equal([500m, 600m, 700m, 800m], trends.Select(t => t.Points));
        Assert.Equal([400m, 500m, 600m, 700m], trends.Select(t => t.PreviousPoints));
    }

    // T016 - C31: a range narrower than a week still touches a week, and reports it once.
    [Fact]
    public void A_range_lying_inside_one_week_reports_that_week_once()
    {
        var history = History(500m, 600m);
        var midweek = new DateRange(new DateOnly(2026, 3, 11), new DateOnly(2026, 3, 13));

        var trends = TrainingLoadTrendCalculator.Calculate(history, midweek);

        Assert.Equal(Week(1), Assert.Single(trends).Week);
    }
}
