"""The /api/dashboard body: exactly http-api.md §2, every displayed value a finished string."""

from tla.dashboard import display
from tla.dashboard.view_builder import DashboardView
from tla.domain.activity import LoadProvenance
from tla.domain.aggregation import LoadBasis
from tla.domain.metrics import DailyTrainingMetrics
from tla.domain.trends import TrendClassification

_JUDGEMENT = {
    TrendClassification.SIGNIFICANT_INCREASE: "Significant increase",
    TrendClassification.SIGNIFICANT_DECREASE: "Significant decrease",
    TrendClassification.STEADY: "Steady",
}


def to_json(view: DashboardView) -> dict:
    current = view.current
    trend = view.trend
    return {
        "asOf": display.day(view.as_of),
        "isoWeek": view.iso_week,
        "maximumHeartRate": str(view.maximum_heart_rate),
        "isStravaConnected": view.is_strava_connected,
        "isUnavailable": view.is_unavailable,
        "hasActivities": view.has_activities,
        "hasEnoughHistoryForChart": view.has_enough_history_for_chart,
        "current": None if current is None else {
            "fitness": _figure(current.fitness, current, current.fitness_basis),
            "fatigue": _figure(current.fatigue, current, current.fatigue_basis),
            "form": _figure(current.form, current, current.form_basis),
        },
        "week": {
            "points": display.points(None if view.current_week is None else view.current_week.points),
            "trend": None if trend is None else {
                "change": display.week_change(trend.absolute_change),
                "percent": display.percent(trend.relative_change),
                "judgement": _JUDGEMENT.get(trend.classification, "Week in progress"),
            },
        },
        "days": [
            {
                "day": display.day(m.day),
                "fitness": m.fitness,
                "fatigue": m.fatigue,
                "form": m.form,
                "load": float(load.points),
                "display": {
                    "fitness": display.metric(m.fitness),
                    "fatigue": display.metric(m.fatigue),
                    "form": display.metric(m.form),
                    "load": display.points(load.points),
                },
            }
            for m, load in zip(view.metrics, view.daily_load, strict=True)
        ],
        "recent": [
            {
                "day": display.day(r.day),
                "type": r.type.value,
                "movingTime": display.duration(r.moving_time),
                "provenance": "measured" if r.load.provenance is LoadProvenance.MEASURED else "estimated",
                "load": display.points(r.load.points),
            }
            for r in view.recent
        ],
    }


def _figure(value: float, current: DailyTrainingMetrics, basis: LoadBasis) -> dict:
    """MetricRow.Qualifiers: "still settling" first, then the basis, in rendering order."""
    qualifiers = []
    if not current.is_reliable:
        qualifiers.append("still settling")
    if basis is LoadBasis.MIXED:
        qualifiers.append("partly estimated")
    elif basis is LoadBasis.ESTIMATED:
        qualifiers.append("estimated")
    return {"value": display.metric(value), "qualifiers": qualifiers}
