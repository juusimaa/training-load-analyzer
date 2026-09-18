using System.Text.RegularExpressions;
using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Web.Features.Dashboard;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   This ISO week's load, and the comparison with last week's — which is feature 004's judgement
///   rendered, not a second rule (FR-005, research R5).
/// </summary>
public class WeeklyLoadAndTrendTests
{
    private static DashboardView Build(
        IReadOnlyList<TrainingActivity> activities,
        DateOnly? today = null) =>
        DashboardViewBuilder.Build(
            activities, today ?? Fixtures.Today, Fixtures.MaximumHeartRate, true);

    /// <summary>
    ///   FR-004, US2 scenario 1. Fixture H2's current week is four 60-minute sessions: 480 points.
    /// </summary>
    [Fact]
    public void The_current_week_is_the_iso_week_containing_today()
    {
        var view = Build(Fixtures.H2);

        Assert.NotNull(view.CurrentWeek);
        Assert.Equal(480m, view.CurrentWeek.Value.Points);
        Assert.Equal(2026, view.CurrentWeek.Value.Week.Year);
        Assert.Equal(38, view.CurrentWeek.Value.Week.Week);
    }

    /// <summary>
    ///   FR-005, US2 scenario 2. 360 points last week against 480 this week: +120, and +1/3.
    /// </summary>
    [Fact]
    public void The_trend_compares_this_week_against_the_one_before_it()
    {
        var view = Build(Fixtures.H2);

        Assert.NotNull(view.Trend);
        Assert.Equal(480m, view.Trend.Value.Points);
        Assert.Equal(360m, view.Trend.Value.PreviousPoints);
        Assert.Equal(120m, view.Trend.Value.AbsoluteChange);
        Assert.Equal(1m / 3m, view.Trend.Value.RelativeChange!.Value, 10);
    }

    /// <summary>
    ///   US2 scenario 3: a week nobody trained in is present carrying zero, not absent. Feature 002
    ///   builds the series by walking the range rather than grouping the activities, precisely so a
    ///   rest week has an entry (002 FR-005).
    /// </summary>
    [Fact]
    public void A_week_with_no_training_is_present_carrying_zero()
    {
        // Two sessions last week, none this week.
        var view = Build([Fixtures.Session(new DateOnly(2026, 9, 8)), Fixtures.Session(new DateOnly(2026, 9, 10))]);

        Assert.NotNull(view.CurrentWeek);
        Assert.Equal(0m, view.CurrentWeek.Value.Points);
        Assert.Equal(38, view.CurrentWeek.Value.Week.Week);
    }

    /// <summary>
    ///   FR-005a. Today is a Friday, so this week is not finished and the comparison is not like
    ///   for like. The <em>change</em> is still shown — it is real — but no judgement is offered.
    ///   This is feature 004's own rule (004 FR-017), not a display choice.
    /// </summary>
    [Fact]
    public void A_week_still_in_progress_carries_no_judgement()
    {
        var view = Build(Fixtures.H2);

        Assert.NotNull(view.Trend);
        Assert.False(view.Trend.Value.IsComplete);
        Assert.Equal(TrendClassification.Indeterminate, view.Trend.Value.Classification);
        Assert.Equal(120m, view.Trend.Value.AbsoluteChange);
    }

    /// <summary>
    ///   Discriminating check for FR-005 and research R5. The same two weeks, read on the Sunday
    ///   that completes the later one, classify as a significant increase: +120 points clears
    ///   feature 004's 50-point floor and +33% clears its 15% threshold.
    ///   <para>
    ///     This is what proves the dashboard reads feature 004's judgement rather than carrying one
    ///     of its own. The specification originally said "≥5%", and a dashboard that implemented
    ///     that would pass every other test in this file.
    ///   </para>
    /// </summary>
    [Fact]
    public void The_same_two_weeks_are_judged_once_the_week_is_complete()
    {
        var view = Build(Fixtures.H2, new DateOnly(2026, 9, 20));

        Assert.NotNull(view.Trend);
        Assert.True(view.Trend.Value.IsComplete);
        Assert.Equal(TrendClassification.SignificantIncrease, view.Trend.Value.Classification);
    }

    /// <summary>
    ///   004 FR-021 reaches the page: a comparison resting on estimated loads is weaker evidence
    ///   than one resting on measured ones, and says so.
    /// </summary>
    [Fact]
    public void The_trend_carries_the_basis_of_the_weeks_behind_it()
    {
        var view = Build(Fixtures.H2);

        Assert.NotNull(view.Trend);
        Assert.Equal(LoadBasis.Estimated, view.Trend.Value.Basis);
    }

    /// <summary>
    ///   Discriminating check for C103 and research R5. No threshold number appears anywhere in the
    ///   Web project.
    ///   <para>
    ///     The 006 specification originally said a weekly change of "≥5%" was significant. Feature
    ///     004 had already shipped a different rule — ≥15% <em>and</em> ≥50 points — and argued for
    ///     the floor deliberately. The resolution was that the dashboard applies no threshold of its
    ///     own and renders <c>TrendClassification</c> as the domain computes it. This test is where
    ///     that stays true: a reviewer reading only the 006 spec's history could reasonably
    ///     reintroduce the 5% rule, and every other test in this file would still pass.
    ///   </para>
    /// </summary>
    [Fact]
    public void No_threshold_number_lives_in_the_web_project()
    {
        var root = Path.Combine(RepositoryRoot(), "src", "TrainingLoadAnalyzer.Web");

        var offenders = Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal)
                         || f.EndsWith(".razor", StringComparison.Ordinal))
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Number: i + 1, Text: line)))
            // The decimal suffix matters: a C# literal is written 0.05m, and a pattern ending in a
            // (?![\w]) lookahead rejects exactly that. Verified by planting one.
            .Where(l => Regex.IsMatch(
                l.Text,
                @"(?<![\w.])(0\.15|0\.05)[mMdDfF]?(?![\d])"
                    + @"|(?<![\w.])(15|5)\s?%"
                    + @"|(?<![\w.])50[mM](?![\w])"))
            .Select(l => $"{Path.GetFileName(l.File)}:{l.Number}: {l.Text.Trim()}")
            .ToList();

        Assert.Empty(offenders);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TrainingLoadAnalyzer.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root could not be located from the test output directory.");
    }
}
