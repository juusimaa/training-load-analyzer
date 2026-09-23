"""Ported from DashboardViewBuilderTests.cs, WeeklyLoadAndTrendTests.cs, RecentActivitiesTests.cs and EmptyAndPartialHistoryTests.cs (006, 008)."""

from datetime import date, datetime, time, timedelta, timezone
from decimal import Decimal

import pytest

from tla.dashboard.view_builder import build_dashboard_view
from tla.domain.activity import ActivityType, LoadProvenance, TrainingActivity
from tla.domain.heart_rate import HeartRateSample, HeartRateSeries

TODAY = date(2026, 9, 18)
MAX_HR = 190
PLUS_THREE = timezone(timedelta(hours=3))


def session(day: date, minutes: int = 60, type=ActivityType.RUNNING, series=None, hour: int = 7) -> TrainingActivity:
    return TrainingActivity(f"fixture-{day:%Y%m%d}-{hour:02d}", datetime.combine(day, time(hour), PLUS_THREE), timedelta(minutes=minutes), type, series)


def consecutive(days: int) -> list[TrainingActivity]:
    return [session(TODAY - timedelta(days=i)) for i in range(days)]


def build(activities, today=TODAY, connected=True):
    return build_dashboard_view(activities, today, MAX_HR, connected)


def test_h1_gives_the_hand_computed_figures_for_today():
    view = build([session(TODAY)])

    assert view.current.fitness == pytest.approx(2.8233976017308082, abs=1e-12)
    assert view.current.fatigue == pytest.approx(15.974652029978209, abs=1e-12)
    assert view.current == view.metrics[-1] and view.current.day == TODAY


def test_the_metrics_span_the_180_days_ending_today_gap_free_and_ascending():
    view = build(consecutive(200))

    assert len(view.metrics) == 180
    assert (view.metrics[0].day, view.metrics[-1].day) == (date(2026, 3, 23), TODAY)
    assert all(b.day == a.day + timedelta(days=1) for a, b in zip(view.metrics, view.metrics[1:]))


def test_a_shorter_history_starts_the_chart_at_its_first_day():
    view = build(consecutive(45))

    assert (len(view.metrics), view.metrics[0].day) == (45, TODAY - timedelta(days=44))


def test_the_daily_load_is_aligned_day_for_day_with_the_metrics():
    view = build(consecutive(45))

    assert [d.day for d in view.daily_load] == [m.day for m in view.metrics]
    assert all(d.points == Decimal(120) for d in view.daily_load)


def test_a_future_dated_session_extends_the_range_rather_than_failing():
    view = build([session(TODAY - timedelta(days=3)), session(TODAY + timedelta(days=2))])

    assert view.metrics[-1].day == TODAY
    assert view.recent[0].day == TODAY + timedelta(days=2)


def test_the_recent_list_holds_at_most_seven_newest_first():
    view = build(consecutive(10))

    assert [r.day for r in view.recent] == [TODAY - timedelta(days=i) for i in range(7)]
    assert view.recent[0].load.provenance is LoadProvenance.ESTIMATED


def test_enough_history_for_the_chart_is_thirty_days_of_the_180_day_series():
    assert not build(consecutive(29)).has_enough_history_for_chart
    assert build(consecutive(30)).has_enough_history_for_chart


def test_with_no_activities_there_is_no_current_figure_and_nothing_to_show():
    view = build([], connected=False)

    assert (view.has_activities, view.current, view.current_week, view.trend, view.recent) == (False, None, None, None, [])
    assert (view.as_of, view.iso_week, view.maximum_heart_rate, view.is_strava_connected) == (TODAY, "2026-W38", MAX_HR, False)


def test_the_week_and_its_trend_against_the_week_before():
    h2 = [session(date(2026, 9, d)) for d in (7, 9, 11, 14, 15, 16, 17)]

    view = build(h2)

    assert view.current_week.points == Decimal(480)
    assert (view.trend.absolute_change, view.trend.relative_change) == (Decimal(120), Decimal(120) / Decimal(360))


def test_no_trend_when_the_history_does_not_reach_the_previous_week():
    assert build([session(TODAY)]).trend is None


def test_the_iso_week_designation_uses_the_iso_year():
    assert build([], today=date(2027, 1, 1)).iso_week == "2026-W53"


def test_a_measured_session_keeps_its_provenance_in_the_recent_list():
    series = HeartRateSeries([HeartRateSample(timedelta(minutes=i), 150) for i in range(60)])

    [recent] = build([session(TODAY, 45, ActivityType.CYCLING, series)]).recent

    assert (recent.load.points, recent.load.provenance, recent.type, recent.moving_time) == (Decimal(177), LoadProvenance.MEASURED, ActivityType.CYCLING, timedelta(minutes=45))
