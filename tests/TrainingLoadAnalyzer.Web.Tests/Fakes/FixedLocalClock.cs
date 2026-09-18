namespace TrainingLoadAnalyzer.Web.Tests.Fakes;

/// <summary>
///   A clock the test controls, pinned in <em>both</em> respects: the instant and the time zone.
/// </summary>
/// <remarks>
///   <para>
///     Feature 005's <c>FixedClock</c> is deliberately not reused. It overrides only
///     <see cref="TimeProvider.GetUtcNow"/>, and <see cref="TimeProvider.GetLocalNow"/> converts
///     through <see cref="TimeProvider.LocalTimeZone"/>, which defaults to
///     <see cref="TimeZoneInfo.Local"/>. Verified by running it: with the instant pinned to
///     2026-09-18T04:00:00Z, <c>GetLocalNow()</c> returned 07:00+03:00 on a Europe/Helsinki machine
///     and would return 04:00 on a UTC build agent — moving every date assertion in this feature
///     (research R22).
///   </para>
///   <para>
///     The zone is a fixed custom offset rather than a named one, so the tests do not depend on a
///     time-zone database or on any government's future decision about daylight saving.
///   </para>
/// </remarks>
internal sealed class FixedLocalClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>
    ///   The athlete's offset in every test in this feature: +03:00, making the default instant
    ///   2026-09-18T07:00+03:00 — a Friday, in ISO week 2026-W38 (tasks.md, "The clock").
    /// </summary>
    public static readonly TimeSpan FixtureOffset = TimeSpan.FromHours(3);

    /// <summary>The instant every test in this feature runs at.</summary>
    public static readonly DateTimeOffset Default = new(2026, 9, 18, 4, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo FixtureZone = TimeZoneInfo.CreateCustomTimeZone(
        "TrainingLoadAnalyzer/Fixture",
        FixtureOffset,
        "Fixture (UTC+03:00)",
        "Fixture (UTC+03:00)");

    public FixedLocalClock()
        : this(Default)
    {
    }

    public override DateTimeOffset GetUtcNow() => now;

    /// <summary>
    ///   The override that matters. Without it, <see cref="TimeProvider.GetLocalNow"/> would read the
    ///   machine's zone and the athlete's "today" would differ between a laptop and CI.
    /// </summary>
    public override TimeZoneInfo LocalTimeZone => FixtureZone;

    public void Advance(TimeSpan by) => now = now.Add(by);

    public void AdvanceTo(DateTimeOffset instant) => now = instant;

    /// <summary>The athlete's local day, which is what the dashboard means by "today" (R22).</summary>
    public DateOnly Today => DateOnly.FromDateTime(GetLocalNow().DateTime);
}
