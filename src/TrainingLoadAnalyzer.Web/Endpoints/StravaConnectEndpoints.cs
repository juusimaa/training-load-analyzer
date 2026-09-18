using System.Security.Cryptography;
using TrainingLoadAnalyzer.Infrastructure.Strava;
using TrainingLoadAnalyzer.Infrastructure.Sync;

namespace TrainingLoadAnalyzer.Web.Endpoints;

/// <summary>
///   Connecting a Strava account (FR-016 - FR-018).
/// </summary>
/// <remarks>
///   Feature 005 built <see cref="StravaAuthorization"/> and no host to run it from, so until this
///   feature it could only be driven from a test — and US5 scenario 5's "direct them to reconnect"
///   had nowhere to direct anyone (research R6). These two endpoints add no domain and no
///   infrastructure code; they call methods that already exist and are already tested at their own
///   boundary.
///   <para>
///     Connect only. There is no disconnect, no account switching and no token-refresh schedule —
///     <c>DisconnectAsync</c> exists and stays uncalled.
///   </para>
/// </remarks>
public static class StravaConnectEndpoints
{
    /// <summary>
    ///   Where the one-time <c>state</c> waits for the athlete to come back from Strava.
    /// </summary>
    /// <remarks>
    ///   A cookie rather than a server session, so no session store has to be introduced for one
    ///   short-lived value. <c>SameSite=Lax</c> is load-bearing: the callback arrives as a
    ///   cross-site top-level navigation from Strava, which <c>Strict</c> would strip the cookie
    ///   from, and <c>None</c> would send it everywhere.
    /// </remarks>
    private const string StateCookie = "tla.oauth.state";

    public static void MapStravaConnect(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/connect", (HttpContext context, StravaAuthorization authorization) =>
        {
            var state = RandomNumberGenerator.GetHexString(32, lowercase: true);

            context.Response.Cookies.Append(StateCookie, state, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                MaxAge = TimeSpan.FromMinutes(10),
            });

            return Results.Redirect(authorization.BuildAuthorizeUrl(CallbackUri(context), state).ToString());
        });

        endpoints.MapGet("/strava/callback", async (
            HttpContext context,
            StravaAuthorization authorization,
            ILoggerFactory loggers,
            string? code,
            string? state,
            string? error,
            CancellationToken cancellationToken) =>
        {
            var logger = loggers.CreateLogger(typeof(StravaConnectEndpoints));
            var expected = context.Request.Cookies[StateCookie];

            context.Response.Cookies.Delete(StateCookie);

            if (!string.IsNullOrEmpty(error))
            {
                // The athlete said no on Strava's consent screen. That is an answer, not a failure,
                // and there is no code to exchange.
                return Results.Redirect("/?connect=declined");
            }

            // Checked BEFORE the exchange, and that ordering is the whole point: an implementation
            // that exchanged first and validated afterwards would return the same status while
            // having already handed a forged code to Strava (C75, research R21).
            if (string.IsNullOrEmpty(state)
                || string.IsNullOrEmpty(expected)
                || !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(state),
                    System.Text.Encoding.UTF8.GetBytes(expected)))
            {
                logger.LogWarning(
                    "A Strava callback arrived whose state did not match the one issued. No token "
                        + "exchange was attempted.");

                return Results.BadRequest(
                    "This sign-in could not be verified. Start again from the dashboard.");
            }

            if (string.IsNullOrEmpty(code))
            {
                return Results.BadRequest("Strava returned no authorization code.");
            }

            try
            {
                await authorization.ExchangeAsync(code, cancellationToken);

                return Results.Redirect("/");
            }
            catch (InsufficientScopeException withheld)
            {
                // 005 FR-002a: a connection that cannot see private activities is refused, not
                // degraded. The message names what was withheld and carries no credential.
                logger.LogWarning(withheld, "A Strava connection was refused for a withheld scope.");

                return Results.Redirect("/?connect=scope");
            }
            catch (InvalidOperationException mismatch)
            {
                // 005 FR-008: a different athlete authorized. Two athletes' training must never
                // combine into one history.
                logger.LogWarning(mismatch, "A Strava connection was refused for an athlete mismatch.");

                return Results.Redirect("/?connect=mismatch");
            }
        });
    }

    private static Uri CallbackUri(HttpContext context) =>
        new($"{context.Request.Scheme}://{context.Request.Host}/strava/callback");
}
