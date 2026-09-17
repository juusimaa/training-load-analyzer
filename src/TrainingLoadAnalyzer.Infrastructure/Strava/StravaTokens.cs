using System.Text.Json.Serialization;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   Strava's response to a token request, whether an exchange or a renewal.
/// </summary>
/// <remarks>
///   Unrecognised members are ignored, so a field Strava adds later cannot fail an exchange
///   (FR-020). This type never leaves the integration.
/// </remarks>
public sealed record StravaTokens
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>Epoch <em>seconds</em> at which the access token expires — not a duration.</summary>
    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; init; }

    /// <summary>
    ///   What Strava actually granted, which may be narrower than what was requested if the athlete
    ///   unticked a scope on the consent page (FR-002a). Absent on a renewal response.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    /// <summary>Present on an exchange, absent on a renewal.</summary>
    [JsonPropertyName("athlete")]
    public StravaAthlete? Athlete { get; init; }

    public sealed record StravaAthlete
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }
    }
}
