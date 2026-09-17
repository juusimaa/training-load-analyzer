namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   One ISO-8601 week's training load compared against the week before it (FR-030).
/// </summary>
public readonly record struct WeeklyLoadTrend(
    IsoWeek Week,
    decimal Points,
    decimal PreviousPoints)
{
    /// <summary>
    ///   How many points the week moved by, derived on every read and never accumulated
    ///   (FR-002, FR-033).
    /// </summary>
    public decimal AbsoluteChange => Points - PreviousPoints;

    /// <summary>
    ///   That move as a share of the week before it, or nothing at all when the week before
    ///   carried no load and there is no proportion to take (FR-003, FR-004).
    /// </summary>
    /// <remarks>
    ///   The absence is <see langword="null"/>, never zero and never a sentinel: an idle week
    ///   followed by a hard one is not the same as a week that did not change. Because these are
    ///   decimals rather than binary floating point, there is no infinity for this to become by
    ///   accident - omitting the guard throws instead (research R2).
    /// </remarks>
    public decimal? RelativeChange =>
        PreviousPoints == 0 ? null : AbsoluteChange / PreviousPoints;
}
