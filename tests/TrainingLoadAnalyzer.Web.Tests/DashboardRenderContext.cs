using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Strava;
using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;
using TrainingLoadAnalyzer.Web.Components.Pages;
using TrainingLoadAnalyzer.Web.Features.Dashboard;
using TrainingLoadAnalyzer.Web.Features.Sync;
using TrainingLoadAnalyzer.Web.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   A rendered dashboard over a real SQLite database, a real reader and a real coordinator.
/// </summary>
/// <remarks>
///   <para>
///     Feature 008, research R11. This block existed twice before — once in
///     <see cref="DashboardComponentTests"/> and once in <see cref="InformationPreservationTests"/>
///     — and both copies had to change together whenever the container did. Feature 008 changes it
///     twice more (the component library's services come out, and the chart's JS-measurement fake
///     with them) and adds new test classes that would otherwise have made a third and fourth copy.
///     A concrete, current duplication, not an anticipated one (Principle III).
///   </para>
///   <para>
///     Test-side only. No production abstraction is introduced, and nothing here decides anything
///     the tests were not already deciding for themselves.
///   </para>
/// </remarks>
public abstract class DashboardRenderContext : BunitContext
{
    /// <summary>
    ///   Loose interop, and nothing else.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Feature 008 removed three lines from here, which is the return on extracting this base:
    ///     the component library's service registration and the JS measurement fake its chart
    ///     needed went out in one place rather than in every test class that rendered a page.
    ///   </para>
    ///   <para>
    ///     Loose mode stays for the framework's own calls. Nothing this application renders calls
    ///     into JavaScript any more — the chart is markup and the appearance is a media query —
    ///     so there is nothing left for a strict mode to catch.
    ///   </para>
    /// </remarks>
    protected DashboardRenderContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    ///   The database the page reads, exposed so a test can corrupt or connect it.
    /// </summary>
    /// <remarks>
    ///   <c>private protected</c> rather than <c>protected</c>: the fixture and the clock are both
    ///   internal test doubles, and a protected member of a public class may not be less accessible
    ///   than its own type.
    /// </remarks>
    private protected SqliteFixture<ImportDbContext> Fixture { get; } =
        new(options => new ImportDbContext(options));

    /// <summary>The athlete's day and offset, pinned in both respects (research R22).</summary>
    private protected FixedLocalClock Clock { get; } = new();

    /// <summary>
    ///   Seeds the history and registers the page's one dependency. The page fetches; the panels
    ///   below it take a <c>DashboardView</c> and reach for nothing (C102).
    /// </summary>
    protected void SeedAndRegister(params TrainingActivity[] activities)
    {
        using (var db = Fixture.NewContext())
        {
            var store = new ActivityStore(db);

            foreach (var activity in activities)
            {
                store.UpsertAsync(activity, false, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        Services.AddSingleton<TimeProvider>(Clock);
        Services.AddSingleton(new AthleteSettings(Fixtures.MaximumHeartRate));
        Services.AddSingleton<IDbContextFactory<ImportDbContext>>(new FixtureContextFactory(Fixture));
        Services.AddSingleton(sp => new DashboardReader(
            sp.GetRequiredService<IDbContextFactory<ImportDbContext>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<AthleteSettings>(),
            NullLogger<DashboardReader>.Instance));

        // The coordinator's own chain. bUnit's provider supplies IServiceScopeFactory itself, so
        // the coordinator creates a real scope per run exactly as it does in the host.
        Services.AddSingleton(new StravaApiClient(new HttpClient(new StubHttpMessageHandler())));
        Services.AddScoped<ActivityStore>();
        Services.AddScoped(sp => Fixture.NewContext());
        Services.AddScoped<StravaActivitySync>();
        Services.AddSingleton<SyncCoordinator>();
    }

    /// <summary>Seeds the history and renders the page over it.</summary>
    protected IRenderedComponent<Dashboard> RenderDashboard(params TrainingActivity[] activities)
    {
        SeedAndRegister(activities);

        return Render<Dashboard>();
    }

    /// <summary>Records a connected Strava account, which selects the other empty-state branch.</summary>
    protected void Connect()
    {
        using var db = Fixture.NewContext();

        db.Connections.Add(new StravaConnection
        {
            AthleteId = 900001,
            AccessToken = "a",
            RefreshToken = "r",
            GrantedScopes = "activity:read_all",
        });

        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Fixture.Dispose();
        }

        base.Dispose(disposing);
    }
}
