using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrainingLoadAnalyzer.Infrastructure.Persistence;

/// <summary>
///   Used only by <c>dotnet ef</c> when generating migrations. It never runs at runtime, and the
///   database it names is never created — the tool needs a provider to generate SQL against, nothing
///   more.
/// </summary>
internal sealed class DesignTimeImportDbContextFactory : IDesignTimeDbContextFactory<ImportDbContext>
{
    public ImportDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite("DataSource=design-time-only.db")
            .Options;

        return new ImportDbContext(options);
    }
}
