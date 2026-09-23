"""The /api/dashboard body (http-api.md §2), built from the view."""

from datetime import date, datetime, time, timedelta, timezone

from tla.dashboard.view_builder import build_dashboard_view
from tla.dashboard.view_json import to_json
from tla.domain.activity import ActivityType, TrainingActivity

TODAY = date(2026, 9, 18)
PLUS_THREE = timezone(timedelta(hours=3))


def sessions(days):
    return [TrainingActivity(f"s{d}", datetime.combine(TODAY - timedelta(days=d), time(7), PLUS_THREE), timedelta(minutes=60), ActivityType.RUNNING) for d in days]


def test_the_keys_and_nesting_are_exactly_the_contract():
    body = to_json(build_dashboard_view(sessions(range(40)), TODAY, 190, True))

    assert list(body) == ["asOf", "isoWeek", "maximumHeartRate", "isStravaConnected", "isUnavailable", "hasActivities",
                          "hasEnoughHistoryForChart", "current", "week", "days", "recent"]
    assert list(body["current"]) == ["fitness", "fatigue", "form"]
    assert list(body["current"]["fitness"]) == ["value", "qualifiers"]
    assert list(body["week"]) == ["points", "trend"] and list(body["week"]["trend"]) == ["change", "percent", "judgement"]
    assert list(body["days"][0]) == ["day", "fitness", "fatigue", "form", "load", "display"]
    assert list(body["days"][0]["display"]) == ["fitness", "fatigue", "form", "load"]
    assert list(body["recent"][0]) == ["day", "type", "movingTime", "provenance", "load"]


def test_every_displayed_value_is_a_string_and_only_the_geometry_inputs_are_numbers():
    body = to_json(build_dashboard_view(sessions(range(40)), TODAY, 190, True))

    assert body["maximumHeartRate"] == "190"
    assert all(isinstance(v, (int, float)) and not isinstance(v, bool) for d in body["days"] for v in (d["fitness"], d["fatigue"], d["form"], d["load"]))
    strings = [body["asOf"], body["isoWeek"], body["week"]["points"], *[d["day"] for d in body["days"]], *body["days"][0]["display"].values(), *body["recent"][0].values()]
    assert all(isinstance(s, str) for s in strings)


def test_no_activities_gives_no_current_figure_a_dash_for_the_week_and_no_trend():
    body = to_json(build_dashboard_view([], TODAY, 190, False))

    assert (body["current"], body["week"], body["days"], body["recent"]) == (None, {"points": "—", "trend": None}, [], [])
