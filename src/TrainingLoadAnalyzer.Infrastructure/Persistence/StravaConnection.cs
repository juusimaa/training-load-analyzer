namespace TrainingLoadAnalyzer.Infrastructure.Persistence;

/// <summary>
///   The standing permission to read one athlete's Strava data. At most one exists (FR-008).
/// </summary>
/// <remarks>
///   <para>
///     Deliberately a class rather than a record. A record's compiler-generated
///     <see cref="object.ToString"/> prints every property, which would put both tokens into any log
///     line that interpolated one — FR-005 and C51 forbid exactly that.
///   </para>
///   <para>
///     The application's own client id and secret are <em>not</em> here. They belong to the
///     registration rather than to the athlete, and a copied database file must not leak them
///     (research R12).
///   </para>
/// </remarks>
public sealed class StravaConnection
{
    public long AthleteId { get; set; }

    /// <summary>Secret. Never logged, never placed on a sync summary (FR-005).</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    ///   Secret, and <strong>replaced on every token response</strong>. Strava rotates it and
    ///   invalidates the old one immediately; keeping the original works until the first rotation
    ///   and then locks the athlete out permanently (FR-004, C48, research R13).
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    public long ExpiresAtUtcTicks { get; set; }

    /// <summary>What Strava granted, which FR-002a checks before a connection is accepted.</summary>
    public string GrantedScopes { get; set; } = string.Empty;

    public long ConnectedAtUtcTicks { get; set; }

    /// <summary>Never prints a credential (FR-005, C51).</summary>
    public override string ToString() => $"StravaConnection(athlete {AthleteId})";
}
