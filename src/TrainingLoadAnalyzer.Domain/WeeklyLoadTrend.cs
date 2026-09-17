namespace TrainingLoadAnalyzer.Domain;

/// <summary>
///   One ISO-8601 week's training load compared against the week before it (FR-030).
/// </summary>
public readonly record struct WeeklyLoadTrend(
    IsoWeek Week,
    decimal Points,
    decimal PreviousPoints,
    bool IsComplete,
    LoadBasis Basis)
{
    /// <summary>
    ///   How many points a week must move by before the change is worth reporting (FR-010).
    /// </summary>
    /// <remarks>
    ///   Private on purpose: a test asserting against this constant would only establish that
    ///   the code agrees with itself (research R7). Tests use concrete totals either side of it.
    /// </remarks>
    private const decimal AbsoluteThreshold = 50m;

    /// <summary>
    ///   What share of the previous week a change must reach before it is worth reporting
    ///   (FR-010). Private for the same reason as <see cref="AbsoluteThreshold"/>.
    /// </summary>
    private const decimal RelativeThreshold = 0.15m;

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

    /// <summary>
    ///   What to make of that change (FR-007).
    /// </summary>
    public TrendClassification Classification
    {
        get
        {
            // Completeness is read before any threshold, which is what makes FR-017
            // unreachable by construction rather than a rule to be maintained: a week the
            // range cuts short can never acquire a significance label at all.
            if (!IsComplete)
            {
                return TrendClassification.Indeterminate;
            }

            // The floor is tested first, and that ordering is load-bearing rather than
            // incidental: when there is no proportion to test, the floor has already applied
            // the only test that exists, so a week following an idle one falls through to the
            // sign instead of being swallowed as steady (FR-013, FR-014).
            if (Math.Abs(AbsoluteChange) < AbsoluteThreshold)
            {
                return TrendClassification.Steady;
            }

            if (RelativeChange is { } relative && Math.Abs(relative) < RelativeThreshold)
            {
                return TrendClassification.Steady;
            }

            // Magnitudes are compared above, so a rise and a fall of the same size are judged
            // alike; only the sign decides which one this is (FR-008).
            return AbsoluteChange > 0
                ? TrendClassification.SignificantIncrease
                : TrendClassification.SignificantDecrease;
        }
    }
}
