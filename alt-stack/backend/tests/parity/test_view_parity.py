"""SC-002 at the API: every history golden's `view`, from the Python builder and to_json.

Every string and boolean exactly; the raw geometry numbers within parity.md §5.
"""

from datetime import date
from decimal import Decimal

import pytest

from tests.golden import assert_float_close, history_fixture, history_names, load_history
from tests.parity.histories import activities
from tla.dashboard.view_builder import build_dashboard_view
from tla.dashboard.view_json import to_json


def body_for(name: str) -> dict:
    fixture = history_fixture(name)
    view = build_dashboard_view(activities(fixture), date.fromisoformat(fixture["today"]), fixture["maximumHeartRate"], fixture["isStravaConnected"])
    return to_json(view)


def without_numbers(view: dict) -> dict:
    return {**view, "days": [{k: v for k, v in d.items() if k not in ("fitness", "fatigue", "form", "load")} for d in view["days"]]}


@pytest.mark.parametrize("name", history_names())
def test_every_string_and_boolean_matches_the_reference(name):
    assert without_numbers(body_for(name)) == without_numbers(load_history(name)["view"])


@pytest.mark.parametrize("name", history_names())
def test_the_geometry_numbers_match_within_tolerance(name):
    actual, expected = body_for(name)["days"], load_history(name)["view"]["days"]

    assert len(actual) == len(expected)
    for a, e in zip(actual, expected, strict=True):
        for key in ("fitness", "fatigue", "form"):
            assert_float_close(a[key], e[key])
        assert abs(Decimal(str(a["load"])) - Decimal(str(e["load"]))) <= Decimal("1e-12")
