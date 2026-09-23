"""Turns a stored history into the figures one dashboard load shows (006, 008).

Pure, ported from DashboardViewBuilder.cs, DashboardView.cs and RecentActivity.cs: the same
activities, day and maximum always give an equal view, with no clock, storage or ambient state.
"""

from collections.abc import Sequence
from dataclasses import dataclass, field
from datetime import date, timedelta

from tla.domain.activity import ActivityType, TrainingActivity, TrainingLoad, training_load
from tla.domain.aggregation import DailyTrainingLoad, WeeklyTrainingLoad, aggregate_daily, aggregate_weekly
from tla.domain.date_range import DateRange
from tla.domain.iso_week import IsoWeek
from tla.domain.metrics import DailyTrainingMetrics, calculate_metrics
from tla.domain.trends import WeeklyLoadTrend, calculate_trends

# How many days the chart covers, today included (006 FR-006).
_CHART_DAYS = 180

# How many sessions the recent list shows (006 FR-007).
_RECENT_COUNT = 7

# Under 30 days a chart would be a line between two points (006 US3 scenario 2).
_MINIMUM_CHART_DAYS = 30


@dataclass(frozen=True, slots=True)
class RecentActivity:
    """One row of the recent list. The load keeps its provenance with it (001 SC-007)."""

    day: date
    type: ActivityType
    moving_time: timedelta
    load: TrainingLoad


@dataclass(frozen=True, slots=True)
class DashboardView:
    as_of: date
    is_strava_connected: bool
    maximum_heart_rate: int
    iso_week: str
    is_unavailable: bool = False
    metrics: list[DailyTrainingMetrics] = field(default_factory=list)
    current_week: WeeklyTrainingLoad | None = None
    trend: WeeklyLoadTrend | None = None
    daily_load: list[DailyTrainingLoad] = field(default_factory=list)
    recent: list[RecentActivity] = field(default_factory=list)

    @property
    def current(self) -> DailyTrainingMetrics | None:
        """Today's figures: the chart's last point, derived so the two can never disagree."""
        return self.metrics[-1] if self.metrics else None

    @property
    def has_activities(self) -> bool:
        return len(self.metrics) > 0

    @property
    def has_enough_history_for_chart(self) -> bool:
        return len(self.metrics) >= _MINIMUM_CHART_DAYS


def build_dashboard_view(activities: Sequence[TrainingActivity], today: date, maximum_heart_rate: int, is_strava_connected: bool) -> DashboardView:
    if not activities:
        return DashboardView(today, is_strava_connected, maximum_heart_rate, designation(today))

    days = [_day_of(a) for a in activities]
    history_start = min(days)
    # A session dated in the future would otherwise fail the range or vanish from the totals.
    history_end = max(max(days), today)
    history = DateRange(history_start, history_end)

    daily = aggregate_daily(activities, history, maximum_heart_rate)
    chart_start = max(history_start, today - timedelta(days=_CHART_DAYS - 1))
    weekly = aggregate_weekly(activities, history, maximum_heart_rate)
    this_week = IsoWeek.for_day(today)

    return DashboardView(
        as_of=today,
        is_strava_connected=is_strava_connected,
        maximum_heart_rate=maximum_heart_rate,
        iso_week=designation(today),
        metrics=calculate_metrics(daily, DateRange(chart_start, today)),
        daily_load=[d for d in daily if chart_start <= d.day <= today],
        current_week=next((w for w in weekly if w.week == this_week), None),
        trend=_trend_for(weekly, history_start, today),
        recent=_recent_from(activities, maximum_heart_rate),
    )


def designation(day: date) -> str:
    """The ISO week of a day as 2026-W38, from the ISO year (008 Amendment 1(a))."""
    return IsoWeek.for_day(day).designation


def _trend_for(weekly: list[WeeklyTrainingLoad], history_start: date, today: date) -> WeeklyLoadTrend | None:
    """A precondition, not a caught refusal: a first week of training has nothing to compare against."""
    this_monday = IsoWeek.for_day(today).monday
    if IsoWeek.for_day(history_start).monday > this_monday - timedelta(days=7):
        return None
    trends = calculate_trends(weekly, DateRange(this_monday, today))
    return trends[-1] if trends else None


def _recent_from(activities: Sequence[TrainingActivity], maximum_heart_rate: int) -> list[RecentActivity]:
    # Stable, as LINQ's OrderByDescending is: equal starts keep the order they were read in.
    newest = sorted(activities, key=lambda a: a.started_at, reverse=True)[:_RECENT_COUNT]
    return [RecentActivity(_day_of(a), a.type, a.moving_time, training_load(a, maximum_heart_rate)) for a in newest]


def _day_of(activity: TrainingActivity) -> date:
    """The athlete's local day at the session's own offset, exactly as 002 buckets it."""
    return activity.started_at.date()
