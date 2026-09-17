using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

namespace TrainingLoadAnalyzer.Infrastructure.Tests;

/// <summary>
///   FR-024 — what is read back equals what was written, member for member. Asserted twice: once on
///   the reconstructed domain object, and once on the raw columns, because a domain-level round trip
///   passes against several wrong mappings (research R8).
/// </summary>
public sealed class PersistenceRoundTripTests
{
    /// <summary>Fixture activity A1: a Helsinki run, 07:30 local, 52 minutes moving.</summary>
    private static TrainingActivity A1(HeartRateSeries? heartRate = null) => new(
        "11000000001",
        new DateTimeOffset(2026, 9, 10, 7, 30, 0, TimeSpan.FromHours(3)),
        TimeSpan.FromSeconds(3120),
        ActivityType.Running,
        heartRate);

    private static HeartRateSeries S3() => new(
    [
        new HeartRateSample(TimeSpan.Zero, 95),
        new HeartRateSample(TimeSpan.FromSeconds(30), 142),
        new HeartRateSample(TimeSpan.FromSeconds(60) + TimeSpan.FromTicks(1234), 171),
    ]);

    private static SqliteFixture<ImportDbContext> NewFixture() => new(options => new ImportDbContext(options));

    // T041: FR-022, FR-024, C53.
    [Fact]
    public async Task ASessionReadBackEqualsTheSessionThatWasStored()
    {
        using var fixture = NewFixture();
        var original = A1(S3());

        await using (var writing = fixture.NewContext())
        {
            await new ActivityStore(writing).UpsertAsync(
                original,
                heartRateOutstanding: false,
                TestContext.Current.CancellationToken);
        }

        await using var reading = fixture.NewContext();
        var stored = (await new ActivityStore(reading).InRangeAsync(
            DateTimeOffset.MinValue,
            DateTimeOffset.MaxValue,
            TestContext.Current.CancellationToken)).Single();

        Assert.Equal(original.ExternalId, stored.ExternalId);
        Assert.True(original.StartedAt.EqualsExact(stored.StartedAt));
        Assert.Equal(original.MovingTime, stored.MovingTime);
        Assert.Equal(original.Type, stored.Type);
        Assert.NotNull(stored.HeartRate);
        Assert.True(original.HeartRate!.Samples.SequenceEqual(stored.HeartRate.Samples));
    }

    // T045: the assertion the InMemory provider cannot make. A domain-level round trip passes
    // against several wrong mappings; only reading the raw columns pins the stored form.
    [Fact]
    public async Task TheStoredColumnsHoldTicksAndOffsetMinutesRatherThanText()
    {
        using var fixture = NewFixture();

        await using (var writing = fixture.NewContext())
        {
            await new ActivityStore(writing).UpsertAsync(A1(), false, TestContext.Current.CancellationToken);
        }

        Assert.Equal("Strava", fixture.Scalar("SELECT Provider FROM Activities"));
        Assert.Equal("11000000001", fixture.Scalar("SELECT ExternalId FROM Activities"));
        Assert.Equal(
            new DateTimeOffset(2026, 9, 10, 7, 30, 0, TimeSpan.FromHours(3)).UtcTicks,
            fixture.Scalar("SELECT StartedAtUtcTicks FROM Activities"));
        Assert.Equal(180L, fixture.Scalar("SELECT StartedAtOffsetMinutes FROM Activities"));
        Assert.Equal(TimeSpan.FromSeconds(3120).Ticks, fixture.Scalar("SELECT MovingTimeTicks FROM Activities"));
        Assert.Equal("Running", fixture.Scalar("SELECT Type FROM Activities"));
    }

    // T046: C53 across awkward offsets. Non-hour offsets are where a naive minutes conversion breaks.
    [Theory]
    [InlineData(5, 45)]
    [InlineData(-4, 0)]
    [InlineData(0, 0)]
    public async Task AnyOffsetRoundTripsExactlyIncludingASingleTick(int hours, int minutes)
    {
        using var fixture = NewFixture();
        var offset = new TimeSpan(hours, minutes, 0);
        var started = new DateTimeOffset(2026, 3, 15, 7, 30, 12, offset).AddTicks(1);
        var original = new TrainingActivity("a-1", started, TimeSpan.FromMinutes(52), ActivityType.Cycling);

        await using (var writing = fixture.NewContext())
        {
            await new ActivityStore(writing).UpsertAsync(original, false, TestContext.Current.CancellationToken);
        }

        await using var reading = fixture.NewContext();
        var stored = (await new ActivityStore(reading).InRangeAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, TestContext.Current.CancellationToken)).Single();

