using Microsoft.EntityFrameworkCore;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests.Fakes;

/// <summary>
///   Lets a test drive <c>DashboardReader</c> against feature 005's <see cref="SqliteFixture{T}"/>
///   with no host at all — real SQLite, no HTTP, no DI container.
/// </summary>
internal sealed class FixtureContextFactory(SqliteFixture<ImportDbContext> fixture)
    : IDbContextFactory<ImportDbContext>
{
    public ImportDbContext CreateDbContext() => fixture.NewContext();
}
