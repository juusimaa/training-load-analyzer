using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Web.Features.Dashboard;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   The seven most recent sessions, and whether each one's load was measured or estimated
///   (FR-007, FR-008, SC-004).
/// </summary>
public class RecentActivitiesTests
{
    private static DashboardView Build(IReadOnlyList<TrainingActivity> activities) =>
        DashboardViewBuilder.Build(activities, Fixtures.Today, Fixtures.MaximumHeartRate, true);

    /// <summary>
    ///   FR-007, SC-004, C84. Fixture H3f holds ten sessions; the list shows seven, newest first,
    ///   and the three oldest are absent rather than silently truncated from the wrong end.
    /// </summary>
    [Fact]
    public void The_seven_most_recent_sessions_appear_newest_first()
    {
        var view = Build(Fixtures.H3f);

        Assert.Equal(7, view.Recent.Count);
        Assert.Equal(Fixtures.Today, view.Recent[0].Day);

        for (var i = 1; i < view.Recent.Count; i++)
        {
            Assert.True(
                view.Recent[i].Day <= view.Recent[i - 1].Day,
                $"Entry {i} ({view.Recent[i].Day}) is newer than the one before it ({view.Recent[i - 1].Day}).");
        }
    }

    /// <summary>US4 scenario 4: three sessions produce three entries, not three padded to seven.</summary>
    [Fact]
    public void Fewer_than_seven_sessions_are_all_shown()
    {
        Assert.Equal(3, Build(Fixtures.H3g).Recent.Count);
    }

    /// <summary>
    ///   FR-008. The measured session in fixture H3f carries a heart-rate series; the rest do not.
    ///   <c>TrainingLoad</c> carries its points and its provenance inseparably by design (001
    ///   SC-007), so a <c>RecentActivity</c> that flattened it to a bare decimal would reintroduce
    ///   exactly the mistake that type was shaped to prevent.
    /// </summary>
    [Fact]
    public void Each_entry_says_whether_its_load_was_measured_or_estimated()
    {
        var view = Build(Fixtures.H3f);

        Assert.Equal(LoadProvenance.Measured, view.Recent[0].Load.Provenance);
        Assert.All(view.Recent.Skip(1), a => Assert.Equal(LoadProvenance.Estimated, a.Load.Provenance));
    }

    /// <summary>An estimated 60-minute session is exactly 120 points (001 FR-013).</summary>
    [Fact]
    public void An_estimated_load_is_moving_time_times_two()
    {
        var view = Build([Fixtures.Session(Fixtures.Today, 60)]);

        Assert.Equal(120m, view.Recent[0].Load.Points);
        Assert.Equal(TimeSpan.FromMinutes(60), view.Recent[0].MovingTime);
        Assert.Equal(ActivityType.Running, view.Recent[0].Type);
    }

    /// <summary>
    ///   Research R22: a session's day is its local day at its <em>own</em> recorded offset,
    ///   matching how feature 002 buckets it (002 FR-003). A ride at 23:30+03:00 on the 16th
    ///   belongs to the 16th, not to the 16th's UTC date of 20:30 the same day — the two agree here,
    ///   but a session at 01:00+03:00 would not.
    /// </summary>
    [Fact]
    public void A_sessions_day_is_its_own_local_day()
    {
        var lateNight = Fixtures.Session(new DateOnly(2026, 9, 16), hour: 23);
        var earlyMorning = Fixtures.Session(new DateOnly(2026, 9, 17), hour: 1);

        var view = Build([lateNight, earlyMorning]);

        Assert.Equal(new DateOnly(2026, 9, 17), view.Recent[0].Day);
        Assert.Equal(new DateOnly(2026, 9, 16), view.Recent[1].Day);
    }

    /// <summary>
    ///   The other half of the future-dated session from <c>EmptyAndPartialHistoryTests</c>: it is
    ///   listed rather than dropped (research R22).
    /// </summary>
    [Fact]
    public void A_session_dated_in_the_future_is_still_listed()
    {
        var view = Build([Fixtures.Session(Fixtures.Today), Fixtures.Session(Fixtures.Today.AddDays(1))]);

        Assert.Equal(2, view.Recent.Count);
        Assert.Equal(Fixtures.Today.AddDays(1), view.Recent[0].Day);
    }
}