        Assert.True(started.EqualsExact(stored.StartedAt));
    }

    // T047: FR-023, FR-030, C54 — storing the same activity twice leaves one row.
    [Fact]
    public async Task StoringTheSameActivityTwiceLeavesExactlyOneRow()
    {
        using var fixture = NewFixture();

        await using var db = fixture.NewContext();
        var store = new ActivityStore(db);
        await store.UpsertAsync(A1(), false, TestContext.Current.CancellationToken);
        await store.UpsertAsync(A1(), false, TestContext.Current.CancellationToken);

        Assert.Equal(1L, fixture.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T049: C55, a discriminating check. Two activities identical in every domain value but their
    // id are two rows. An implementation that deduplicated on the session's values would pass every
    // other test in this file.
    [Fact]
    public async Task TwoActivitiesAlikeInEveryValueButTheirIdAreTwoRows()
    {
        using var fixture = NewFixture();
        var started = new DateTimeOffset(2026, 9, 10, 7, 30, 0, TimeSpan.FromHours(3));

        await using var db = fixture.NewContext();
        var store = new ActivityStore(db);
        await store.UpsertAsync(
            new TrainingActivity("id-a", started, TimeSpan.FromMinutes(52), ActivityType.Running),
            false, TestContext.Current.CancellationToken);
        await store.UpsertAsync(
            new TrainingActivity("id-b", started, TimeSpan.FromMinutes(52), ActivityType.Running),
            false, TestContext.Current.CancellationToken);

        Assert.Equal(2L, fixture.Scalar("SELECT COUNT(*) FROM Activities"));
    }

    // T050: FR-024, C53 for the series, including a sub-tick sample offset.
    [Fact]
    public async Task EveryHeartRateSampleSurvivesStorageExactly()
    {
        using var fixture = NewFixture();
        var original = A1(S3());

        await using (var writing = fixture.NewContext())
        {
            await new ActivityStore(writing).UpsertAsync(original, false, TestContext.Current.CancellationToken);
        }

        await using var reading = fixture.NewContext();
        var stored = (await new ActivityStore(reading).InRangeAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, TestContext.Current.CancellationToken)).Single();

        Assert.Equal(3, stored.HeartRate!.Samples.Count);
        Assert.True(original.HeartRate!.Samples.SequenceEqual(stored.HeartRate.Samples));
        Assert.Equal(
            TimeSpan.FromSeconds(60) + TimeSpan.FromTicks(1234),
            stored.HeartRate.Samples[2].TimeFromStart);
    }

    // T052: a session with no series stores SQL NULL, asserted against the raw column — a JSON
    // string "null" would read back the same way through the object.
    [Fact]
    public async Task ASessionWithNoSeriesStoresSqlNull()
    {
        using var fixture = NewFixture();

        await using var db = fixture.NewContext();
        await new ActivityStore(db).UpsertAsync(A1(), false, TestContext.Current.CancellationToken);

        Assert.Equal(1L, fixture.Scalar("SELECT COUNT(*) FROM Activities WHERE HeartRateJson IS NULL"));
    }

    // T054: FR-021, C58 — load is derived on read by the domain, never stored here.
    [Fact]
    public void NoLoadValueIsStoredAnywhereInTheRow()
    {
        var columns = typeof(ActivityRow).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(columns, c => c.Contains("Load", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("Trimp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("Points", StringComparison.OrdinalIgnoreCase));
    }

    // T053: C57 — a corrupted row throws at the store's boundary, on the caller's stack, rather
    // than from inside EF Core's materializer. This is what justified ActivityRow over direct
    // mapping (research R4).
    [Fact]
    public async Task ACorruptedSeriesThrowsAtTheStoreRatherThanInsideTheQuery()
    {
        using var fixture = NewFixture();

        await using var db = fixture.NewContext();
        await new ActivityStore(db).UpsertAsync(A1(S3()), false, TestContext.Current.CancellationToken);
        fixture.Execute("UPDATE Activities SET HeartRateJson = '[[0,5],[300000000,9]]'");

        await using var reading = fixture.NewContext();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => new ActivityStore(reading).InRangeAsync(
                DateTimeOffset.MinValue, DateTimeOffset.MaxValue, TestContext.Current.CancellationToken));
    }
}
