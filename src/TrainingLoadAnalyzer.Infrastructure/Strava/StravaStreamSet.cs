using System.Text.Json.Serialization;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   The response to a streams request with <c>keys=time,heartrate</c> and <c>key_by_type=true</c>:
///   an object keyed by stream type, each carrying a <c>data</c> array.
/// </summary>
/// <remarks>
///   <para>
///     A stream Strava does not have is an <strong>absent key</strong>, not a null entry — asking for
///     <c>time,heartrate</c> on an activity without heart rate returns an object containing only
///     <c>time</c>. Both properties are therefore nullable and must be key-checked, never indexed
///     (research R20).
///   </para>
///   <para>
///     Sampling is not 1 Hz. Strava's streams are irregularly spaced because of recording gaps and
///     smart recording, so <see cref="Time"/> is load-bearing data rather than an index. This suits
///     the load calculation exactly as feature 001 built it: each gap is charged to the sample
///     preceding it.
///   </para>
/// </remarks>
public sealed record StravaStreamSet
{
    /// <summary>Seconds elapsed since the activity started.</summary>
    [JsonPropertyName("time")]
    public StravaStream? Time { get; init; }

    /// <summary>Beats per minute, index-aligned with <see cref="Time"/>.</summary>
    [JsonPropertyName("heartrate")]
    public StravaStream? Heartrate { get; init; }
}

/// <summary>One stream's data.</summary>
public sealed record StravaStream
{
    [JsonPropertyName("data")]
    public IReadOnlyList<int> Data { get; init; } = [];
}
