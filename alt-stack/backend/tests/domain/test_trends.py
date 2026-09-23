"""Ported from WeeklyLoadTrendTests.cs, TrendClassificationTests.cs, TrendInputTests.cs and TrendReliabilityTests.cs (004)."""

import dataclasses
from datetime import date, timedelta
from decimal import Decimal

import pytest

from tla.domain.aggregation import LoadBasis, WeeklyTrainingLoad
from tla.domain.date_range import DateRange
from tla.domain.iso_week import IsoWeek
from tla.domain.trends import TrendClassification, WeeklyLoadTrend, calculate_trends

W10 = date(2026, 3, 2)


def week(offset: int, base: date = W10) -> IsoWeek:
    return IsoWeek.for_day(base + timedelta(weeks=offset))


def weekly(points: list[int], base: date = W10, basis: LoadBasis = LoadBasis.MEASURED) -> list[WeeklyTrainingLoad]:
    return [WeeklyTrainingLoad(week(i, base), Decimal(p), 1, basis) for i, p in enumerate(points)]


def whole_week(offset: int, base: date = W10) -> DateRange:
    return DateRange(week(offset, base).monday, week(offset, base).sunday)


def trend(previous, current, complete=True) -> WeeklyLoadTrend:
    return WeeklyLoadTrend(week(0), Decimal(current), Decimal(previous), complete, LoadBasis.MEASURED)


# Changes ------------------------------------------------------------------------------------

def test_a_week_reports_its_move_in_points_and_as_a_proportion():
    [t] = calculate_trends(weekly([500, 600]), whole_week(1))

    assert (t.absolute_change, t.relative_change) == (Decimal(100), Decimal("0.2"))


def test_a_week_following_an_idle_one_reports_no_proportion_at_all():
    [t] = calculate_trends(weekly([0, 400]), whole_week(1))

    assert t.relative_change is None


def test_a_fall_and_a_flat_week_both_report_their_change():
    [fall] = calculate_trends(weekly([600, 450]), whole_week(1))
    [flat] = calculate_trends(weekly([600, 600]), whole_week(1))

    assert (fall.absolute_change, fall.relative_change) == (Decimal(-150), Decimal("-0.25"))
    assert (flat.absolute_change, flat.relative_change) == (Decimal(0), Decimal(0))


def test_the_absolute_change_is_exact():
    history = [WeeklyTrainingLoad(week(0), Decimal("395.3"), 1, LoadBasis.MEASURED), WeeklyTrainingLoad(week(1), Decimal("500.0"), 1, LoadBasis.MEASURED)]

    [t] = calculate_trends(history, whole_week(1))

    assert t.absolute_change == Decimal("104.7")


def test_the_changes_and_the_classification_are_derived_and_cannot_be_set():
    names = [f.name for f in dataclasses.fields(WeeklyLoadTrend)]

    assert not {"absolute_change", "relative_change", "classification"} & set(names)


# Classification -----------------------------------------------------------------------------

@pytest.mark.parametrize(
    ("previous", "current", "expected"),
    [
        (500, 600, TrendClassification.SIGNIFICANT_INCREASE),
        (500, 520, TrendClassification.STEADY),
        (1000, 1060, TrendClassification.STEADY),  # a large week moving a small share of itself
        (20, 26, TrendClassification.STEADY),  # a tiny week moving a large share of itself
        (600, 400, TrendClassification.SIGNIFICANT_DECREASE),
        (500, 0, TrendClassification.SIGNIFICANT_DECREASE),
        (0, 400, TrendClassification.SIGNIFICANT_INCREASE),  # the floor alone decides
        (0, 30, TrendClassification.STEADY),
        (0, 0, TrendClassification.STEADY),
        (400, 460, TrendClassification.SIGNIFICANT_INCREASE),  # relative exactly 0.15
        (400, 459, TrendClassification.STEADY),
        (200, 250, TrendClassification.SIGNIFICANT_INCREASE),  # absolute exactly 50
        (200, 249, TrendClassification.STEADY),
        (200, "249.9", TrendClassification.STEADY),
        (200, "250.0000001", TrendClassification.SIGNIFICANT_INCREASE),
    ],
)
def test_the_classification_reads_the_floor_then_the_proportion(previous, current, expected):
    assert trend(previous, current).classification is expected


