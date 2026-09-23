"""Ported from tests/TrainingLoadAnalyzer.Domain.Tests/HeartRateSeriesTests.cs (001 FR-007, FR-021, FR-022)."""

from datetime import timedelta
from decimal import Decimal

import pytest

from tla.domain.heart_rate import HeartRateSample, HeartRateSeries


def minutes(n: int) -> timedelta:
    return timedelta(minutes=n)


def worked_example() -> list[HeartRateSample]:
    return [
        HeartRateSample(minutes(0), 150),
        HeartRateSample(minutes(10), 175),
        HeartRateSample(minutes(20), 160),
    ]


def test_a_series_returns_its_samples_in_their_original_order_with_their_original_values():
    samples = worked_example()

    series = HeartRateSeries(samples)

    assert list(series.samples) == samples


def test_the_worked_example_yields_exactly_eighty_trimp_points():
    # At maximum 190: 150 bpm is 78.9% (zone 3) held 10 minutes = 30; 175 bpm is 92.1% (zone 5)
    # held 10 minutes = 50; the final sample has no successor.
    assert HeartRateSeries(worked_example()).trimp_points(190) == Decimal("80")


def test_a_single_sample_series_yields_zero_points():
    assert HeartRateSeries([HeartRateSample(minutes(0), 175)]).trimp_points(190) == Decimal("0")


def test_a_long_gap_is_charged_to_the_sample_preceding_it():
    series = HeartRateSeries([
        HeartRateSample(minutes(0), 150),  # zone 3 at maximum 190
        HeartRateSample(minutes(60), 100),  # 60-minute gap held at 150
        HeartRateSample(minutes(70), 100),  # below 50%: weight 0
    ])

    # 60 minutes at weight 3 = 180; the 10 minutes at 100 bpm (52.6%, weight 1) = 10.
    assert series.trimp_points(190) == Decimal("190")


@pytest.mark.parametrize("bpm", [19, 251, 300])
def test_a_sample_outside_the_plausible_range_is_refused_and_named(bpm):
    with pytest.raises(ValueError) as refusal:
        HeartRateSeries([HeartRateSample(minutes(0), 150), HeartRateSample(minutes(10), bpm)])

    assert str(refusal.value) == f"A heart-rate sample of {bpm} bpm is outside the plausible range of 20-250 bpm."


@pytest.mark.parametrize("bpm", [20, 250])
def test_the_ends_of_the_plausible_range_are_accepted(bpm):
    series = HeartRateSeries([HeartRateSample(minutes(0), bpm)])

    assert series.samples[0].bpm == bpm


@pytest.mark.parametrize(
    ("first", "second", "expected"),
    [
        # decreasing
        (10, 5, "the sample at index 1 (00:05:00) does not follow the one before it (00:10:00)."),
        # equal, and therefore not ascending
        (10, 10, "the sample at index 1 (00:10:00) does not follow the one before it (00:10:00)."),
    ],
)
def test_samples_that_are_not_in_ascending_time_order_are_refused(first, second, expected):
    with pytest.raises(ValueError) as refusal:
        HeartRateSeries([HeartRateSample(minutes(first), 150), HeartRateSample(minutes(second), 160)])

    assert str(refusal.value) == "Heart-rate sample times must be in ascending order; " + expected


def test_the_ascending_order_rule_is_checked_before_the_plausible_range():
    # The reference checks every time before any value, so a series breaking both reports the order.
    with pytest.raises(ValueError, match="ascending order"):
        HeartRateSeries([HeartRateSample(minutes(10), 300), HeartRateSample(minutes(5), 150)])


@pytest.mark.parametrize("samples", [None, []])
def test_a_null_or_empty_sample_list_is_refused(samples):
    with pytest.raises(ValueError) as refusal:
        HeartRateSeries(samples)

    assert str(refusal.value) == (
        "A heart-rate series must contain at least one sample; a session either has "
        "heart-rate data or has none."
    )


def test_mutating_the_list_a_series_was_built_from_does_not_change_the_series():
    samples = [HeartRateSample(minutes(0), 150), HeartRateSample(minutes(10), 160)]
    series = HeartRateSeries(samples)

    samples[1] = HeartRateSample(minutes(5), 999)

    assert series.samples[1] == HeartRateSample(minutes(10), 160)


def test_writing_through_the_samples_a_series_exposes_is_refused():
    series = HeartRateSeries([HeartRateSample(minutes(0), 150), HeartRateSample(minutes(10), 160)])

    with pytest.raises(TypeError):
        series.samples[1] = HeartRateSample(minutes(5), 999)  # type: ignore[index]

    assert series.samples[1].bpm == 160
