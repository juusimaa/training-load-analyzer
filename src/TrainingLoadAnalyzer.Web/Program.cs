using Microsoft.EntityFrameworkCore;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Strava;
using TrainingLoadAnalyzer.Web;
using TrainingLoadAnalyzer.Web.Components;
using TrainingLoadAnalyzer.Web.Features.Dashboard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Read before anything else is registered, so an application without a maximum heart rate fails
// here rather than on the first page load with a broken figure on it (FR-015, C77).
builder.Services.AddSingleton(AthleteSettings.From(builder.Configuration));

// Exactly one registration, and deliberately not AddDbContext alongside it. Verified by running
// it: the pair fails at container build with "Cannot resolve scoped service
// IEnumerable<IDbContextOptionsConfiguration<ImportDbContext>> from root provider". This single
// call already supplies both the singleton factory the components use and the scoped context
// ActivityStore and StravaActivitySync take by constructor (research R12).
builder.Services.AddDbContextFactory<ImportDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Import")));

builder.Services.AddSingleton(new StravaCredentials(
    builder.Configuration["Strava:ClientId"] ?? string.Empty,
    builder.Configuration["Strava:ClientSecret"] ?? string.Empty));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<StravaApiClient>();

// Scoped: it creates and disposes its own context per read, so it holds nothing across a circuit.
builder.Services.AddScoped<DashboardReader>();

var app = builder.Build();

// Feature 005 generated three migrations, so they are applied rather than bypassed. The
// create-if-not-exists shortcut would build a schema that then cannot be migrated, and the two do
// not mix (005 R7, research R20).
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ImportDbContext>().Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>
///   Named so <c>WebApplicationFactory&lt;Program&gt;</c> can bind to it. Top-level statements
///   generate an internal <c>Program</c>, which the test project cannot reach.
/// </summary>
public partial class Program;
