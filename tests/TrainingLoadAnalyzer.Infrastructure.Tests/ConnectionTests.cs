using System.Net;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Strava;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

namespace TrainingLoadAnalyzer.Infrastructure.Tests;

/// <summary>User Story 1 — connecting a Strava account.</summary>
public sealed class ConnectionTests
{
    private static readonly StravaCredentials Credentials = new("12345", "a-client-secret");

    private static readonly Uri Redirect = new("http://localhost/callback");

    // T011: FR-001, FR-002, C47.
    [Fact]
    public void AuthorizeUrlCarriesTheParametersStravaRequires()
    {
        var authorization = new StravaAuthorization(
            new HttpClient(new StubHttpMessageHandler()),
            Credentials,
            new FixedClock(), Db());

        var url = authorization.BuildAuthorizeUrl(Redirect, "a-state-value");
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);

        Assert.Equal("https://www.strava.com/oauth/authorize", url.GetLeftPart(UriPartial.Path));
        Assert.Equal("12345", query["client_id"]);
        Assert.Equal(Redirect.ToString(), query["redirect_uri"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("a-state-value", query["state"]);
        Assert.Contains("activity:read_all", query["scope"]);
    }

    // T013: C46, C70 — the analyzer never sees a password and never asks to write.
    [Fact]
    public void AuthorizeUrlNeitherTakesAPasswordNorAsksToWrite()
    {
        var authorization = new StravaAuthorization(
            new HttpClient(new StubHttpMessageHandler()),
            Credentials,
            new FixedClock(), Db());

        var url = authorization.BuildAuthorizeUrl(Redirect, "a-state-value").ToString();

        Assert.DoesNotContain("password", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":write", url, StringComparison.OrdinalIgnoreCase);
    }

    // T014: scenario US1.1, FR-001.
    [Fact]
    public async Task ExchangingTheCodeYieldsAConnectionForTheAuthorizingAthlete()
    {
        var stub = new StubHttpMessageHandler()
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson());
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, new FixedClock(), Db());

        var connection = await authorization.ExchangeAsync("an-authorization-code", TestContext.Current.CancellationToken);

