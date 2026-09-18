using System.Net;
using Microsoft.EntityFrameworkCore;
using TrainingLoadAnalyzer.Web.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   FR-016 - FR-018. Feature 005 built the authorization code and no host to run it from, so
///   until now it could only be driven from a test and no US5 scenario could run end to end
///   (research R6).
/// </summary>
/// <remarks>
///   No test performs a real OAuth exchange: Strava is feature 005's
///   <c>StubHttpMessageHandler</c>, which records every request it is given.
/// </remarks>
public class ConnectEndpointTests
{
    private const string TokenResponse = """
        {
          "access_token": "an-access-token",
          "refresh_token": "a-refresh-token",
          "expires_at": 1790000000,
          "scope": "read,activity:read_all",
          "athlete": { "id": 900001 }
        }
        """;

    private static HttpClient NonRedirecting(WebAppFactory app) =>
        app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    /// <summary>FR-016: the athlete is sent to Strava's consent screen, carrying a state value.</summary>
    [Fact]
    public async Task Connect_redirects_to_stravas_consent_screen()
    {
        await using var app = new WebAppFactory();
        using var client = NonRedirecting(app);

        using var response = await client.GetAsync("/connect", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location!.ToString();

        Assert.StartsWith("https://www.strava.com/oauth/authorize", location, StringComparison.Ordinal);
        Assert.Contains("state=", location, StringComparison.Ordinal);
        Assert.Contains("activity%3Aread_all", location, StringComparison.Ordinal);
    }

    /// <summary>
    ///   C74: a fresh state per request. A reused or guessable one is the hole state exists to
    ///   close.
    /// </summary>
    [Fact]
    public async Task Each_connect_request_carries_a_different_state()
    {
        await using var app = new WebAppFactory();
        using var client = NonRedirecting(app);

        using var first = await client.GetAsync("/connect", TestContext.Current.CancellationToken);
        using var second = await client.GetAsync("/connect", TestContext.Current.CancellationToken);

        static string StateOf(HttpResponseMessage r) =>
            System.Web.HttpUtility.ParseQueryString(r.Headers.Location!.Query)["state"]!;

        Assert.NotEqual(StateOf(first), StateOf(second));
        Assert.True(StateOf(first).Length >= 16, "A state short enough to guess is not a state.");
    }

    /// <summary>
    ///   Discriminating check for C75 and research R21. A callback whose state does not match is
    ///   refused <em>before</em> the exchange — and the second assertion is the one that matters:
    ///   an implementation that exchanges first and validates afterwards returns the same 400 while
    ///   having already handed a forged code to Strava. The stub records every request, which is
    ///   what makes "no exchange was attempted" assertable rather than merely intended.
    /// </summary>
    [Fact]
    public async Task A_callback_with_a_mismatched_state_is_refused_before_any_exchange()
    {
        await using var app = new WebAppFactory();
        app.Strava.Respond("oauth/token", HttpStatusCode.OK, TokenResponse);

        using var client = NonRedirecting(app);
        using var _ = await client.GetAsync("/connect", TestContext.Current.CancellationToken);

        using var callback = await client.GetAsync(
            "/strava/callback?code=a-code&state=not-the-one-we-issued",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, callback.StatusCode);
        Assert.Empty(app.Strava.Requests);
    }

    /// <summary>FR-016's happy path: a matching state exchanges the code and stores the connection.</summary>
    [Fact]
    public async Task A_matching_state_exchanges_the_code_and_stores_the_connection()
    {
        await using var app = new WebAppFactory();
        app.Strava.Respond("oauth/token", HttpStatusCode.OK, TokenResponse);

        using var client = NonRedirecting(app);
        using var authorize = await client.GetAsync("/connect", TestContext.Current.CancellationToken);
        var state = System.Web.HttpUtility.ParseQueryString(authorize.Headers.Location!.Query)["state"];

        using var callback = await client.GetAsync(
            $"/strava/callback?code=a-code&state={state}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location!.ToString());

        await using var db = app.NewContext();
        var connection = await db.Connections.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(900001, connection.AthleteId);
    }

    /// <summary>
    ///   The athlete declined on Strava's consent screen. That is an answer, not a failure: they
    ///   are told, and no exchange is attempted with a code that does not exist.
    /// </summary>
    [Fact]
    public async Task A_declined_consent_is_reported_without_an_exchange()
    {
        await using var app = new WebAppFactory();

        using var client = NonRedirecting(app);
        using var _ = await client.GetAsync("/connect", TestContext.Current.CancellationToken);

        using var callback = await client.GetAsync(
            "/strava/callback?error=access_denied",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Contains("declined", callback.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(app.Strava.Requests);
    }
}
