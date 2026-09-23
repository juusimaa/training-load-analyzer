"""Ported from TrainingMetricsTests.cs, MetricsSeriesTests.cs, MetricsBasisTests.cs and MetricsInputTests.cs (003)."""

import dataclasses
from datetime import date, timedelta
from decimal import Decimal

import pytest

from tla.domain.aggregation import DailyTrainingLoad, LoadBasis
from tla.domain.date_range import DateRange
from tla.domain.metrics import DailyTrainingMetrics, calculate_metrics

TOL = 1e-4
FIRST = date(2026, 3, 1)


def day(n: int) -> date:
    return FIRST + timedelta(days=n)


def training(d: date, points: Decimal = Decimal(100), basis: LoadBasis = LoadBasis.MEASURED) -> DailyTrainingLoad:
    return DailyTrainingLoad(d, points, 1, basis)


def rest(d: date) -> DailyTrainingLoad:
    return DailyTrainingLoad(d, Decimal(0), 0, LoadBasis.NONE)


def run(start: date, days: int, points: int) -> list[DailyTrainingLoad]:
    return [training(start + timedelta(days=i), Decimal(points)) for i in range(days)]


def whole(history: list[DailyTrainingLoad]) -> DateRange:
    return DateRange(history[0].day, history[-1].day)


def varied(days: int) -> list[DailyTrainingLoad]:
    return [rest(day(i)) if i % 4 == 0 else training(day(i), Decimal(30 + i * 7 % 180)) for i in range(days)]


def test_the_first_day_advances_fitness_and_fatigue_from_a_seed_of_zero():
    [m] = calculate_metrics([training(FIRST)], DateRange(FIRST, FIRST))

    assert m.fitness == pytest.approx(2.352831335, abs=TOL)
    assert m.fatigue == pytest.approx(13.31221002, abs=TOL)
    assert m.form == pytest.approx(-10.95937869, abs=TOL)


def test_fitness_follows_the_exponential_factor_and_not_the_reciprocal_approximation():
    history = run(FIRST, 42, 100)

    assert calculate_metrics(history, whole(history))[-1].fitness == pytest.approx(63.212056, abs=TOL)


def test_sustained_unchanging_training_settles_both_metrics_at_the_daily_load():
    history = run(FIRST, 365, 100)

    settled = calculate_metrics(history, whole(history))[-1]

    assert (settled.fitness, settled.fatigue, settled.form) == pytest.approx((99.9832, 100.0, -0.0168), abs=TOL)


def test_a_hard_block_raises_fatigue_further_than_fitness_and_turns_form_negative():
    history = run(FIRST, 200, 100) + run(day(200), 7, 200)

    after = calculate_metrics(history, whole(history))[-1]

    assert (after.fitness, after.fatigue, after.form) == pytest.approx((114.6281, 163.2121, -48.5839), abs=TOL)


def test_a_rest_week_sheds_fatigue_faster_than_fitness_and_turns_form_positive():
    history = run(FIRST, 200, 100) + run(day(200), 7, 0)

    series = calculate_metrics(history, whole(history))

    assert (series[199].fitness, series[199].fatigue) == pytest.approx((99.1451, 100.0), abs=TOL)
    assert (series[-1].fitness, series[-1].fatigue, series[-1].form) == pytest.approx((83.9245, 36.7879, 47.1365), abs=TOL)


def test_form_equals_fitness_minus_fatigue_exactly_on_every_day():
    history = varied(60)

    assert all(m.form == m.fitness - m.fatigue for m in calculate_metrics(history, whole(history)))


def test_form_and_its_basis_are_derived_and_never_stored():
    names = [f.name for f in dataclasses.fields(DailyTrainingMetrics)]

    assert "form" not in names and "form_basis" not in names
    m = calculate_metrics([training(FIRST)], DateRange(FIRST, FIRST))[0]
    assert m.form_basis is m.fitness_basis


def test_only_the_requested_range_is_returned_but_every_earlier_day_feeds_it():
    history = run(FIRST, 30, 100)

    full = calculate_metrics(history, whole(history))
    tail = calculate_metrics(history, DateRange(day(20), day(29)))

    assert [m.day for m in tail] == [day(i) for i in range(20, 30)]
    assert tail == full[20:]


def test_a_figure_is_settling_for_the_first_42_days_and_reliable_from_day_43():
    history = run(FIRST, 50, 100)

    series = calculate_metrics(history, whole(history))

    assert [m.is_reliable for m in series[:42]] == [False] * 42
    assert all(m.is_reliable for m in series[42:])


def test_the_basis_of_fitness_reads_42_days_back_and_of_fatigue_7():
    history = [training(day(i), basis=LoadBasis.ESTIMATED if i == 0 else LoadBasis.MEASURED) for i in range(45)]

    series = calculate_metrics(history, whole(history))

    # Day 0's estimate is inside fitness's window until day 41, and inside fatigue's until day 6.
    assert series[6].fatigue_basis is LoadBasis.MIXED and series[7].fatigue_basis is LoadBasis.MEASURED
    assert series[41].fitness_basis is LoadBasis.MIXED and series[42].fitness_basis is LoadBasis.MEASURED


def test_a_rest_day_contributes_nothing_to_a_basis():
    history = [rest(day(0)), training(day(1)), rest(day(2))]

    series = calculate_metrics(history, whole(history))

    assert series[0].fitness_basis is LoadBasis.NONE
    assert series[2].fitness_basis is LoadBasis.MEASURED


def test_a_history_of_nothing_but_rest_days_is_not_a_refusal():
    history = [rest(day(i)) for i in range(60)]

    assert all(m.fitness == 0.0 and m.fatigue == 0.0 for m in calculate_metrics(history, whole(history)))


def test_an_empty_history_is_refused_and_named_as_empty():
    with pytest.raises(ValueError) as refusal:
        calculate_metrics([], DateRange(day(0), day(5)))

    assert str(refusal.value) == (
        "The history is empty, so it cannot reach back to the requested range's first day of 2026-03-01."
    )


def test_a_history_that_starts_after_the_range_is_refused():
    with pytest.raises(ValueError) as refusal:
        calculate_metrics([training(day(4)), training(day(5))], DateRange(day(0), day(5)))

    assert str(refusal.value) == (
        "The history starts on 2026-03-05, after the requested range starts on 2026-03-01. "
        "The metrics of the range's first day need every day before it."
    )


def test_a_history_that_ends_before_the_range_is_refused():
    with pytest.raises(ValueError) as refusal:
        calculate_metrics(run(FIRST, 20, 100), DateRange(day(0), day(30)))

    assert str(refusal.value) == (
        "The history ends on 2026-03-20, before the requested range ends on 2026-03-31. "
        "The missing days cannot be treated as rest without inventing training history."
    )


@pytest.mark.parametrize(
    ("days", "index", "found", "previous"),
    [
        ([0, 1, 3, 4], 2, "2026-03-04", "2026-03-02"),  # a missing day
        ([0, 1, 1, 2], 2, "2026-03-02", "2026-03-02"),  # a repeated day
        ([0, 1, 3, 2], 2, "2026-03-04", "2026-03-02"),  # days out of order
    ],
)
def test_a_history_that_is_not_continuous_is_refused(days, index, found, previous):
    history = [training(day(i)) for i in days]

    with pytest.raises(ValueError) as refusal:
        calculate_metrics(history, DateRange(day(0), day(2)))

    assert str(refusal.value) == (
        f"The history is not continuous: the day at index {index} is {found}, which does not follow {previous}."
    )
