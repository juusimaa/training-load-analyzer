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

    // User Story 3, scenario 4 (FR-019). Only the default value is refused — a future start
    // time is accepted, which TrainingActivityCreationTests asserts.
    [Fact]
    public void A_missing_start_time_is_refused()
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => new TrainingActivity("A-1", default, TimeSpan.FromMinutes(45), ActivityType.Running));

        Assert.Equal("startedAt", refusal.ParamName);
    }

    // User Story 3, scenario 5 (FR-020).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_or_blank_external_identifier_is_refused(string? externalId)
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => new TrainingActivity(externalId!, AnyStart, TimeSpan.FromMinutes(45), ActivityType.Running));

        Assert.Equal("externalId", refusal.ParamName);
    }

    // User Story 3, scenario 10 (FR-023, contract C2). Every guard runs before any field is
    // assigned, so a refusal is the only thing the caller ever gets back — there is no
    // out-parameter, no static registry, and no partially-populated instance to inspect.
    [Fact]
    public void A_refused_construction_leaves_no_instance_behind()
    {
        Assert.Throws<ArgumentException>(
            () => new TrainingActivity("", AnyStart, TimeSpan.FromMinutes(45), ActivityType.Running));
        Assert.Throws<ArgumentException>(
            () => new TrainingActivity("A-1", default, TimeSpan.FromMinutes(45), ActivityType.Running));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TrainingActivity("A-1", AnyStart, TimeSpan.Zero, ActivityType.Running));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TrainingActivity("A-1", AnyStart, TimeSpan.FromMinutes(45), (ActivityType)99));

        // The only way to obtain a TrainingActivity is the constructor, and it either returns a
        // fully valid instance or throws: no factory, no parameterless constructor, no static
        // state that a refused attempt could have touched.
        Assert.Empty(typeof(TrainingActivity).GetConstructors().Where(c => c.GetParameters().Length == 0));
        Assert.Empty(typeof(TrainingActivity).GetFields(
            System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic));
    }
}
