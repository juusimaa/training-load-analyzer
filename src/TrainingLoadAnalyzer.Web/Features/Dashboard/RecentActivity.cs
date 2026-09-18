using TrainingLoadAnalyzer.Domain;

namespace TrainingLoadAnalyzer.Web.Features.Dashboard;

/// <summary>One row of the recent-activities list (FR-007, FR-008).</summary>
/// <remarks>
///   <see cref="Load"/> is the domain's <see cref="TrainingLoad"/>, not a decimal beside a flag.
///   Feature 001 made the points and their provenance inseparable precisely so a consumer "cannot
///   accidentally drop it" (001 SC-007), which is exactly what FR-008 requires — flattening it here
///   would reintroduce the mistake that type was shaped to prevent.
/// </remarks>
public sealed record RecentActivity(
    DateOnly Day,
    ActivityType Type,
    TimeSpan MovingTime,
    TrainingLoad Load);
