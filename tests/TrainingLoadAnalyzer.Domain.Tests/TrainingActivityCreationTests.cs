namespace TrainingLoadAnalyzer.Domain.Tests;

public class TrainingActivityCreationTests
{
    // User Story 1, scenario 1 (FR-001, FR-002, FR-003, FR-004, FR-005)
    [Fact]
    public void A_recorded_run_reads_back_every_detail_unchanged()
    {
        var startedAt = new DateTimeOffset(2026, 3, 1, 7, 30, 0, TimeSpan.FromHours(2));

        var activity = new TrainingActivity(
            "A-1",
            startedAt,
            TimeSpan.FromMinutes(45),
            ActivityType.Running);

        Assert.Equal("A-1", activity.ExternalId);
        Assert.Equal(startedAt, activity.StartedAt);
        Assert.Equal(TimeSpan.FromMinutes(45), activity.MovingTime);
        Assert.Equal(ActivityType.Running, activity.Type);
    }
}
