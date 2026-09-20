using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrainingLoadAnalyzer.Infrastructure.Persistence;

namespace TrainingLoadAnalyzer.Web.Features.Dashboard;

/// <summary>
///   Reads the stored history once and hands it to <see cref="DashboardViewBuilder"/>.
/// </summary>
/// <remarks>
///   <para>
///     One read serves all five sections. The obvious shape is a query per section — the recent
///     seven, the current week, the last 180 days — but the headline figures need the
///     <em>whole</em> history regardless: fitness is an exponential accumulation from the first
///     recorded day, and <c>TrainingMetricsCalculator</c> refuses a history that does not reach
///     back far enough rather than assuming rest. Once it is all in memory a second query would be
///     a second code path returning a subset of what was already read (research R15).
///   </para>
///   <para>
///     The context comes from the factory and is disposed per call. A Blazor Interactive Server
///     circuit is one DI scope for the life of the connection, so a scoped context injected into a
///     component would be a change-tracking cache that never resets and is shared by every
///     concurrent render on that circuit (research R12, C86).
///   </para>
/// </remarks>
public sealed class DashboardReader(
    IDbContextFactory<ImportDbContext> factory,
    TimeProvider clock,
    AthleteSettings settings,
    ILogger<DashboardReader> logger)
{
    public async Task<DashboardView> ReadAsync(CancellationToken cancellationToken)
    {
        // The athlete's local day, not UTC. Feature 002 buckets an activity by the local day at its
        // own recorded offset, so asking in UTC would put an evening session in a different week
        // from the one the athlete just finished (research R22, C88).
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var connected = await db.Connections.AnyAsync(cancellationToken);

        try
        {
            var store = new ActivityStore(db);

            var activities = await store.InRangeAsync(
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.MaxValue,
                cancellationToken);

            return DashboardViewBuilder.Build(activities, today, settings.MaximumHeartRate, connected);
        }
        catch (Exception failure) when (failure is ArgumentException
                                            or InvalidOperationException
                                            or JsonException
                                            or FormatException
                                            or OverflowException)
        {
            // Named types rather than a bare catch, and OperationCanceledException deliberately
            // absent: a cancelled request is not a corrupt database, and swallowing it would hide
            // a shutdown as a data problem. Logged rather than only displayed - the athlete sees
            // "Data unavailable", but nobody could act on that without the reason (Principle VI).
            logger.LogError(
                failure,
                "The stored training history could not be read, so the dashboard is showing its "
                    + "unavailable state.");

            return new DashboardView
            {
                AsOf = today,
                IsStravaConnected = connected,
                IsUnavailable = true,

                // The rail renders either side of this branch, so both members it needs are
                // populated here too. Neither depends on the history that could not be read.
                MaximumHeartRate = settings.MaximumHeartRate,
                IsoWeek = DashboardViewBuilder.Designation(today),
            };
        }
    }
}
