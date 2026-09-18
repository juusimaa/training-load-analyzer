using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;
using TrainingLoadAnalyzer.Web.Components.Dashboard;
using TrainingLoadAnalyzer.Web.Components.Pages;
using TrainingLoadAnalyzer.Web.Features.Dashboard;
using TrainingLoadAnalyzer.Web.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   What only rendering can assert: an order, a badge, a rounding, an empty state. Everything with
///   a right answer that is <em>not</em> about markup lives in the read model and is tested without
///   a renderer (research R11).
/// </summary>
/// <remarks>
///   Derives from <c>BunitContext</c>, not <c>TestContext</c>: under xUnit v3 the latter is
///   ambiguous with <c>Xunit.TestContext</c> (CS0104), which every bUnit 1.x tutorial still shows
///   (research R10).
/// </remarks>
public class DashboardComponentTests : BunitContext
{
    private readonly SqliteFixture<ImportDbContext> fixture = new(options => new ImportDbContext(options));

    private readonly FixedLocalClock clock = new();

    /// <summary>
    ///   Registers the page's one dependency over a real SQLite database. The page fetches; the
    ///   five panels below it take a <c>DashboardView</c> and reach for nothing (C102).
    /// </summary>
    private void SeedAndRegister(params TrainingActivity[] activities)
    {
        using (var db = fixture.NewContext())
        {
            var store = new ActivityStore(db);

            foreach (var activity in activities)
            {
                store.UpsertAsync(activity, false, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        Services.AddSingleton<TimeProvider>(clock);
        Services.AddSingleton(new AthleteSettings(Fixtures.MaximumHeartRate));
        Services.AddSingleton<IDbContextFactory<ImportDbContext>>(new FixtureContextFactory(fixture));
        Services.AddSingleton(sp => new DashboardReader(
            sp.GetRequiredService<IDbContextFactory<ImportDbContext>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<AthleteSettings>(),
            NullLogger<DashboardReader>.Instance));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fixture.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>FR-001 - FR-003: a tile shows what it is and what it reads.</summary>
    [Fact]
    public void A_metric_tile_shows_its_label_and_its_figure()
    {
        var tile = Render<MetricTile>(p => p
            .Add(c => c.Label, "Fitness")
            .Add(c => c.Value, 2.8233976017308082));

        Assert.Contains("Fitness", tile.Markup, StringComparison.Ordinal);
        Assert.Contains("2.8", tile.Markup, StringComparison.Ordinal);
    }

    /// <summary>C100, US1 scenario 2: nothing to show is a dash, not a confident zero.</summary>
    [Fact]
    public void A_metric_tile_with_no_figure_shows_a_dash()
    {
        var tile = Render<MetricTile>(p => p.Add(c => c.Label, "Fitness"));

        Assert.Contains(Display.Missing, tile.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0", tile.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   003 FR-014 reaches the page: a figure with fewer than 42 days behind it still carries the
    ///   zero it started from, and must not be presented as though it had settled.
    /// </summary>
    [Fact]
    public void A_figure_that_has_not_warmed_up_says_so()
    {
        var warming = Render<MetricTile>(p => p
            .Add(c => c.Label, "Fitness").Add(c => c.Value, 2.8).Add(c => c.IsReliable, false));

        var settled = Render<MetricTile>(p => p
            .Add(c => c.Label, "Fitness").Add(c => c.Value, 45.3).Add(c => c.IsReliable, true));

        Assert.Contains("settling", warming.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("settling", settled.Markup, StringComparison.Ordinal);
    }

    /// <summary>US1 scenario 1: the three figures fixture H1 produces, on the page.</summary>
    [Fact]
    public void The_dashboard_shows_fitness_fatigue_and_form()
    {
        SeedAndRegister([.. Fixtures.H1]);

        var page = Render<Dashboard>();

        Assert.Contains("Fitness", page.Markup, StringComparison.Ordinal);
        Assert.Contains("2.8", page.Markup, StringComparison.Ordinal);
        Assert.Contains("16.0", page.Markup, StringComparison.Ordinal);
        Assert.Contains("-13.2", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   FR-011 and FR-017: a fresh installation is the ordinary first experience. The guidance is
    ///   a route, not a dead end — the athlete is sent somewhere they can act.
    /// </summary>
    [Fact]
    public void With_no_activities_and_no_connection_the_page_offers_a_way_to_connect()
    {
        SeedAndRegister();

        var page = Render<Dashboard>();

        Assert.Contains("No activities recorded", page.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/connect\"", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   FR-011 with a connection in place: the athlete has already connected, so telling them to
    ///   connect again would be wrong. The next step is a sync.
    /// </summary>
    [Fact]
    public void With_a_connection_but_no_activities_the_page_offers_a_sync()
    {
        using (var db = fixture.NewContext())
        {
            db.Connections.Add(new Infrastructure.Persistence.StravaConnection
            {
                AthleteId = 900001,
                AccessToken = "a",
                RefreshToken = "r",
                GrantedScopes = "activity:read_all",
            });
            db.SaveChanges();
        }

        SeedAndRegister();

        var page = Render<Dashboard>();

        Assert.Contains("No activities recorded", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Sync", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    ///   The "metric calculations fail" edge case, in markup: "Data unavailable", not a stack trace
    ///   and not a page of confident zeroes (C87).
    /// </summary>
    [Fact]
    public void A_history_that_cannot_be_read_shows_data_unavailable_rather_than_tiles()
    {
        SeedAndRegister([.. Fixtures.H1]);
        fixture.Execute("UPDATE Activities SET Type = 'Swimming'");

        var page = Render<Dashboard>();

        Assert.Contains("Data unavailable", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("tile-value", page.Markup, StringComparison.Ordinal);
    }

}
