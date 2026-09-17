using Microsoft.EntityFrameworkCore;

namespace TrainingLoadAnalyzer.Infrastructure.Persistence;

/// <summary>The analyzer's local store: one athlete's connection and, later, their sessions.</summary>
public sealed class ImportDbContext(DbContextOptions<ImportDbContext> options) : DbContext(options)
{
    public DbSet<StravaConnection> Connections => Set<StravaConnection>();

    public DbSet<ActivityRow> Activities => Set<ActivityRow>();

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

        modelBuilder.Entity<ActivityRow>(activity =>
        {
            // FR-023, C55: identity is the provider plus the provider's own id, and nothing else.
            // Nothing about the session's start, duration or type participates, so two activities
            // that happen to coincide are two rows.
            activity.HasKey(a => new { a.Provider, a.ExternalId });

            // Every query this feature makes is a range over start time: the resume point (FR-027),
            // the reconciliation span (FR-031), the measured window (FR-017a).
            activity.HasIndex(a => a.StartedAtUtcTicks);

            activity.Property(a => a.Type).IsRequired();
        });
    }
}
