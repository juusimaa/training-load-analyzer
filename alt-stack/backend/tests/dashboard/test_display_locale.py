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