def test_an_incomplete_week_is_indeterminate_whatever_the_change():
    assert trend(100, 900, complete=False).classification is TrendClassification.INDETERMINATE
    assert trend(500, 510, complete=False).classification is TrendClassification.INDETERMINATE


def test_stopping_altogether_is_a_relative_change_of_minus_one():
    assert trend(500, 0).relative_change == Decimal(-1)


# Series -------------------------------------------------------------------------------------

def test_every_week_in_the_range_reports_once_in_ascending_order():
    trends = calculate_trends(weekly([400, 500, 600, 700, 800]), DateRange(week(1).monday, week(4).sunday))

    assert [t.week for t in trends] == [week(1), week(2), week(3), week(4)]
    assert [t.points for t in trends] == [500, 600, 700, 800]
    assert [t.previous_points for t in trends] == [400, 500, 600, 700]


def test_only_the_weeks_touching_the_requested_range_are_returned():
    [only] = calculate_trends(weekly([400, 500, 600, 700, 800]), whole_week(3))

    assert (only.week, only.points, only.previous_points) == (week(3), 700, 600)


def test_a_week_is_complete_only_when_the_range_covers_all_of_it():
    history = weekly([400, 500, 600])

    trends = calculate_trends(history, DateRange(week(1).monday + timedelta(days=2), week(2).sunday))

    assert [t.is_complete for t in trends] == [False, True]


def test_weeks_are_ordered_across_an_iso_year_boundary():
    base = date(2026, 12, 14)  # 2026-W51
    trends = calculate_trends(weekly([300, 400, 500, 600], base), DateRange(week(1, base).monday, week(3, base).sunday))

    assert [(t.week.year, t.week.week) for t in trends] == [(2026, 52), (2026, 53), (2027, 1)]
    assert [t.previous_points for t in trends] == [300, 400, 500]


def test_the_basis_combines_this_weeks_and_the_previous_weeks():
    history = [
        WeeklyTrainingLoad(week(0), Decimal(400), 1, LoadBasis.ESTIMATED),
        WeeklyTrainingLoad(week(1), Decimal(500), 1, LoadBasis.MEASURED),
        WeeklyTrainingLoad(week(2), Decimal(0), 0, LoadBasis.NONE),
        WeeklyTrainingLoad(week(3), Decimal(500), 1, LoadBasis.MEASURED),
    ]

    trends = calculate_trends(history, DateRange(week(1).monday, week(3).sunday))

    assert [t.basis for t in trends] == [LoadBasis.MIXED, LoadBasis.MEASURED, LoadBasis.MEASURED]


# Refusals -----------------------------------------------------------------------------------

def test_an_empty_history_is_refused():
    with pytest.raises(ValueError) as refusal:
        calculate_trends([], whole_week(1))

    assert str(refusal.value) == (
        "The history is empty, so it cannot reach back to the week before the requested range, beginning 2026-03-02."
    )


def test_a_history_that_does_not_reach_the_week_before_the_range_is_refused():
    with pytest.raises(ValueError) as refusal:
        calculate_trends(weekly([500, 600], W10 + timedelta(weeks=1)), whole_week(1))

    assert str(refusal.value) == (
        "The history starts with the week beginning 2026-03-09, so the week beginning 2026-03-02 is missing. "
        "The range's first week has nothing to be compared against."
    )


def test_a_history_that_ends_before_the_ranges_last_week_is_refused():
    with pytest.raises(ValueError) as refusal:
        calculate_trends(weekly([500, 600]), DateRange(week(1).monday, week(3).sunday))

    assert str(refusal.value) == (
        "The history ends with the week beginning 2026-03-09, before the requested range's last week, "
        "beginning 2026-03-23. The missing weeks cannot be treated as rest without inventing training history."
    )


def test_a_history_with_a_gap_is_refused():
    history = weekly([400, 500, 600, 700])
    history.pop(2)

    with pytest.raises(ValueError) as refusal:
        calculate_trends(history, DateRange(week(1).monday, week(3).sunday))

    assert str(refusal.value) == (
        "The history is not continuous: the week at index 2 begins 2026-03-23, which does not follow "
        "the week beginning 2026-03-09."
    )
