"""Ported from DailyAggregationTests.cs, WeeklyAggregationTests.cs and LoadBasisTests.cs (002)."""

from datetime import date, datetime, time, timedelta, timezone
from decimal import Decimal

import pytest

from tla.domain.activity import ActivityType, TrainingActivity, training_load
from tla.domain.aggregation import DailyTrainingLoad, LoadBasis, WeeklyTrainingLoad, aggregate_daily, aggregate_weekly
from tla.domain.date_range import DateRange
from tla.domain.heart_rate import HeartRateSample, HeartRateSeries
from tla.domain.iso_week import IsoWeek

MAX_HR = 190


def tz(hours: float) -> timezone:
    return timezone(timedelta(hours=hours))


def at(day: date, hour: int = 8, offset: float = 2) -> datetime:
    return datetime.combine(day, time(hour), tz(offset))


def estimated(id: str, start: datetime | date, minutes: int) -> TrainingActivity:
    started = at(start) if not isinstance(start, datetime) else start
    return TrainingActivity(id, started, timedelta(minutes=minutes), ActivityType.RUNNING)


def measured(id: str, day: date) -> TrainingActivity:
    # 80 points at 190: 10 minutes at zone 3 and 10 at zone 5.
    series = HeartRateSeries([HeartRateSample(timedelta(0), 150), HeartRateSample(timedelta(minutes=10), 175), HeartRateSample(timedelta(minutes=20), 160)])
    return TrainingActivity(id, at(day), timedelta(minutes=20), ActivityType.RUNNING, series)


def zero_scoring(id: str, day: date) -> TrainingActivity:
    series = HeartRateSeries([HeartRateSample(timedelta(0), 80), HeartRateSample(timedelta(minutes=30), 80)])
    return TrainingActivity(id, at(day), timedelta(minutes=30), ActivityType.RUNNING, series)


def d(month: int, day: int, year: int = 2026) -> date:
    return date(year, month, day)


# Daily ---------------------------------------------------------------------------------------

def test_several_activities_on_the_same_day_are_summed_into_that_days_total():
    daily = aggregate_daily([estimated("A-1", d(3, 2), 15), estimated("A-2", d(3, 2), 20), estimated("A-3", d(3, 2), 25)], DateRange(d(3, 2), d(3, 2)), MAX_HR)

    assert daily == [DailyTrainingLoad(d(3, 2), Decimal("120"), 3, LoadBasis.ESTIMATED)]


def test_every_day_in_the_range_is_reported_including_days_with_no_training():
    daily = aggregate_daily([estimated("A-1", d(3, 2), 15)], DateRange(d(3, 1), d(3, 5)), MAX_HR)

    assert [x.day for x in daily] == [d(3, 1), d(3, 2), d(3, 3), d(3, 4), d(3, 5)]
    assert [x.points for x in daily] == [0, 30, 0, 0, 0]
    assert daily[0] == DailyTrainingLoad(d(3, 1), Decimal(0), 0, LoadBasis.NONE)


def test_activities_outside_the_range_contribute_to_no_daily_total():
    daily = aggregate_daily([estimated("A-1", d(2, 28), 15), estimated("A-2", d(3, 6), 20)], DateRange(d(3, 1), d(3, 5)), MAX_HR)

    assert len(daily) == 5
    assert all(x.points == 0 for x in daily)


def test_activities_on_the_first_and_last_day_of_the_range_are_both_included():
    daily = aggregate_daily([estimated("A-1", d(3, 1), 15), estimated("A-2", d(3, 5), 20)], DateRange(d(3, 1), d(3, 5)), MAX_HR)

    assert (daily[0].points, daily[4].points) == (30, 40)


def test_an_empty_activity_collection_still_yields_the_full_zero_series():
    daily = aggregate_daily([], DateRange(d(3, 1), d(3, 5)), MAX_HR)

    assert len(daily) == 5 and all(x.points == 0 and x.basis is LoadBasis.NONE for x in daily)


