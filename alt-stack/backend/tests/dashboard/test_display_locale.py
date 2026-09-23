"""SC-007: nothing is formatted with the ambient locale.

Python ignores LANG/LC_ALL until setlocale is called, so the fixture sets a comma-decimal locale
explicitly and restores the previous one afterwards.
"""

import locale
from datetime import date, timedelta
from decimal import Decimal

import pytest

from tests.golden import load_probes
from tla.dashboard import display

PROBES = load_probes()


@pytest.fixture
def finnish():
    previous = locale.setlocale(locale.LC_ALL)
    try:
        locale.setlocale(locale.LC_ALL, "fi_FI.UTF-8")
    except locale.Error:
        pytest.skip("the fi_FI.UTF-8 locale is not installed on this machine")
    try:
        yield
    finally:
        locale.setlocale(locale.LC_ALL, previous)


def test_the_locale_really_changed(finnish):
    assert locale.format_string("%.1f", 1.5) == "1,5"


def test_every_probe_is_unchanged_under_a_comma_decimal_locale(finnish):
    assert [display.metric(float(p["input"])) for p in PROBES["metric"]] == [p["output"] for p in PROBES["metric"]]
    assert [display.points(Decimal(p["input"])) for p in PROBES["points"]] == [p["output"] for p in PROBES["points"]]
    assert [display.percent(Decimal(p["input"])) for p in PROBES["percent"]] == [p["output"] for p in PROBES["percent"]]
    assert [display.week_change(Decimal(p["input"])) for p in PROBES["weekChange"]] == [p["output"] for p in PROBES["weekChange"]]
    assert [display.duration(timedelta(seconds=p["seconds"])) for p in PROBES["duration"]] == [p["output"] for p in PROBES["duration"]]
    assert [display.day(date.fromisoformat(p["input"])) for p in PROBES["day"]] == [p["output"] for p in PROBES["day"]]


def test_the_whole_dashboard_body_for_h3f_is_unchanged_under_a_comma_decimal_locale(finnish):
    from datetime import date

    from tests.golden import history_fixture, load_history
    from tests.parity.histories import activities
    from tla.dashboard.view_builder import build_dashboard_view
    from tla.dashboard.view_json import to_json

    fixture = history_fixture("H3f")
    body = to_json(build_dashboard_view(activities(fixture), date.fromisoformat(fixture["today"]), 190, True))

    assert {k: v for k, v in body.items() if k != "days"} == {k: v for k, v in load_history("H3f")["view"].items() if k != "days"}
    assert [d["display"] for d in body["days"]] == [d["display"] for d in load_history("H3f")["view"]["days"]]
