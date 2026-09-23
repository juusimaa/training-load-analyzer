"""SC-001: every history golden's loads, totals, metrics and trends, from the Python domain.

Tolerances are parity.md §5 and nothing else: loads and changes within 1e-20, metrics within 1e-4,
everything else exactly.
"""

from datetime import timedelta
from decimal import Decimal

import pytest

from tests.golden import assert_decimal_close, assert_float_close, history_fixture, history_names, load_history
from tests.parity.histories import activities, day
from tla.domain.activity import training_load
from tla.domain.aggregation import aggregate_daily, aggregate_weekly
from tla.domain.date_range import DateRange
from tla.domain.metrics import calculate_metrics
from tla.domain.trends import calculate_trends

NAMES = history_names()


def computed(name: str):
    fixture = history_fixture(name)
    golden = load_history(name)
    acts = activities(fixture)
    maximum = fixture["maximumHeartRate"]
    span = DateRange(day(golden["range"]["start"]), day(golden["range"]["end"]))
    daily = aggregate_daily(acts, span, maximum)
    weekly = aggregate_weekly(acts, span, maximum)
    trend_start = weekly[0].week.monday + timedelta(days=7)
    trends = calculate_trends(weekly, DateRange(trend_start, span.end)) if trend_start <= span.end else []
    return golden, acts, maximum, daily, weekly, calculate_metrics(daily, span), trends


@pytest.mark.parametrize("name", NAMES)
def test_every_load_matches_the_reference(name):
    golden, acts, maximum, *_ = computed(name)

    assert [a.external_id for a in acts] == [g["id"] for g in golden["loads"]]
    for activity, expected in zip(acts, golden["loads"], strict=True):
        load = training_load(activity, maximum)
        assert_decimal_close(load.points, Decimal(expected["points"]))
        assert load.provenance.value == expected["provenance"]


@pytest.mark.parametrize("name", NAMES)
def test_every_daily_total_matches_the_reference(name):
    golden, _, _, daily, *_ = computed(name)

    assert len(daily) == len(golden["daily"])
    for actual, expected in zip(daily, golden["daily"], strict=True):
        assert (actual.day.isoformat(), actual.activity_count, actual.basis.value) == (expected["day"], expected["count"], expected["basis"])
        assert_decimal_close(actual.points, Decimal(expected["points"]))


@pytest.mark.parametrize("name", NAMES)
def test_every_weekly_total_matches_the_reference(name):
    golden, _, _, _, weekly, *_ = computed(name)

    assert len(weekly) == len(golden["weekly"])
    for actual, expected in zip(weekly, golden["weekly"], strict=True):
        assert (actual.week.year, actual.week.week, actual.week.monday.isoformat(), actual.activity_count, actual.basis.value) == (
            expected["year"], expected["week"], expected["monday"], expected["count"], expected["basis"])
        assert_decimal_close(actual.points, Decimal(expected["points"]))


@pytest.mark.parametrize("name", NAMES)
def test_every_days_metrics_match_the_reference(name):
    golden, *_, metrics, _ = computed(name)

    assert len(metrics) == len(golden["metrics"])
    for actual, expected in zip(metrics, golden["metrics"], strict=True):
        assert actual.day.isoformat() == expected["day"]
        assert_float_close(actual.fitness, float(expected["fitness"]))
        assert_float_close(actual.fatigue, float(expected["fatigue"]))
        assert_float_close(actual.form, float(expected["form"]))
        assert (actual.is_reliable, actual.fitness_basis.value, actual.fatigue_basis.value, actual.form_basis.value) == (
            expected["isReliable"], expected["fitnessBasis"], expected["fatigueBasis"], expected["formBasis"])


@pytest.mark.parametrize("name", NAMES)
def test_every_weekly_trend_matches_the_reference(name):
    golden, *_, trends = computed(name)

    assert len(trends) == len(golden["trends"])
    for actual, expected in zip(trends, golden["trends"], strict=True):
        assert (actual.week.year, actual.week.week, actual.week.monday.isoformat()) == (expected["year"], expected["week"], expected["monday"])
        assert (actual.is_complete, actual.basis.value, actual.classification.value) == (expected["isComplete"], expected["basis"], expected["classification"])
        assert_decimal_close(actual.points, Decimal(expected["points"]))
        assert_decimal_close(actual.previous_points, Decimal(expected["previous"]))
        assert_decimal_close(actual.absolute_change, Decimal(expected["absoluteChange"]))
        if expected["relativeChange"] is None:
            assert actual.relative_change is None
        else:
            assert_decimal_close(actual.relative_change, Decimal(expected["relativeChange"]))


def test_the_one_second_series_is_among_the_goldens():
    # Research R1: a 1 Hz series is where the two decimal types round in different places.
    assert "one-second-hr" in NAMES
