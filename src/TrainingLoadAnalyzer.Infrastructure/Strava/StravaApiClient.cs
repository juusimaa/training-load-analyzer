using System.Net;
using System.Net.Http.Json;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>Reads activities and streams from Strava (FR-027, FR-034 - FR-037).</summary>
public sealed class StravaApiClient(HttpClient http)
{
    /// <summary>
    ///   How many times a plausibly transient failure is retried before the sync stops cleanly
    ///   (FR-037). Hand-written rather than delegated to a resilience package: the requirement is
    ///   three attempts with backoff, and a policy engine to express one policy is the kind of
    ///   dependency Principle III exists to decline (research R17).
    /// </summary>
    private const int Attempts = 3;

    /// <summary>
    ///   Strava documents only the default of 30 and no maximum. 200 comes from Strava staff on
    ///   Strava's own forum and is reliable but undocumented; exceeding it returns HTTP 400. It is
    ///   what makes the request budget work — 1,200 activities is six requests, not forty
    ///   (research R15).
    /// </summary>
    private const int PageSize = 200;

    private const string ActivitiesUrl = "https://www.strava.com/api/v3/athlete/activities";

    /// <summary>
    ///   One page of activities at or after <paramref name="after"/>, anchored with <c>after</c> so
    ///   the prefix of the list is immutable and new activities only ever append.
    /// </summary>
    /// <remarks>
    ///   The order results arrive in is deliberately not depended on. Strava's current reference
    ///   documents no ordering for this endpoint at all; the familiar "after returns oldest-first"
    ///   rests on a 2015 forum post never restated since. The caller sorts (research R15).
    /// </remarks>
    public async Task<IReadOnlyList<StravaActivitySummary>> GetActivitiesAsync(
        string accessToken,
        DateTimeOffset after,
        int page,
        CancellationToken cancellationToken)
    {
        var url = $"{ActivitiesUrl}?after={after.ToUnixTimeSeconds()}&page={page}&per_page={PageSize}";

        using var response = await SendAsync(url, accessToken, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<StravaActivitySummary>>(cancellationToken) ?? [];
    }

    /// <summary>
    ///   One activity's time and heart-rate streams, or null when it has none (FR-017).
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Sends neither <c>resolution</c> nor <c>series_type</c>. Omitting <c>resolution</c> is the
    ///     only way to obtain full resolution — its named values cap at 100, 1,000 and 10,000 points
    ///     and there is no value meaning "everything" (FR-017c, research R20). Time in each
    ///     heart-rate zone is what the load calculation is built from, and downsampling would change
    ///     it.
    ///   </para>
    ///   <para>
    ///     A 404 means "this activity has no streams" — it is what manual activities return — and is
    ///     not a failure (C67).
    ///   </para>
    /// </remarks>
    public async Task<StravaStreamSet?> GetStreamsAsync(
        string accessToken,
        string activityId,
        CancellationToken cancellationToken)
    {
        var url = $"https://www.strava.com/api/v3/activities/{activityId}/streams"
            + "?keys=time,heartrate&key_by_type=true";

        using var response = await SendAsync(url, accessToken, cancellationToken, notFoundIsEmpty: true);

        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await response.Content.ReadFromJsonAsync<StravaStreamSet>(cancellationToken);
    }

    /// <summary>
    ///   Sends one request, retrying only what is plausibly transient (FR-037).
    /// </summary>
    /// <remarks>
    ///   Retried: a dropped connection and a 5xx. Never retried: a 429, which is a limit rather
    ///   than a failure (C68); a 401, which is a rejected credential (FR-006); and any other 4xx,
    ///   which will be malformed again on the next attempt.
    /// </remarks>
    private async Task<HttpResponseMessage> SendAsync(
        string url,
        string accessToken,
        CancellationToken cancellationToken,
        bool notFoundIsEmpty = false)
    {
        HttpResponseMessage? response = null;
        Exception? lastFailure = null;

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            response?.Dispose();
            response = null;

            try
            {
                using var request = Authorised(HttpMethod.Get, url, accessToken);
                response = await http.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException failure)
            {
                lastFailure = failure;
            }

            if (response is not null)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var status = RateLimitStatus.From(response.Headers);
                    response.Dispose();

                    throw new StravaRateLimitedException(status);
                }

                if (response.IsSuccessStatusCode
                    || (notFoundIsEmpty && response.StatusCode == HttpStatusCode.NotFound))
                {
                    var budget = RateLimitStatus.From(response.Headers);

                    // FR-034: stop before the limit is exceeded, not after.
                    if (budget?.IsExhausted == true)
                    {
                        response.Dispose();

                        throw new StravaRateLimitedException(budget);
                    }

                    return response;
                }

                if ((int)response.StatusCode < 500)
                {
                    var status = response.StatusCode;
                    response.Dispose();

                    throw new StravaRequestFailedException(status);
                }
            }

            if (attempt < Attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10 * Math.Pow(2, attempt - 1)), cancellationToken);
            }
        }

        var finalStatus = response?.StatusCode;
        response?.Dispose();

        throw new StravaRequestFailedException(finalStatus, lastFailure);
    }

    private static HttpRequestMessage Authorised(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            accessToken);

        return request;
    }
}