        Assert.Equal(900001, connection.AthleteId);
        Assert.Equal("access-1", connection.AccessToken);
        Assert.Equal("refresh-1", connection.RefreshToken);
        Assert.Equal(
            FixedClock.Default.AddHours(6).UtcTicks,
            connection.ExpiresAtUtcTicks);
    }

    // T016: FR-020 — a field this code has never heard of must not fail an exchange.
    [Fact]
    public async Task UnrecognisedFieldsInATokenResponseAreIgnored()
    {
        const string withExtras = """
            {
              "token_type": "Bearer", "expires_at": 1789661253, "expires_in": 21600,
              "refresh_token": "refresh-1", "access_token": "access-1",
              "athlete": { "id": 900001 }, "scope": "read,activity:read_all",
              "a_field_added_later": 42, "another": { "nested": true }, "third": ["x"]
            }
            """;
        var stub = new StubHttpMessageHandler().Respond("oauth/token", HttpStatusCode.OK, withExtras);
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, new FixedClock(), Db());

        var connection = await authorization.ExchangeAsync("a-code", TestContext.Current.CancellationToken);

        Assert.Equal(900001, connection.AthleteId);
    }

    // T017: scenario US1.2, FR-003 — the connection survives a restart.
    [Fact]
    public async Task TheConnectionIsReadableFromAFreshContext()
    {
        using var fixture = NewFixture();
        var stub = new StubHttpMessageHandler().Respond("oauth/token", HttpStatusCode.OK, TokenJson());

        await using (var writing = fixture.NewContext())
        {
            var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, new FixedClock(), writing);
            await authorization.ExchangeAsync("a-code", TestContext.Current.CancellationToken);
        }

        await using var reading = fixture.NewContext();
        var stored = reading.Connections.Single();

        Assert.Equal(900001, stored.AthleteId);
        Assert.Equal("access-1", stored.AccessToken);
        Assert.Equal("refresh-1", stored.RefreshToken);
    }

    // T020: FR-008's precondition — at most one connection can exist.
    [Fact]
    public async Task ASecondConnectionForTheSameAthleteReplacesRatherThanAccumulates()
    {
        using var fixture = NewFixture();
        var stub = new StubHttpMessageHandler()
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(access: "access-1"))
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(access: "access-2"));

        await using var db = fixture.NewContext();
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, new FixedClock(), db);
        await authorization.ExchangeAsync("code-1", TestContext.Current.CancellationToken);
        await authorization.ExchangeAsync("code-2", TestContext.Current.CancellationToken);

        Assert.Single(db.Connections);
    }

    // T021: scenario US1.3, FR-004 — an expired access token renews itself.
    [Fact]
    public async Task AnExpiredAccessTokenIsRenewedWithoutTheAthlete()
    {
        using var fixture = NewFixture();
        var clock = new FixedClock();
        var stub = new StubHttpMessageHandler()
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(access: "access-1", refresh: "refresh-1"), once: true)
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(access: "access-2", refresh: "refresh-1"));

        await using var db = fixture.NewContext();
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, clock, db);
        var connection = await authorization.ExchangeAsync("a-code", TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromHours(7));
        var renewed = await authorization.RefreshAsync(connection, TestContext.Current.CancellationToken);

        Assert.Equal("access-2", renewed.AccessToken);
        Assert.Equal("access-2", fixture.NewContext().Connections.Single().AccessToken);
    }

    // T023: C48, research R13 — the refresh token rotates, and the rotated one must be persisted.
    // Strava invalidates the old one immediately; keeping it works in testing and then locks the
    // athlete out permanently, presenting as a revoked-access failure.
    [Fact]
    public async Task TheRotatedRefreshTokenIsStoredInPlaceOfTheOldOne()
    {
        using var fixture = NewFixture();
        var clock = new FixedClock();
        var stub = new StubHttpMessageHandler()
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(access: "access-1", refresh: "refresh-1"), once: true)
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(access: "access-2", refresh: "refresh-2"));

        await using var db = fixture.NewContext();
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, clock, db);
        var connection = await authorization.ExchangeAsync("a-code", TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromHours(7));
        await authorization.RefreshAsync(connection, TestContext.Current.CancellationToken);

        Assert.Equal("refresh-2", fixture.NewContext().Connections.Single().RefreshToken);
    }

    // T025: the same rule holds on the exchange, not only on the refresh.
    [Fact]
    public async Task AnExchangeAlsoStoresTheRefreshTokenItWasGiven()
    {
        using var fixture = NewFixture();
        var stub = new StubHttpMessageHandler()
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(refresh: "refresh-from-exchange"));

        await using var db = fixture.NewContext();
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, new FixedClock(), db);
        await authorization.ExchangeAsync("a-code", TestContext.Current.CancellationToken);

        Assert.Equal("refresh-from-exchange", fixture.NewContext().Connections.Single().RefreshToken);
    }

    // T026: scenario US1.4, FR-006, C49 — a rejected renewal is its own named outcome.
    [Fact]
    public async Task ARejectedRenewalAsksForReconnectionAndKeepsTheConnection()
    {
        using var fixture = NewFixture();
        var clock = new FixedClock();
        var stub = new StubHttpMessageHandler()
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(), once: true)
            .Respond("oauth/token", HttpStatusCode.BadRequest, """{"message":"Bad Request"}""");

        await using var db = fixture.NewContext();
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, clock, db);
        var connection = await authorization.ExchangeAsync("a-code", TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromHours(7));

        await Assert.ThrowsAsync<ReconnectionRequiredException>(
            () => authorization.RefreshAsync(connection, TestContext.Current.CancellationToken));

        Assert.Single(fixture.NewContext().Connections);
    }

    // T028: FR-002a, scenario US1.7, C52 — a grant that withholds private-activity access is
    // refused. Accepted silently, the import reports success while omitting every private
    // activity, and no figure downstream could ever detect it (research R14).
    [Fact]
    public async Task AGrantWithoutPrivateActivityAccessIsRefused()
    {
        using var fixture = NewFixture();
        var stub = new StubHttpMessageHandler()
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(scope: "read,activity:read"));

        await using var db = fixture.NewContext();
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, new FixedClock(), db);

        var refusal = await Assert.ThrowsAsync<InsufficientScopeException>(
            () => authorization.ExchangeAsync("a-code", TestContext.Current.CancellationToken));

        Assert.Contains("activity:read_all", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.NewContext().Connections);
    }

    // T028 continued: the refusal must be a different type from a rejected credential, because
    // feature 6 presents them differently (FR-002a).
    [Fact]
    public void AWithheldScopeIsNotTheSameFailureAsARejectedCredential()
    {
        Assert.False(typeof(ReconnectionRequiredException).IsAssignableFrom(typeof(InsufficientScopeException)));
        Assert.False(typeof(InsufficientScopeException).IsAssignableFrom(typeof(ReconnectionRequiredException)));
    }

    // T030: the accepting path, pinned against T028 so the two cannot drift.
    [Fact]
    public async Task AGrantIncludingPrivateActivityAccessIsAcceptedAndItsScopesRecorded()
    {
        using var fixture = NewFixture();
        var stub = new StubHttpMessageHandler()
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(scope: "read activity:read_all"));

        await using var db = fixture.NewContext();
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, new FixedClock(), db);
        var connection = await authorization.ExchangeAsync("a-code", TestContext.Current.CancellationToken);

        Assert.Contains("activity:read_all", connection.GrantedScopes, StringComparison.Ordinal);
    }

    // T031: scenario US1.6, FR-008, C50 — two athletes' training must never combine.
    [Fact]
    public async Task AuthorizingADifferentAthleteIsRefusedWhileOneIsConnected()
    {
        using var fixture = NewFixture();
        var stub = new StubHttpMessageHandler()
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(athleteId: 900001), once: true)
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson(athleteId: 900002));

        await using var db = fixture.NewContext();
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, new FixedClock(), db);
        await authorization.ExchangeAsync("code-1", TestContext.Current.CancellationToken);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => authorization.ExchangeAsync("code-2", TestContext.Current.CancellationToken));

        Assert.Contains("900001", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("900002", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(900001, fixture.NewContext().Connections.Single().AthleteId);
    }

    // T033: scenario US1.5, FR-007 — disconnecting discards the credentials. The other half of
    // US1.5 — that a sync then refuses — is asserted at T088, once StravaActivitySync exists.
    [Fact]
    public async Task DisconnectingDiscardsTheStoredCredentials()
    {
        using var fixture = NewFixture();
        var stub = new StubHttpMessageHandler()
            .Respond("oauth/token", HttpStatusCode.OK, TokenJson())
            .Respond("oauth/revoke", HttpStatusCode.OK);

        await using var db = fixture.NewContext();
        var authorization = new StravaAuthorization(new HttpClient(stub), Credentials, new FixedClock(), db);
        await authorization.ExchangeAsync("a-code", TestContext.Current.CancellationToken);

        await authorization.DisconnectAsync(TestContext.Current.CancellationToken);

        Assert.Empty(fixture.NewContext().Connections);
        Assert.Contains(stub.Requests, r => r.RequestUri!.ToString().Contains("oauth/revoke", StringComparison.Ordinal));
    }

    // T036: FR-005, C51 — a discriminating check. A record's generated ToString prints every
    // property, so declaring StravaConnection as a record would put both tokens into any log line
    // that interpolated one. This is what makes that a deliberate choice rather than an accident.
    [Fact]
    public void NoCredentialIsReachableThroughToStringOrAnExceptionMessage()
    {
        var connection = new StravaConnection
        {
            AthleteId = 900001,
            AccessToken = "a-secret-access-token",
            RefreshToken = "a-secret-refresh-token",
            GrantedScopes = "read,activity:read_all",
        };

        Assert.DoesNotContain("a-secret-access-token", connection.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("a-secret-refresh-token", connection.ToString(), StringComparison.Ordinal);

        var reconnect = new ReconnectionRequiredException(900001).ToString();
        Assert.DoesNotContain("a-secret", reconnect, StringComparison.Ordinal);

        var scope = new InsufficientScopeException("read,activity:read_all", "read").ToString();
        Assert.DoesNotContain("a-secret", scope, StringComparison.Ordinal);
    }

    private static SqliteFixture<ImportDbContext> NewFixture() => new(options => new ImportDbContext(options));

    private static ImportDbContext Db() => NewFixture().NewContext();

    /// <summary>
    ///   A Strava token response. <c>expires_at</c> is epoch <em>seconds</em>, not a duration, and
    ///   the granted <c>scope</c> is what FR-002a turns on.
    /// </summary>
    private static string TokenJson(
        string access = "access-1",
        string refresh = "refresh-1",
        long athleteId = 900001,
        string scope = "read,activity:read_all",
        DateTimeOffset? expiresAt = null)
    {
        var expiry = (expiresAt ?? FixedClock.Default.AddHours(6)).ToUnixTimeSeconds();

        return $$"""
            {
              "token_type": "Bearer",
              "expires_at": {{expiry}},
              "expires_in": 21600,
              "refresh_token": "{{refresh}}",
              "access_token": "{{access}}",
              "athlete": { "id": {{athleteId}}, "username": "anonymised" },
              "scope": "{{scope}}"
            }
            """;
    }
}