def test_an_activity_is_attributed_to_its_own_offset_local_day_not_its_utc_day():
    started = datetime(2026, 3, 2, 0, 30, tzinfo=tz(2))  # 2026-03-01T22:30Z

    daily = aggregate_daily([estimated("A-1", started, 15)], DateRange(d(3, 1), d(3, 2)), MAX_HR)

    assert [x.points for x in daily] == [0, 30]


@pytest.mark.parametrize("offset", [5.5, 5.75])
def test_sessions_near_local_midnight_at_fractional_offsets_land_on_their_local_day(offset):
    late = datetime(2026, 9, 17, 23, 50, tzinfo=tz(offset))
    early = datetime(2026, 9, 18, 0, 15, tzinfo=tz(offset))

    daily = aggregate_daily([estimated("A-1", late, 15), estimated("A-2", early, 20)], DateRange(d(9, 17), d(9, 18)), MAX_HR)

    assert [x.points for x in daily] == [30, 40]


def test_two_activities_on_the_same_local_day_at_different_offsets_both_count_to_that_day():
    far = datetime(2026, 3, 2, 8, tzinfo=tz(14))
    home = datetime(2026, 3, 2, 20, tzinfo=tz(-11))

    daily = aggregate_daily([estimated("A-1", far, 15), estimated("A-2", home, 20)], DateRange(d(3, 1), d(3, 4)), MAX_HR)

    assert [x.points for x in daily] == [0, 70, 0, 0]


def test_totals_and_ordering_do_not_depend_on_the_order_activities_were_supplied():
    a, b, c = estimated("A-1", d(3, 2), 15), estimated("A-2", d(3, 4), 20), estimated("A-3", d(3, 2), 25)
    r = DateRange(d(3, 1), d(3, 5))

    assert aggregate_daily([a, b, c], r, MAX_HR) == aggregate_daily([c, a, b], r, MAX_HR)
    assert aggregate_daily([c, a, b], r, MAX_HR)[1].points == 80


def test_a_days_total_is_the_exact_sum_of_its_activities_own_load_values():
    activities = [estimated("A-1", d(3, 2), 37), estimated("A-2", d(3, 2), 53), estimated("A-3", d(3, 2), 11)]

    [day] = aggregate_daily(activities, DateRange(d(3, 2), d(3, 2)), MAX_HR)

    assert day.points == sum((training_load(a, MAX_HR).points for a in activities), Decimal(0))


def test_two_activities_sharing_an_external_identifier_both_contribute():
    [day] = aggregate_daily([estimated("A-1", d(3, 2), 15), estimated("A-1", d(3, 2), 15)], DateRange(d(3, 2), d(3, 2)), MAX_HR)

    assert day.points == 60


# Weekly --------------------------------------------------------------------------------------

def test_activities_in_the_same_iso_week_are_summed_into_that_week():
    weekly = aggregate_weekly([estimated("A-1", d(3, 2), 15), estimated("A-2", d(3, 4), 20), estimated("A-3", d(3, 6), 25)], DateRange(d(3, 2), d(3, 8)), MAX_HR)

    assert weekly == [WeeklyTrainingLoad(IsoWeek.for_day(d(3, 2)), Decimal("120"), 3, LoadBasis.ESTIMATED)]


def test_a_sunday_and_the_monday_after_it_fall_in_different_weeks():
    weekly = aggregate_weekly([estimated("A-1", d(3, 1), 15), estimated("A-2", d(3, 2), 20)], DateRange(d(3, 1), d(3, 8)), MAX_HR)

    assert [(w.week.year, w.week.week, w.points) for w in weekly] == [(2026, 9, 30), (2026, 10, 40)]


def test_every_week_the_range_touches_is_reported_including_weeks_with_no_training():
    weekly = aggregate_weekly([estimated("A-1", d(3, 3), 15), estimated("A-2", d(3, 10), 20), estimated("A-3", d(3, 24), 25)], DateRange(d(3, 2), d(3, 29)), MAX_HR)

    assert [w.week.week for w in weekly] == [10, 11, 12, 13]
    assert [w.points for w in weekly] == [30, 40, 0, 50]


