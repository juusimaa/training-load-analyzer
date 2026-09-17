using System.Text.Json;
using TrainingLoadAnalyzer.Domain;

namespace TrainingLoadAnalyzer.Infrastructure.Persistence;

/// <summary>
///   The stored form of one imported session. An infrastructure type, never returned to a caller:
///   it converts to and from <see cref="TrainingActivity"/> at the boundary.
/// </summary>
/// <remarks>
///   <para>
///     <see cref="TrainingActivity"/> is deliberately <em>not</em> mapped directly. EF Core binds
///     constructor parameters to properties by name, and its fourth parameter is
///     <c>activityType</c> against a <c>Type</c> property, which fails model build with "No suitable
///     constructor was found" and has no Fluent API fix. The alternatives were renaming a parameter
///     in a signed-off domain type because a database library wants it, or binding through a shadow
///     property that makes <c>Type</c> invisible to LINQ (research R4).
///   </para>
///   <para>
///     The instant is stored as ticks plus offset-minutes rather than as a
///     <see cref="DateTimeOffset"/>. Fidelity is not the reason — that round-trips exactly — but
///     SQLite stores it as text, so <c>OrderBy</c> throws and a range <c>Where</c> will not
///     translate. Every query this feature makes is a range over start time (research R5).
///   </para>
/// </remarks>
public sealed class ActivityRow
{
    /// <summary>Which provider <see cref="ExternalId"/> belongs to. Half of the identity (FR-014).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The provider's own identifier, verbatim: unparsed, untrimmed, unprefixed (FR-014).</summary>
    public string ExternalId { get; set; } = string.Empty;

    public long StartedAtUtcTicks { get; set; }

    public short StartedAtOffsetMinutes { get; set; }

    public long MovingTimeTicks { get; set; }

    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///   The heart-rate series as <c>[[ticks, bpm], …]</c>, or SQL NULL when none is held. A child
    ///   table is not available: <see cref="HeartRateSample"/> is a readonly record struct and
    ///   <c>OwnsMany</c> requires a reference type (research R6).
    /// </summary>
    public string? HeartRateJson { get; set; }

    /// <summary>
    ///   A series was owed and could not be fetched (FR-017d). Distinct from a null
    ///   <see cref="HeartRateJson"/> with this false, which means no series exists to fetch — the
    ///   difference decides whether a later sync comes back for it (FR-017e).
    /// </summary>
    public bool HeartRateOutstanding { get; set; }

    public static ActivityRow From(TrainingActivity activity, string provider, bool heartRateOutstanding)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return new ActivityRow
        {
            Provider = provider,
            ExternalId = activity.ExternalId,
            StartedAtUtcTicks = activity.StartedAt.UtcTicks,
            StartedAtOffsetMinutes = (short)activity.StartedAt.Offset.TotalMinutes,
            MovingTimeTicks = activity.MovingTime.Ticks,
            Type = activity.Type.ToString(),
            HeartRateJson = Serialise(activity.HeartRate),
            HeartRateOutstanding = heartRateOutstanding,
        };
    }

    /// <summary>
    ///   Rebuilds the domain session, running the real constructors so the domain's invariants are
    ///   re-checked on load. A corrupted row therefore throws here, at the store's boundary and on
    ///   the caller's stack, rather than from inside EF Core's materializer (C57, research R4).
    /// </summary>
    public TrainingActivity ToDomain() => new(
        ExternalId,
        // Reconstructed in two steps deliberately: the shorter
        // `new DateTimeOffset(utcTicks + offset.Ticks, offset)` overflows at the extremes of the
        // range (research R5).
        new DateTimeOffset(new DateTime(StartedAtUtcTicks, DateTimeKind.Utc))
            .ToOffset(TimeSpan.FromMinutes(StartedAtOffsetMinutes)),
        TimeSpan.FromTicks(MovingTimeTicks),
        Enum.Parse<ActivityType>(Type),
        Deserialise(HeartRateJson));

    private static string? Serialise(HeartRateSeries? series) =>
        series is null
            ? null
            : JsonSerializer.Serialize(
                series.Samples.Select(s => new long[] { s.TimeFromStart.Ticks, s.Bpm }));

    private static HeartRateSeries? Deserialise(string? json)
    {
        if (json is null)
        {
            return null;
        }

        var pairs = JsonSerializer.Deserialize<long[][]>(json)
            ?? throw new InvalidOperationException("A stored heart-rate series could not be read.");

        return new HeartRateSeries(
            [.. pairs.Select(p => new HeartRateSample(TimeSpan.FromTicks(p[0]), (int)p[1]))]);
    }
}
