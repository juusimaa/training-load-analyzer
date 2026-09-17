namespace TrainingLoadAnalyzer.Infrastructure.Strava;

/// <summary>
///   Strava granted less access than was requested — the athlete approved the consent page but
///   unticked a scope (FR-002a).
/// </summary>
/// <remarks>
///   <para>
///     A separate type from <see cref="ReconnectionRequiredException"/>, and deliberately not related
///     to it by inheritance, because the two mean different things to the athlete: one says
///     "reconnect", the other says "reconnect <em>and approve this</em>".
///   </para>
///   <para>
///     This is refused rather than accepted-and-warned because the failure is otherwise silent and
///     undetectable: with only <c>activity:read</c>, private activities are filtered out of every
///     response, so the history imports successfully and under-reports the athlete's training
///     (research R14).
///   </para>
/// </remarks>
public sealed class InsufficientScopeException(string requested, string granted)
    : Exception(
        $"Strava granted '{granted}' but the analyzer needs '{requested}'. Without activity:read_all "
            + "your private activities would be silently missing from every figure. Reconnect and "
            + "approve access to all your activities.")
{
    public string Requested { get; } = requested;

    public string Granted { get; } = granted;
}
