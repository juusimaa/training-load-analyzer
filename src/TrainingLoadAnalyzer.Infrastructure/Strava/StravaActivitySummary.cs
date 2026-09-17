using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   One activity as Strava's list endpoint describes it. Lives only inside the integration and is
///   never stored in this form (FR-018).
/// </summary>
/// <remarks>
///   <para>
///     <see cref="UtcOffset"/> and <see cref="HasHeartrate"/> appear only in Strava's example
///     payloads and in no published schema, so both are treated as optional-by-default even though
///     real responses always carry them (research R15).
///   </para>
///   <para>
///     <c>start_date_local</c> is deliberately absent. Strava renders it with a trailing <c>Z</c>
///     while it holds local wall-clock time, which is a trap that yields a plausible and wrong
///     answer. The offset comes from <see cref="UtcOffset"/> instead.
///   </para>
/// </remarks>
public sealed record StravaActivitySummary
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(IdAsStringConverter))]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    ///   Branch on this, never on <c>type</c>. The legacy field is lossy by Strava's own
    ///   documentation — a TrailRun reports <c>type: "Run"</c> and a GravelRide reports
    ///   <c>type: "Ride"</c> — so it would silently collapse the distinctions FR-010 depends on.
    /// </summary>
    [JsonPropertyName("sport_type")]
    public string SportType { get; init; } = string.Empty;

    [JsonPropertyName("start_date")]
    public DateTimeOffset StartDate { get; init; }

    /// <summary>The athlete's offset from UTC, in seconds.</summary>
    [JsonPropertyName("utc_offset")]
    public int UtcOffset { get; init; }

    [JsonPropertyName("moving_time")]
    public int MovingTime { get; init; }

    [JsonPropertyName("elapsed_time")]
    public int ElapsedTime { get; init; }

    [JsonPropertyName("has_heartrate")]
    public bool HasHeartrate { get; init; }

    [JsonPropertyName("manual")]
    public bool Manual { get; init; }

    [JsonPropertyName("private")]
    public bool Private { get; init; }

    [JsonPropertyName("trainer")]
    public bool Trainer { get; init; }

    /// <summary>Strava's id is a JSON number; the analyzer keeps identifiers as opaque text.</summary>
    private sealed class IdAsStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Number
                ? reader.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : reader.GetString() ?? string.Empty;

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}
