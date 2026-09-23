"""Ported from IsoWeekTests.cs (002 FR-002; 004 research R9)."""

from datetime import date, timedelta

import pytest

from tla.domain.iso_week import IsoWeek


def test_a_week_knows_its_iso_year_number_and_both_of_its_ends():
    week = IsoWeek.for_day(date(2026, 3, 2))

    assert (week.year, week.week) == (2026, 10)
    assert week.monday == date(2026, 3, 2)
    assert week.sunday == date(2026, 3, 8)


@pytest.mark.parametrize(
    ("day", "year", "number"),
    [
        (date(2026, 3, 1), 2026, 9),
        (date(2025, 12, 29), 2026, 1),
        (date(2027, 1, 3), 2026, 53),
        (date(2027, 1, 4), 2027, 1),
        (date(2026, 12, 31), 2026, 53),
        (date(2027, 1, 1), 2026, 53),
        (date(2021, 1, 3), 2020, 53),
    ],
)
def test_a_week_at_the_year_boundary_is_numbered_by_its_iso_year_not_its_calendar_year(day, year, number):
    week = IsoWeek.for_day(day)

    assert (week.year, week.week) == (year, number)


def test_2026_w53_opens_on_monday_2026_12_28():
    assert IsoWeek.for_day(date(2026, 12, 31)).monday == date(2026, 12, 28)


def test_walking_mondays_crosses_the_fifty_third_week_of_2026_without_a_gap():
    week53 = IsoWeek.for_day(date(2026, 12, 28))
    following = IsoWeek.for_day(week53.monday + timedelta(days=7))

    assert (following.year, following.week) == (2027, 1)
    assert following.monday == week53.sunday + timedelta(days=1)


def test_weeks_are_ordered_by_their_monday_never_by_year_and_number():
    w53 = IsoWeek.for_day(date(2026, 12, 28))
    w01 = IsoWeek.for_day(date(2027, 1, 4))
    w02_2026 = IsoWeek.for_day(date(2026, 1, 5))

    assert sorted([w01, w53, w02_2026]) == [w02_2026, w53, w01]
    assert w53 < w01


def test_the_designation_is_year_dash_w_and_two_digits():
    assert IsoWeek.for_day(date(2026, 9, 18)).designation == "2026-W38"
    assert IsoWeek.for_day(date(2026, 3, 2)).designation == "2026-W10"
    assert IsoWeek.for_day(date(2027, 1, 1)).designation == "2026-W53"
