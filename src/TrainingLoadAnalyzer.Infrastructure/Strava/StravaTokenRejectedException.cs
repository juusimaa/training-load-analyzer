using System.Net;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   Strava refused a token request. Internal: callers see the outcome that fits their context —
///   <see cref="ReconnectionRequiredException"/> on a renewal — rather than an HTTP status code.
/// </summary>
internal sealed class StravaTokenRejectedException(HttpStatusCode status)
    : Exception($"Strava refused the token request with {(int)status} {status}.");
