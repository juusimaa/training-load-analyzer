using System.Net.Http.Json;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   Strava's OAuth endpoints, and nothing else. Knows about a third party and nothing about
///   storage — the half of authorization that belongs in <c>Strava/</c>.
/// </summary>
public sealed class StravaOAuthClient(HttpClient http, StravaCredentials credentials)
{
    private const string AuthorizeUrl = "https://www.strava.com/oauth/authorize";

    private const string TokenUrl = "https://www.strava.com/oauth/token";

    private const string RevokeUrl = "https://www.strava.com/oauth/revoke";

    /// <summary>
    ///   The scopes requested. <c>activity:read_all</c> is what makes activities the athlete has
    ///   marked private visible; without it they are filtered out of every response and the import
    ///   silently under-reports the athlete's training (FR-002, FR-012, C47).
    /// </summary>
    /// <remarks>
    ///   <c>read</c> grants nothing about activities at all — profile and public segments only — so
    ///   it is not a substitute for either activity scope. No <c>:write</c> scope is ever requested:
    ///   this analyzer reads and never writes (C70).
    /// </remarks>
    public const string RequiredScopes = "read,activity:read_all";

    /// <summary>
    ///   Where to send the athlete to grant consent (FR-001). Strava white-lists <c>localhost</c>
    ///   and <c>127.0.0.1</c> as redirect targets, which is what lets feature 6 catch the redirect
    ///   locally; catching it is not this feature's job (research R11).
    /// </summary>
    public Uri BuildAuthorizeUrl(Uri redirectUri, string state)
    {
        ArgumentNullException.ThrowIfNull(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var query = string.Join(
            '&',
            $"client_id={Uri.EscapeDataString(credentials.ClientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri.ToString())}",
            "response_type=code",
            $"scope={Uri.EscapeDataString(RequiredScopes)}",
            $"state={Uri.EscapeDataString(state)}");

        return new Uri($"{AuthorizeUrl}?{query}");
    }

    /// <summary>Exchanges a one-time authorization code for tokens (FR-001).</summary>
    public Task<StravaTokens> ExchangeAsync(string code, CancellationToken cancellationToken) =>
        PostAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = credentials.ClientId,
                ["client_secret"] = credentials.ClientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
            },
            cancellationToken);

    /// <summary>Renews an access token (FR-004).</summary>
    public Task<StravaTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        PostAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = credentials.ClientId,
                ["client_secret"] = credentials.ClientSecret,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            },
            cancellationToken);

    /// <summary>
    ///   Revokes an access token at Strava (FR-007). Uses <c>/oauth/revoke</c> rather than the
    ///   older <c>/oauth/deauthorize</c>: as of June 2026 revoke is the recommended endpoint, and
    ///   from June 2027 it is the only supported one (research R11).
    /// </summary>
    public async Task RevokeAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, RevokeUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = accessToken }),
        };

        using var response = await http.SendAsync(request, cancellationToken);
    }

    private async Task<StravaTokens> PostAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(
            TokenUrl,
            new FormUrlEncodedContent(form),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new StravaTokenRejectedException(response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<StravaTokens>(cancellationToken)
            ?? throw new InvalidOperationException("Strava returned an empty token response.");
    }
}
