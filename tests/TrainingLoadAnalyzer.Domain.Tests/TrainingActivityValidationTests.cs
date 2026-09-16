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
}
