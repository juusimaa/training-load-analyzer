namespace TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

/// <summary>
///   A clock the test controls. Three requirements depend on "now" and are untestable without it:
///   FR-017a's 180-day measured window, FR-004's token-expiry check, and FR-035's retry time
///   (research R10).
/// </summary>
/// <remarks>
///   Features 001-004 were forbidden from reading the clock at all, because they were pure
///   calculations. A sync is inherently something that happens at a time; the discipline here is
///   that the time is an <em>input</em> rather than an ambient read.
/// </remarks>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>The instant every test in this feature runs at, fixing the measured window's start
    /// at 2026-03-21 (tasks.md, "Fixture activities").</summary>
    public static readonly DateTimeOffset Default = new(2026, 9, 17, 10, 7, 33, TimeSpan.Zero);

    public FixedClock()
        : this(Default)
    {
    }

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now = now.Add(by);

    public void AdvanceTo(DateTimeOffset instant) => now = instant;
}
