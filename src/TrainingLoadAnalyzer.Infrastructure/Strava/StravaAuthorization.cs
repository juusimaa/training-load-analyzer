using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TrainingLoadAnalyzer.Infrastructure.Persistence;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   Obtains and maintains permission to read one athlete's Strava data (FR-001 - FR-008).
/// </summary>
public sealed class StravaAuthorization(
    HttpClient http,
    StravaCredentials credentials,
    TimeProvider clock,
    ImportDbContext db)
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
    ///   Note that <c>read</c> grants nothing about activities at all — profile and public segments
    ///   only — so it is not a substitute for either activity scope. No <c>:write</c> scope is ever
    ///   requested: this analyzer reads and never writes (C70).
    /// </remarks>
    internal const string RequiredScopes = "read,activity:read_all";

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

    /// <summary>
    ///   Exchanges a one-time authorization code for tokens (FR-001). The code is short-lived and
    ///   single-use: a second attempt with the same one fails, which is Strava working correctly.
    /// </summary>
    public async Task<StravaConnection> ExchangeAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var tokens = await PostAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = credentials.ClientId,
                ["client_secret"] = credentials.ClientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
            },
            cancellationToken);

        // FR-002a: refuse before anything is stored. A connection that cannot see private
        // activities is not a degraded connection, it is a refused one.
        var granted = tokens.Scope ?? string.Empty;

        if (!granted.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Contains("activity:read_all", StringComparer.Ordinal))
        {
            throw new InsufficientScopeException(RequiredScopes, granted);
        }

        var athleteId = tokens.Athlete?.Id ?? 0;
        var connected = await db.Connections.FirstOrDefaultAsync(cancellationToken);

        // FR-008: one athlete at a time. Disconnecting first is how an account is changed, which is
        // what makes discarding the previous athlete's activities an explicit choice (FR-007).
        if (connected is not null && connected.AthleteId != athleteId)
        {
            throw new InvalidOperationException(
                $"This analyzer already holds training for Strava athlete {connected.AthleteId}, and "
                    + $"athlete {athleteId} authorized instead. Disconnect the existing account before "
                    + "connecting a different one, so two athletes' training is never combined.");
        }

        var connection = await db.Connections.FindAsync([athleteId], cancellationToken)
            ?? new StravaConnection { AthleteId = athleteId };

        if (db.Entry(connection).State == EntityState.Detached)
        {
            db.Connections.Add(connection);
        }

        Apply(tokens, connection);
        connection.ConnectedAtUtcTicks = clock.GetUtcNow().UtcTicks;

        await db.SaveChangesAsync(cancellationToken);

        return connection;
    }

    /// <summary>
    ///   Writes a token response onto the connection. Both tokens move together, in one place, so no
    ///   path can store an access token while leaving a stale refresh token behind — which is the one
    ///   way this type can lock an athlete out permanently (C48, research R13).
    /// </summary>
    private static void Apply(StravaTokens tokens, StravaConnection connection)
    {
        connection.AccessToken = tokens.AccessToken;
        connection.RefreshToken = tokens.RefreshToken;
        connection.ExpiresAtUtcTicks = DateTimeOffset.FromUnixTimeSeconds(tokens.ExpiresAt).UtcTicks;

        if (tokens.Scope is not null)
        {
            connection.GrantedScopes = tokens.Scope;
        }
    }

    /// <summary>
    ///   Renews an expired access token without involving the athlete (FR-004). Both tokens are
    ///   written through <see cref="Apply"/>, because Strava rotates the refresh token on every
    ///   successful token request and invalidates the old one immediately (C48, research R13).
    /// </summary>
    public async Task<StravaConnection> RefreshAsync(
        StravaConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        StravaTokens tokens;

        try
        {
            tokens = await PostAsync(
                new Dictionary<string, string>
                {
                    ["client_id"] = credentials.ClientId,
                    ["client_secret"] = credentials.ClientSecret,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = connection.RefreshToken,
                },
                cancellationToken);
        }
        catch (StravaTokenRejectedException)
        {
            throw new ReconnectionRequiredException(connection.AthleteId);
        }

        Apply(tokens, connection);

        await db.SaveChangesAsync(cancellationToken);

        return connection;
    }

    /// <summary>
    ///   Revokes at Strava and discards the stored credentials (FR-007). Whether the athlete's
    ///   imported activities are also discarded is their separate, explicit choice — never a side
    ///   effect of disconnecting.
    /// </summary>
    /// <remarks>
    ///   Uses <c>/oauth/revoke</c> rather than the older <c>/oauth/deauthorize</c>: as of June 2026
    ///   revoke is the recommended endpoint, and from June 2027 it is the only supported one
    ///   (research R11).
    /// </remarks>
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var connection = await db.Connections.FirstOrDefaultAsync(cancellationToken);

        if (connection is null)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, RevokeUrl)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string> { ["token"] = connection.AccessToken }),
        };

        // Strava having already forgotten the token is not a reason to keep it locally.
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Deliberately swallowed, and only here: the local discard below is what FR-007
            // actually requires, and it must happen whether or not Strava could be reached.
        }

        db.Connections.Remove(connection);
        await db.SaveChangesAsync(cancellationToken);
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
