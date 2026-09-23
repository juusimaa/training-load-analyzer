"""Ported from DisplayFormatTests.cs, and every probe in golden/probes/display.json (research R2)."""

from datetime import date, timedelta
from decimal import Decimal

import pytest

from tests.golden import load_probes
from tla.dashboard import display

PROBES = load_probes()


@pytest.mark.parametrize(
    ("value", "expected"),
    [(2.8233976017308082, "2.8"), (15.974652029978209, "16.0"), (-13.151254428247402, "-13.2"), (0.0, "0.0"),
     (45.25, "45.3"), (0.15, "0.2"), (12.349999999999999, "12.4"), (-0.25, "-0.3"), (-13.25, "-13.3")],
)
def test_a_figure_renders_to_one_decimal_place_rounding_half_away_from_zero(value, expected):
    assert display.metric(value) == expected


def test_a_negative_double_that_rounds_to_zero_keeps_its_sign():
    assert display.metric(-0.04) == "-0.0"


def test_a_negative_decimal_that_rounds_to_zero_drops_its_sign():
    assert display.points(Decimal("-0.04")) == "0.0"


def test_a_missing_figure_renders_as_a_dash():
    assert display.metric(None) == "—"
    assert display.points(None) == "—"
    assert display.percent(None) == "—"


def test_training_load_renders_to_one_decimal_place():
    assert display.points(Decimal(480)) == "480.0"


@pytest.mark.parametrize(("fraction", "expected"), [(Decimal("-0.004"), "0%"), (Decimal("0.005"), "+1%"), (Decimal("0.3333"), "+33%")])
def test_a_percentage_is_whole_signed_and_uses_the_zero_section(fraction, expected):
    assert display.percent(fraction) == expected


def test_the_week_change_caption_of_a_tiny_fall_carries_no_sign():
    assert display.week_change(Decimal("-0.04")) == "0.0"
    assert display.week_change(Decimal(0)) == "+0.0"


def test_a_duration_under_an_hour_is_minutes_and_over_it_hours_and_padded_minutes():
    assert display.duration(timedelta(minutes=45)) == "45m"
    assert display.duration(timedelta(hours=1, minutes=5)) == "1h 05m"


def test_a_day_is_iso_formatted():
    assert display.day(date(2026, 9, 18)) == "2026-09-18"


# Every probe the reference was run on ---------------------------------------------------------

@pytest.mark.parametrize("probe", PROBES["metric"], ids=lambda p: p["input"])
def test_metric_matches_the_reference_on_every_probe(probe):
    assert display.metric(float(probe["input"])) == probe["output"]


@pytest.mark.parametrize("probe", PROBES["points"], ids=lambda p: p["input"])
def test_points_matches_the_reference_on_every_probe(probe):
    assert display.points(Decimal(probe["input"])) == probe["output"]


@pytest.mark.parametrize("probe", PROBES["percent"], ids=lambda p: p["input"])
def test_percent_matches_the_reference_on_every_probe(probe):
    assert display.percent(Decimal(probe["input"])) == probe["output"]


@pytest.mark.parametrize("probe", PROBES["weekChange"], ids=lambda p: p["input"])
def test_week_change_matches_the_reference_on_every_probe(probe):
    assert display.week_change(Decimal(probe["input"])) == probe["output"]


@pytest.mark.parametrize("probe", PROBES["duration"], ids=lambda p: str(p["seconds"]))
def test_duration_matches_the_reference_on_every_probe(probe):
    assert display.duration(timedelta(seconds=probe["seconds"])) == probe["output"]


@pytest.mark.parametrize("probe", PROBES["day"], ids=lambda p: p["input"])
def test_day_matches_the_reference_on_every_probe(probe):
    assert display.day(date.fromisoformat(probe["input"])) == probe["output"]


def test_the_missing_marker_is_the_reference_s():
    assert display.MISSING == PROBES["missing"]
