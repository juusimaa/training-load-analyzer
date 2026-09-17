using Microsoft.EntityFrameworkCore;
using TrainingLoadAnalyzer.Domain;

namespace TrainingLoadAnalyzer.Infrastructure.Persistence;

/// <summary>Reads and writes imported sessions (FR-022, FR-023, FR-030).</summary>
public sealed class ActivityStore(ImportDbContext db)
{
    /// <summary>The only provider this feature imports from. Recorded, not abstracted over.</summary>
    public const string Strava = "Strava";

    /// <summary>
    ///   Inserts or overwrites by <c>(Provider, ExternalId)</c>, so importing the same activity twice
    ///   leaves exactly one row (FR-023, FR-030, C54).
    /// </summary>
    public async Task UpsertAsync(
        TrainingActivity activity,
        bool heartRateOutstanding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var incoming = ActivityRow.From(activity, Strava, heartRateOutstanding);
        var existing = await db.Activities.FindAsync(
            [Strava, activity.ExternalId],
            cancellationToken);

        if (existing is null)
        {
            db.Activities.Add(incoming);
        }
        else
        {
            existing.StartedAtUtcTicks = incoming.StartedAtUtcTicks;
            existing.StartedAtOffsetMinutes = incoming.StartedAtOffsetMinutes;
            existing.MovingTimeTicks = incoming.MovingTimeTicks;
            existing.Type = incoming.Type;
            existing.HeartRateOutstanding = incoming.HeartRateOutstanding;

            // FR-017b, C56: a stored series is never discarded or replaced. The measured window
            // governs what is newly retrieved, never what is kept, so an athlete's measured history
            // accumulates rather than rolling forward.
            existing.HeartRateJson ??= incoming.HeartRateJson;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///   Sessions whose start falls inside the range, ascending. A SQL range query, which is what
    ///   the ticks column exists for (research R5).
    /// </summary>
    public async Task<IReadOnlyList<TrainingActivity>> InRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var rows = await db.Activities
            .Where(r => r.StartedAtUtcTicks >= from.UtcTicks && r.StartedAtUtcTicks <= to.UtcTicks)
            .OrderBy(r => r.StartedAtUtcTicks)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => r.ToDomain())];
    }
}
