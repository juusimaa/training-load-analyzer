"""Ported from DateRangeTests.cs (002 FR-019 – FR-021)."""

from datetime import date

import pytest

from tla.domain.date_range import DateRange


def test_a_range_whose_end_precedes_its_start_is_refused_with_both_dates():
    with pytest.raises(ValueError) as refusal:
        DateRange(date(2026, 3, 5), date(2026, 3, 1))

    assert str(refusal.value) == "The range ends on 2026-03-01, before it starts on 2026-03-05."


def test_a_range_with_a_missing_start_is_refused():
    with pytest.raises(ValueError) as refusal:
        DateRange(None, date(2026, 3, 5))  # type: ignore[arg-type]

    assert str(refusal.value) == "The range starts unbounded. A range must be bounded at both ends."


def test_a_range_with_a_missing_end_is_refused_as_missing_not_as_backwards():
    with pytest.raises(ValueError) as refusal:
        DateRange(date(2026, 3, 1), None)  # type: ignore[arg-type]

    assert str(refusal.value) == "The range ends unbounded. A range must be bounded at both ends."


def test_a_missing_start_wins_over_every_other_rule():
    # The order is load-bearing (002 FR-021): both bounds missing reports the start.
    with pytest.raises(ValueError, match="starts unbounded"):
        DateRange(None, None)  # type: ignore[arg-type]


def test_a_range_of_one_day_is_accepted():
    assert list(DateRange(date(2026, 3, 2), date(2026, 3, 2)).days()) == [date(2026, 3, 2)]


def test_every_day_is_listed_ascending_both_ends_included():
    assert list(DateRange(date(2026, 2, 27), date(2026, 3, 2)).days()) == [
        date(2026, 2, 27), date(2026, 2, 28), date(2026, 3, 1), date(2026, 3, 2)]


def test_a_date_range_cannot_be_changed_after_construction():
    range_ = DateRange(date(2026, 3, 1), date(2026, 3, 5))

    with pytest.raises(AttributeError):
        range_.end = date(2026, 3, 9)  # type: ignore[misc]
