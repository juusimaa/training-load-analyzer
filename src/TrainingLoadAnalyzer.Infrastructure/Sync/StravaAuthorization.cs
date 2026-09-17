using Microsoft.EntityFrameworkCore;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Strava;

namespace TrainingLoadAnalyzer.Infrastructure.Sync;

/// <summary>
///   Obtains and maintains permission to read one athlete's Strava data (FR-001 - FR-008).
/// </summary>
/// <remarks>
///   Lives in <c>Sync/</c> because it knows both sides: it talks to Strava through
///   <see cref="StravaOAuthClient"/> and persists the result. <c>Strava/</c> knows about a third
///   party and nothing about storage; <c>Persistence/</c> knows about storage and nothing about
///   Strava; only this folder knows both, which is what makes the one-way dependency checkable by
///   inspection rather than only by intent.
/// </remarks>
public sealed class StravaAuthorization(
    HttpClient http,
    StravaCredentials credentials,
    TimeProvider clock,
    ImportDbContext db)
{
    private readonly StravaOAuthClient oauth = new(http, credentials);

    /// <summary>Where to send the athlete to grant consent (FR-001).</summary>
    public Uri BuildAuthorizeUrl(Uri redirectUri, string state) =>
        oauth.BuildAuthorizeUrl(redirectUri, state);

    /// <summary>
    ///   Exchanges a one-time authorization code for a stored connection (FR-001). The code is
    ///   short-lived and single-use: a second attempt with the same one fails, which is Strava
    ///   working correctly.
    /// </summary>
    public async Task<StravaConnection> ExchangeAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var tokens = await oauth.ExchangeAsync(code, cancellationToken);

        // FR-002a: refuse before anything is stored. A connection that cannot see private
        // activities is not a degraded connection, it is a refused one.
        var granted = tokens.Scope ?? string.Empty;

        if (!granted.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Contains("activity:read_all", StringComparer.Ordinal))
        {
            throw new InsufficientScopeException(StravaOAuthClient.RequiredScopes, granted);
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
            tokens = await oauth.RefreshAsync(connection.RefreshToken, cancellationToken);
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
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var connection = await db.Connections.FirstOrDefaultAsync(cancellationToken);

        if (connection is null)
        {
            return;
        }

        // Strava having already forgotten the token is not a reason to keep it locally.
        try
        {
            await oauth.RevokeAsync(connection.AccessToken, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Deliberately swallowed, and only here: the local discard below is what FR-007
            // actually requires, and it must happen whether or not Strava could be reached.
        }

        db.Connections.Remove(connection);
        await db.SaveChangesAsync(cancellationToken);
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
}
