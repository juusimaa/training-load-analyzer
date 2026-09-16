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
        if (movingTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(movingTime),
                movingTime,
                "A session's moving time must be a positive span of time.");
        }

        if (!Enum.IsDefined(activityType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(activityType),
                activityType,
                "A session must be classified as either running or cycling.");
        }

        if (startedAt == default)
        {
            throw new ArgumentException("A session must have a start time.", nameof(startedAt));
        }

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

    /// <summary>
    ///   This session's training load: measured from the heart-rate series when one is present
    ///   (FR-009), otherwise estimated from moving time (FR-013). Always marked as which
    ///   (FR-014). Pure: the same receiver and argument always yield an equal value, with no
    ///   reads of the clock or of any ambient state (FR-011).
    /// </summary>
    public TrainingLoad CalculateTrainingLoad(int maximumHeartRate)
    {
        if (HeartRate is null)
        {
            // The default intensity weight of 2 is fixed by the specification's Assumptions.
            var minutes = (decimal)MovingTime.Ticks / TimeSpan.TicksPerMinute;

            return new TrainingLoad(minutes * 2, LoadProvenance.Estimated);
        }

        return new TrainingLoad(HeartRate.TrimpPoints(maximumHeartRate), LoadProvenance.Measured);
    }
}
