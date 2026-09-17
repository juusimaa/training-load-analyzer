namespace TrainingLoadAnalyzer.Domain.Tests;

/// <summary>
///   The range a caller asks about: bounded at both ends, and never backwards (FR-019, FR-020).
/// </summary>
public class DateRangeTests
{
    // FR-019: an end before the start is refused, and the refusal names the end.
    [Fact]
    public void A_range_whose_end_precedes_its_start_is_refused()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new DateRange(new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 1)));

        Assert.Equal("end", exception.ParamName);
    }

    // FR-020: a range must be explicitly bounded at both ends. default(DateOnly) is how an
    // absent bound arrives, following feature 001's treatment of a missing start time.
    [Fact]
    public void A_range_with_a_missing_start_is_refused()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new DateRange(default, new DateOnly(2026, 3, 5)));

        Assert.Equal("start", exception.ParamName);
    }

    // FR-020, and the ordering that matters: a missing end must report the missing bound rather
    // than the end-before-start rule, which it would also violate.
    [Fact]
    public void A_range_with_a_missing_end_is_refused_as_missing_not_as_backwards()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new DateRange(new DateOnly(2026, 3, 1), default));

        Assert.Equal("end", exception.ParamName);
        Assert.Contains("unbounded", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Feature 001's contract guarantee C3, applied here (FR-021): two rules share the ParamName
    // "end", so their messages must tell them apart - otherwise a test for one passes against
    // the other and neither rule is really covered.
    [Fact]
    public void The_two_refusals_that_name_the_end_are_distinguishable_from_each_other()
    {
        var missing = Assert.Throws<ArgumentException>(
            () => new DateRange(new DateOnly(2026, 3, 1), default));
        var backwards = Assert.Throws<ArgumentException>(
            () => new DateRange(new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 1)));

        Assert.Equal("end", missing.ParamName);
        Assert.Equal("end", backwards.ParamName);
        Assert.NotEqual(missing.Message, backwards.Message);
    }

    // A one-day range is valid: both endpoints are included, so start == end is a real range.
    [Fact]
    public void A_range_of_one_day_is_accepted()
    {
        var range = new DateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2));

        Assert.Equal(new DateOnly(2026, 3, 2), Assert.Single(range.Days));
    }

    // Contract C17: a DateRange is either valid or does not exist. No setter, and sealed.
    [Fact]
    public void A_date_range_cannot_be_changed_after_construction()
    {
        Assert.True(typeof(DateRange).IsSealed);
        Assert.DoesNotContain(
            typeof(DateRange).GetProperties(),
            property => property.SetMethod is { IsPublic: true });
    }
}
