using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Web;
using TrainingLoadAnalyzer.Web.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   The host's own behaviour: the configuration it refuses to start without, the services it
///   registers, and that it serves a page at all.
/// </summary>
public class StartupTests
{
    private static IConfiguration ConfigurationWith(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    /// <summary>
    ///   FR-015, C77: the application does not start and then display figures computed from a
    ///   maximum heart rate nobody chose. The message names the key, because a refusal that does
    ///   not say what is missing is a puzzle rather than an error.
    /// </summary>
    [Fact]
    public void A_missing_maximum_heart_rate_refuses_to_start()
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => AthleteSettings.From(ConfigurationWith()));

        Assert.Contains("Athlete:MaximumHeartRate", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///   C77's boundaries. A non-positive maximum must not reach
    ///   <c>TrainingActivity.CalculateTrainingLoad</c>, whose own
    ///   <see cref="ArgumentOutOfRangeException"/> would surface as a broken page rather than as
    ///   the configuration error it is (research R4, R19).
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not a number")]
    [InlineData("")]
    public void A_maximum_heart_rate_that_is_not_a_positive_number_refuses_to_start(string configured)
    {
        Assert.Throws<InvalidOperationException>(
            () => AthleteSettings.From(ConfigurationWith((AthleteSettings.Key, configured))));
    }

    [Fact]
    public void A_positive_maximum_heart_rate_is_accepted()
    {
        var settings = AthleteSettings.From(ConfigurationWith((AthleteSettings.Key, "190")));

        Assert.Equal(190, settings.MaximumHeartRate);
    }

    /// <summary>
    ///   C73: the dashboard responds with a usable page whether or not Strava is reachable and
    ///   whether or not an account is connected. An athlete who has just installed this has none of
    ///   those things, and a 500 would be their first impression.
    /// </summary>
    [Fact]
    public async Task The_dashboard_responds_against_an_empty_database_with_no_connection()
    {
        await using var app = new WebAppFactory();
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    ///   C86, research R12. One <c>AddDbContextFactory</c> call supplies both the factory the
    ///   components read through and the scoped context <c>ActivityStore</c> and
    ///   <c>StravaActivitySync</c> take by constructor — and adding <c>AddDbContext</c> alongside it
    ///   breaks the container at build time, which is why <c>Program.cs</c> has exactly one call.
    /// </summary>
    [Fact]
    public void One_factory_registration_supplies_both_a_factory_and_a_scoped_context()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<ImportDbContext>(o => o.UseSqlite("DataSource=:memory:"));

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IDbContextFactory<ImportDbContext>>());
        Assert.NotNull(scope.ServiceProvider.GetService<ImportDbContext>());
    }

    /// <summary>
    ///   Research R20: feature 005 generated three migrations, so they are applied rather than
    ///   bypassed. <c>EnsureCreated</c> would build a schema that then cannot be migrated.
    /// </summary>
    [Fact]
    public async Task Startup_applies_feature_005s_migrations()
    {
        await using var app = new WebAppFactory();
        using var client = app.CreateClient();
        using var _ = await client.GetAsync("/", TestContext.Current.CancellationToken);

        await using var db = app.NewContext();

        Assert.Empty(await db.Activities.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Connections.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.SyncStates.ToListAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///   SC-006 and C73: the stored figures reach the page while Strava is unreachable. The stub
    ///   handler has no rule for any request, so it throws on one — a network call here fails the
    ///   test rather than quietly succeeding.
    /// </summary>
    [Fact]
    public async Task With_strava_unreachable_the_stored_figures_still_render()
    {
        await using var app = new WebAppFactory();

        await using (var db = app.NewContext())
        {
            var store = new ActivityStore(db);

            foreach (var activity in Fixtures.H1)
            {
                await store.UpsertAsync(activity, false, TestContext.Current.CancellationToken);
            }
        }

        using var client = app.CreateClient();
        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("2.8", html, StringComparison.Ordinal);
        Assert.Contains("16.0", html, StringComparison.Ordinal);
    }

    /// <summary>
    ///   FR-013 and C72: a dashboard load consults nothing outside the local database. The stub
    ///   records every request it is given, so "none" is assertable rather than merely intended.
    /// </summary>
    [Fact]
    public async Task A_dashboard_load_makes_no_outbound_request()
    {
        await using var app = new WebAppFactory();
        using var client = app.CreateClient();

        using var _ = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Empty(app.Strava.Requests);
    }
}
