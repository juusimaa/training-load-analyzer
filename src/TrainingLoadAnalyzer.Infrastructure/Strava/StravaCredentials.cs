namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   The application's own registration at Strava. These belong to whoever runs the analyzer, not to
///   the athlete, which is why they are configuration rather than a column: a copied database file
///   must not leak the application's identity along with the athlete's training (research R12).
/// </summary>
public sealed record StravaCredentials(string ClientId, string ClientSecret);
