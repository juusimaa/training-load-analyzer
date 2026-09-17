using System.Globalization;
using System.Net.Http.Headers;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   What Strava reports about the application's remaining request budget (FR-034, FR-035).
/// </summary>
/// <remarks>
///   <para>
///     Read from the <c>X-ReadRateLimit-*</c> headers rather than <c>X-RateLimit-*</c>. The read
///     allowance — 100 requests every fifteen minutes and 1,000 a day — is the binding one for an
///     importer; the overall allowance of 200 and 2,000 never binds first (research R16).
///   </para>
///   <para>
///     Each header carries two comma-separated integers: the fifteen-minute value, then the daily
///     one. Header names are matched without regard to case, because Strava's own documentation is
///     inconsistent about theirs.
///   </para>
/// </remarks>
public sealed record RateLimitStatus(
    int ShortTermUsage,
    int ShortTermLimit,
    int DailyUsage,
    int DailyLimit)
{
    private const string LimitHeader = "X-ReadRateLimit-Limit";

    private const string UsageHeader = "X-ReadRateLimit-Usage";

    /// <summary>Whether either window has no room left.</summary>
    public bool IsExhausted => ShortTermUsage >= ShortTermLimit || DailyUsage >= DailyLimit;

    private bool DailyExhausted => DailyUsage >= DailyLimit;

    /// <summary>Reads the budget from a response, or null when the headers are absent.</summary>
    public static RateLimitStatus? From(HttpResponseHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var (shortLimit, dayLimit) = Pair(headers, LimitHeader);
        var (shortUsage, dayUsage) = Pair(headers, UsageHeader);

        return shortLimit is null || shortUsage is null
            ? null
            : new RateLimitStatus(shortUsage.Value, shortLimit.Value, dayUsage ?? 0, dayLimit ?? 0);
    }

    /// <summary>
    ///   The earliest time a sync can usefully be retried (FR-035, C62).
    /// </summary>
    /// <remarks>
    ///   Strava's fifteen-minute limit "is reset at natural 15-minute intervals corresponding to 0,
    ///   15, 30 and 45 minutes after the hour", and the daily limit resets at midnight UTC. So this
    ///   is a window boundary, never a fixed delay from now — which on average halves the wait.
    /// </remarks>
    public DateTimeOffset RetryAfter(DateTimeOffset now)
    {
        if (DailyExhausted)
        {
            return new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(1);
        }

        var quarters = (now.UtcDateTime.Minute / 15) + 1;

        return new DateTimeOffset(
            now.UtcDateTime.Date.AddHours(now.UtcDateTime.Hour),
            TimeSpan.Zero).AddMinutes(quarters * 15);
    }

    private static (int? First, int? Second) Pair(HttpResponseHeaders headers, string name)
    {
        var value = headers
            .FirstOrDefault(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase))
            .Value?
            .FirstOrDefault();

        if (value is null)
        {
            return (null, null);
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);

        return (
            parts.Length > 0 && int.TryParse(parts[0], CultureInfo.InvariantCulture, out var first)
                ? first
                : null,
            parts.Length > 1 && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var second)
                ? second
                : null);
    }
}
