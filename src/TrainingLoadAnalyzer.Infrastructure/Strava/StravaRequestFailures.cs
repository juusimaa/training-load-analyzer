using System.Net;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   A read limit was reached. Never retried: Strava's documentation is explicit that requests
///   violating the short-term limit still count toward the daily one, so retrying into a limit burns
///   a budget that resets only at midnight UTC. This is a stop signal, not a failure (C68).
/// </summary>
public sealed class StravaRateLimitedException(RateLimitStatus? status)
    : Exception("Strava's read limit has been reached; the sync stopped rather than exceeding it.")
{
    public RateLimitStatus? Status { get; } = status;
}

/// <summary>
///   Strava answered with something that will not improve on a retry — a malformed request, a
///   rejected credential — or a transient failure survived the bounded retry (FR-037).
/// </summary>
public sealed class StravaRequestFailedException(HttpStatusCode? status, Exception? inner = null)
    : Exception(
        status is null
            ? "The connection to Strava failed and did not recover."
            : $"Strava answered {(int)status} {status}.",
        inner)
{
    public HttpStatusCode? Status { get; } = status;
}
