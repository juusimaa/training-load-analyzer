using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests.Fakes;

/// <summary>
///   The application, running in process, against a database and a Strava that the test controls.
/// </summary>
/// <remarks>
///   <para>
///     Used for what only an HTTP request can reach: the two connect endpoints and the statically
///     rendered page. Interactive behaviour is covered by bUnit instead, because
///     <see cref="WebApplicationFactory{T}"/> cannot drive a SignalR circuit (research R18).
///   </para>
///   <para>
///     The SQLite connection is opened here and <strong>kept open</strong> for the factory's
///     lifetime, and the connection <em>object</em> is handed to <c>UseSqlite</c>. Feature 005's
///     research R8 established why both matter: given a connection string EF Core opens and closes
///     one per operation, so the schema is created and the database then evaporates.
///   </para>
/// </remarks>
internal sealed class WebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection;

    public WebAppFactory()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var context = NewContext();
        context.Database.Migrate();
    }

    /// <summary>The clock every test in this feature shares (tasks.md, "The clock").</summary>
    public FixedLocalClock Clock { get; } = new();

    /// <summary>Stands in for Strava. Records every request, so a test can assert none was made.</summary>
    public StubHttpMessageHandler Strava { get; } = new();

    /// <summary>A context over the same database, for seeding and for reading back.</summary>
    public ImportDbContext NewContext() => new(
        new DbContextOptionsBuilder<ImportDbContext>().UseSqlite(connection).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Athlete:MaximumHeartRate", "190");
        builder.UseSetting("Strava:ClientId", "test-client-id");
        builder.UseSetting("Strava:ClientSecret", "test-client-secret");
        builder.UseSetting("ConnectionStrings:Import", "DataSource=:memory:");

        builder.ConfigureTestServices(services =>
        {
            // The host's own registration points at a file; this one points at the open
            // connection above. Removing the options descriptors first is what makes the
            // replacement take rather than sit alongside.
            services.RemoveAll<DbContextOptions<ImportDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextFactory<ImportDbContext>>();
            services.RemoveAll<ImportDbContext>();
            services.AddDbContextFactory<ImportDbContext>(options => options.UseSqlite(connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            // Both clients: the named one StravaAuthorization uses, and the typed one
            // StravaApiClient uses. Replacing only one leaves a real outbound call in the other.
            services.AddHttpClient("Strava").ConfigurePrimaryHttpMessageHandler(() => Strava);
            services.AddHttpClient<TrainingLoadAnalyzer.Infrastructure.Strava.StravaApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() => Strava);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            connection.Dispose();
            Strava.Dispose();
        }
    }
}
