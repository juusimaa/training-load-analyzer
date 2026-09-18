using Microsoft.Extensions.Configuration;

namespace TrainingLoadAnalyzer.Web;

/// <summary>
///   The one thing the analyzer needs to know about the athlete that their training data does not
///   contain (FR-014).
/// </summary>
/// <remarks>
///   <para>
///     Every measured training load is <c>CalculateTrainingLoad(int maximumHeartRate)</c>, and no
///     part of the system stores that number: feature 001 declined to put it on the activity (001
///     FR-015), feature 002 declined an athlete-profile entity for it, and feature 005 neither reads
///     it from Strava nor stores it. Until this feature, every call site was a test choosing 190.
///   </para>
///   <para>
///     It arrives as configuration for the same reason <c>StravaCredentials</c> does: it belongs to
///     whoever runs the analyzer rather than in a database column (research R4).
///   </para>
/// </remarks>
public sealed record AthleteSettings(int MaximumHeartRate)
{
    public const string Key = "Athlete:MaximumHeartRate";

    /// <summary>
    ///   Reads the setting, or refuses (FR-015, C77).
    /// </summary>
    /// <remarks>
    ///   Refusing is the deliberate part. The failure this avoids is an application that starts
    ///   against a default nobody chose and then presents confidently wrong TRIMP figures with
    ///   nothing on screen to suggest anything is amiss — the silent, correctness-relevant failure
    ///   Principle VI exists to prevent.
    /// </remarks>
    public static AthleteSettings From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration[Key];

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"'{Key}' is not configured. Every measured training load is computed from the "
                    + "athlete's maximum heart rate, and there is no sensible default for it. Set it "
                    + "in user secrets, in the environment, or in appsettings.json before starting.");
        }

        if (!int.TryParse(configured, out var maximumHeartRate) || maximumHeartRate <= 0)
        {
            throw new InvalidOperationException(
                $"'{Key}' is '{configured}', which is not a positive whole number of beats per "
                    + "minute. A training load computed from it would be meaningless.");
        }

        return new AthleteSettings(maximumHeartRate);
    }
}
