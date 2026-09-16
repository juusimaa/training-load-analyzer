namespace TrainingLoadAnalyzer.Domain;

/// <summary>A single completed training session (FR-001).</summary>
public sealed class TrainingActivity
{
    public TrainingActivity(
        string externalId,
        DateTimeOffset startedAt,
        TimeSpan movingTime,
        ActivityType activityType,
        HeartRateSeries? heartRate = null)
    {
        ExternalId = externalId;
        StartedAt = startedAt;
        MovingTime = movingTime;
        Type = activityType;
        HeartRate = heartRate;
    }

    /// <summary>An opaque, provider-neutral identifier, stored verbatim (FR-002).</summary>
    public string ExternalId { get; }

    /// <summary>The start instant together with the athlete's offset from UTC (FR-003).</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Time spent actually moving, excluding stops (FR-004).</summary>
    public TimeSpan MovingTime { get; }

    public ActivityType Type { get; }

    /// <summary>The heart-rate series, or null when none was recorded (FR-006).</summary>
    public HeartRateSeries? HeartRate { get; }
}
