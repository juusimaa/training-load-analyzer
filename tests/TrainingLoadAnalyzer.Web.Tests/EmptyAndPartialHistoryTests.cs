using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Web.Features.Dashboard;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   The histories the calculators refuse. Feature 003 throws on an empty or short history and
///   feature 004 throws when there is no previous week to compare against — both deliberately,
///   rather than inventing rest days. The builder must therefore <em>ask</em> the right question
///   rather than catch the answer (research R19, C81).
/// </summary>
public class EmptyAndPartialHistoryTests
{
    private static DashboardView Build(
        IReadOnlyList<TrainingActivity> activities,
        bool connected = true,
        DateOnly? today = null) =>
        DashboardViewBuilder.Build(
            activities, today ?? Fixtures.Today, Fixtures.MaximumHeartRate, connected);

    /// <summary>US1 scenario 2, C81: a fresh account is the ordinary first experience, not an edge.</summary>
    [Fact]
    public void No_activities_yields_an_empty_view_rather_than_an_exception()
    {
        var view = Build([]);

        Assert.Null(view.Current);
        Assert.False(view.HasActivities);
        Assert.Empty(view.Metrics);
        Assert.Null(view.CurrentWeek);
        Assert.Null(view.Trend);
        Assert.False(view.HasEnoughHistoryForChart);
    }

    /// <summary>The empty view still knows whether an account is connected, which decides the guidance shown.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_empty_view_carries_the_connection_state_through(bool connected)
    {
        Assert.Equal(connected, Build([], connected).IsStravaConnected);
    }

    /// <summary>C81: one activity is enough for figures, and not enough for a trend or a chart.</summary>
    [Fact]
    public void A_single_activity_yields_figures_without_a_trend_or_a_chart()
    {
        var view = Build(Fixtures.H3b);

        Assert.NotNull(view.Current);
        Assert.Null(view.Trend);
        Assert.False(view.HasEnoughHistoryForChart);
    }

    /// <summary>
    ///   Discriminating check for research R22. A session dated tomorrow — a watch with a wrong
    ///   clock — must not throw and must not silently vanish. The history window ends at the later
    ///   of today and the last recorded day; an implementation that ended it at today either throws
    ///   from <c>DateRange</c> or drops the session from every total. Its appearance in the recent
    ///   list is asserted in <c>RecentActivitiesTests</c>, once that list exists.
    /// </summary>
    [Fact]
    public void An_activity_dated_in_the_future_neither_throws_nor_vanishes()
    {
        var view = Build([Fixtures.Session(Fixtures.Today), Fixtures.Session(Fixtures.Today.AddDays(1))]);

        Assert.NotNull(view.Current);
        Assert.Equal(Fixtures.Today, view.Current.Value.Day);
    }
}
