using System.Net;
using System.Net.Http.Json;

namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>Reads activities and streams from Strava (FR-027, FR-034 - FR-037).</summary>
public sealed class StravaApiClient(HttpClient http)
{
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

        using var request = Authorised(HttpMethod.Get, url, accessToken);
        using var response = await http.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

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

        using var request = Authorised(HttpMethod.Get, url, accessToken);
        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<StravaStreamSet>(cancellationToken);
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