def test_a_weekly_total_covers_its_whole_week_including_days_outside_the_range():
    monday = estimated("A-1", d(3, 2), 30)
    r = DateRange(d(3, 4), d(3, 5))

    [week] = aggregate_weekly([monday], r, MAX_HR)
    daily = aggregate_daily([monday], r, MAX_HR)

    assert (week.week.monday, week.week.sunday, week.points) == (d(3, 2), d(3, 8), 60)
    assert [x.day for x in daily] == [d(3, 4), d(3, 5)] and all(x.points == 0 for x in daily)


@pytest.mark.parametrize(("start", "end"), [(d(3, 4), d(3, 5)), (d(3, 4), d(4, 10)), (d(1, 1), d(6, 30)), (d(12, 30), d(1, 5, 2027))])
def test_every_weekly_entry_covers_seven_days_monday_to_sunday(start, end):
    weekly = aggregate_weekly([], DateRange(start, end), MAX_HR)

    assert weekly and all(w.week.monday.weekday() == 0 and w.week.sunday == w.week.monday + timedelta(days=6) for w in weekly)


def test_weeks_stay_consecutive_across_the_iso_year_boundary():
    weekly = aggregate_weekly([estimated("A-1", d(1, 3, 2027), 15)], DateRange(d(12, 28), d(1, 17, 2027)), MAX_HR)

    assert [(w.week.year, w.week.week) for w in weekly] == [(2026, 53), (2027, 1), (2027, 2)]
    assert [w.points for w in weekly] == [30, 0, 0]


# Basis ---------------------------------------------------------------------------------------

def test_a_day_whose_activities_are_all_measured_reports_a_measured_total():
    [day] = aggregate_daily([measured("A-1", d(3, 2)), measured("A-2", d(3, 2))], DateRange(d(3, 2), d(3, 2)), MAX_HR)

    assert (day.points, day.basis) == (160, LoadBasis.MEASURED)


def test_a_day_mixing_measured_and_estimated_activities_reports_a_mixed_total():
    [day] = aggregate_daily([measured("A-1", d(3, 2)), estimated("A-2", d(3, 2), 15)], DateRange(d(3, 2), d(3, 2)), MAX_HR)

    assert (day.points, day.basis) == (110, LoadBasis.MIXED)


def test_a_rest_day_and_a_day_of_zero_scoring_training_are_distinguishable():
    [rest] = aggregate_daily([], DateRange(d(3, 2), d(3, 2)), MAX_HR)
    [trained] = aggregate_daily([zero_scoring("A-1", d(3, 2))], DateRange(d(3, 2), d(3, 2)), MAX_HR)

    assert rest == DailyTrainingLoad(d(3, 2), Decimal(0), 0, LoadBasis.NONE)
    assert trained == DailyTrainingLoad(d(3, 2), Decimal(0), 1, LoadBasis.MEASURED)


def test_a_week_with_one_estimated_activity_among_measured_ones_is_mixed():
    activities = [measured("A-1", d(3, 2)), measured("A-2", d(3, 4)), measured("A-3", d(3, 6)), estimated("A-4", d(3, 8), 15)]

    [week] = aggregate_weekly(activities, DateRange(d(3, 2), d(3, 8)), MAX_HR)

    assert (week.activity_count, week.basis) == (4, LoadBasis.MIXED)


def test_a_week_reports_its_basis_and_its_session_count():
    weekly = aggregate_weekly([measured("A-1", d(3, 3)), measured("A-2", d(3, 5))], DateRange(d(3, 2), d(3, 15)), MAX_HR)

    assert weekly == [
        WeeklyTrainingLoad(IsoWeek.for_day(d(3, 2)), Decimal(160), 2, LoadBasis.MEASURED),
        WeeklyTrainingLoad(IsoWeek.for_day(d(3, 9)), Decimal(0), 0, LoadBasis.NONE),
    ]
