using Microsoft.EntityFrameworkCore;

namespace TrainingLoadAnalyzer.Infrastructure.Persistence;

/// <summary>The analyzer's local store: one athlete's connection and, later, their sessions.</summary>
public sealed class ImportDbContext(DbContextOptions<ImportDbContext> options) : DbContext(options)
{
    public DbSet<StravaConnection> Connections => Set<StravaConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StravaConnection>(connection =>
        {
            // The athlete's own Strava id is the key, so a second exchange for the same athlete
            // replaces rather than accumulates (FR-008).
            connection.HasKey(c => c.AthleteId);
            connection.Property(c => c.AccessToken).IsRequired();
            connection.Property(c => c.RefreshToken).IsRequired();
            connection.Property(c => c.GrantedScopes).IsRequired();
        });
    }
}
