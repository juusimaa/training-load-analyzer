using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;
using TrainingLoadAnalyzer.Web.Features.Dashboard;
using TrainingLoadAnalyzer.Web.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   The one read: stored activities become the figures the page shows. Real SQLite, no host — the
///   InMemory provider was rejected on evidence in feature 005 (005 R8) and nothing has changed.
/// </summary>
public sealed class DashboardReaderTests : IDisposable
{
    private readonly SqliteFixture<ImportDbContext> fixture =
        new(options => new ImportDbContext(options));

    private readonly FixedLocalClock clock = new();

    public void Dispose() => fixture.Dispose();

    private DashboardReader Reader() => new(
        new FixtureContextFactory(fixture),
        clock,
        new AthleteSettings(Fixtures.MaximumHeartRate),
        NullLogger<DashboardReader>.Instance);

    private async Task SeedAsync(params TrainingActivity[] activities)
    {
        await using var db = fixture.NewContext();
        var store = new ActivityStore(db);

        foreach (var activity in activities)
        {
            await store.UpsertAsync(activity, false, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    ///   FR-013: the figures come from the local store. The same expectations as
    ///   <c>DashboardViewBuilderTests</c>, reached through storage rather than from memory.
    /// </summary>
    [Fact]
    public async Task Stored_activities_become_the_dashboards_figures()
    {
        await SeedAsync([.. Fixtures.H1]);

        var view = await Reader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(view.Current);
        Assert.Equal(2.8233976017308082, view.Current.Value.Fitness, 12);
        Assert.Equal(15.974652029978209, view.Current.Value.Fatigue, 12);
    }

    /// <summary>
    ///   C86: each read gets its own context. Two consecutive reads share no change-tracking state,
    ///   so a dashboard reloaded after a sync sees what was written rather than what was cached.
    /// </summary>
    [Fact]
    public async Task Each_read_sees_what_was_written_since_the_last_one()
    {
        var reader = Reader();
        await SeedAsync([.. Fixtures.H1]);

        var before = await reader.ReadAsync(TestContext.Current.CancellationToken);

        await SeedAsync(Fixtures.Session(Fixtures.Today, 90, id: "added-later"));

        var after = await reader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(before.Current);
        Assert.NotNull(after.Current);
        Assert.True(after.Current.Value.Fatigue > before.Current.Value.Fatigue);
    }

    /// <summary>
    ///   Discriminating check for C88 and research R22. At 22:00Z the athlete at +03:00 is already
    ///   on the next day, so "today" is 2026-09-19. An implementation reading <c>GetUtcNow</c>
    ///   answers 2026-09-18 — the defect that puts an evening session in the wrong ISO week, and
    ///   only for evening sessions.
    /// </summary>
    [Fact]
    public async Task Today_is_the_athletes_local_day_not_the_utc_one()
    {
        clock.AdvanceTo(new DateTimeOffset(2026, 9, 18, 22, 0, 0, TimeSpan.Zero));
        await SeedAsync([.. Fixtures.H1]);

        var view = await Reader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new DateOnly(2026, 9, 19), view.AsOf);
    }

    /// <summary>
    ///   C87 and the "corrupted or malformed" edge case. Fixture H4: a row whose sport no enum value
    ///   matches. Feature 005's <c>ActivityRow.ToDomain</c> re-runs the domain constructors on load
    ///   deliberately, so a bad row throws at the store's boundary (005 C57) — and the page must say
    ///   "Data unavailable" rather than showing a stack trace.
    /// </summary>
    [Fact]
    public async Task A_row_that_cannot_be_read_yields_an_unavailable_view_rather_than_throwing()
    {
        await SeedAsync([.. Fixtures.H1]);
        fixture.Execute("UPDATE Activities SET Type = 'Swimming'");

        var view = await Reader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.True(view.IsUnavailable);
        Assert.Null(view.Current);
    }
}
