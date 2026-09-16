namespace TrainingLoadAnalyzer.Domain.Tests;

public class TrainingActivityValidationTests
{
    private static readonly DateTimeOffset AnyStart =
        new(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2));

    // User Story 3, scenario 1 (FR-017).
    [Fact]
    public void A_moving_time_of_zero_is_refused()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TrainingActivity("A-1", AnyStart, TimeSpan.Zero, ActivityType.Running));

        Assert.Equal("movingTime", refusal.ParamName);
    }

    // User Story 3, scenario 2 (FR-017).
    [Fact]
    public void A_negative_moving_time_is_refused()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TrainingActivity("A-1", AnyStart, TimeSpan.FromMinutes(-1), ActivityType.Running));

        Assert.Equal("movingTime", refusal.ParamName);
    }

    // User Story 3, scenario 3 (FR-018, FR-005).
    [Fact]
    public void An_activity_type_outside_the_defined_set_is_refused()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TrainingActivity("A-1", AnyStart, TimeSpan.FromMinutes(45), (ActivityType)99));

        Assert.Equal("activityType", refusal.ParamName);
    }
}
