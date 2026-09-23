"""Ported from TrainingActivityCreationTests.cs and TrainingActivityValidationTests.cs (001 FR-001 – FR-024)."""

import dataclasses
from datetime import UTC, datetime, timedelta, timezone

import pytest

from tla.domain.activity import ActivityType, TrainingActivity
from tla.domain.heart_rate import HeartRateSample, HeartRateSeries

PLUS_TWO = timezone(timedelta(hours=2))
ANY_START = datetime(2026, 3, 1, 7, 30, tzinfo=PLUS_TWO)
FORTY_FIVE = timedelta(minutes=45)


def test_a_recorded_run_reads_back_every_detail_unchanged():
    activity = TrainingActivity("A-1", ANY_START, FORTY_FIVE, ActivityType.RUNNING)

    assert activity.external_id == "A-1"
    assert activity.started_at == ANY_START
    assert activity.moving_time == FORTY_FIVE
    assert activity.type is ActivityType.RUNNING


def test_a_recorded_ride_classifies_as_cycling_and_is_distinct_from_the_run():
    run = TrainingActivity("A-1", ANY_START, FORTY_FIVE, ActivityType.RUNNING)
    ride = TrainingActivity("A-2", ANY_START, FORTY_FIVE, ActivityType.CYCLING)

    assert ride.type is ActivityType.CYCLING
    assert ride.external_id == "A-2"
    assert run is not ride
    assert run.external_id != ride.external_id


def test_both_the_utc_instant_and_the_athletes_local_start_time_are_recoverable():
    activity = TrainingActivity("A-1", ANY_START, FORTY_FIVE, ActivityType.RUNNING)

    assert activity.started_at.astimezone(UTC) == datetime(2026, 3, 1, 5, 30, tzinfo=UTC)
    assert activity.started_at.replace(tzinfo=None) == datetime(2026, 3, 1, 7, 30)
    assert activity.started_at.utcoffset() == timedelta(hours=2)


def test_the_same_wall_clock_time_at_two_offsets_stays_two_instants_on_one_local_day():
    before = TrainingActivity("A-1", datetime(2026, 10, 25, 3, 30, tzinfo=timezone(timedelta(hours=3))), FORTY_FIVE, ActivityType.RUNNING)
    after = TrainingActivity("A-2", datetime(2026, 10, 25, 3, 30, tzinfo=PLUS_TWO), FORTY_FIVE, ActivityType.RUNNING)

    assert after.started_at - before.started_at == timedelta(hours=1)
    assert before.started_at.date() == after.started_at.date()


def test_an_external_identifier_round_trips_byte_for_byte_including_whitespace():
    activity = TrainingActivity("  A-1  ", ANY_START, FORTY_FIVE, ActivityType.RUNNING)

    assert activity.external_id == "  A-1  "


def test_a_start_time_in_the_future_is_accepted():
    future = datetime.now(UTC) + timedelta(days=1)

    assert TrainingActivity("A-1", future, FORTY_FIVE, ActivityType.RUNNING).started_at == future


def test_a_training_activity_is_frozen():
    activity = TrainingActivity("A-1", ANY_START, FORTY_FIVE, ActivityType.RUNNING)

    with pytest.raises(dataclasses.FrozenInstanceError):
        activity.moving_time = timedelta(minutes=1)  # type: ignore[misc]


def test_an_activity_recorded_without_heart_rate_data_has_no_series_at_all():
    assert TrainingActivity("A-1", ANY_START, FORTY_FIVE, ActivityType.RUNNING).heart_rate is None


def test_an_activity_recorded_with_heart_rate_data_returns_that_series_unchanged():
    series = HeartRateSeries([HeartRateSample(timedelta(minutes=0), 150), HeartRateSample(timedelta(minutes=10), 175)])

    activity = TrainingActivity("A-1", ANY_START, FORTY_FIVE, ActivityType.RUNNING, series)

    assert activity.heart_rate is series


def test_a_series_extending_beyond_the_activitys_moving_time_is_accepted():
    series = HeartRateSeries([HeartRateSample(timedelta(minutes=0), 150), HeartRateSample(timedelta(minutes=90), 160)])

    activity = TrainingActivity("A-1", ANY_START, FORTY_FIVE, ActivityType.RUNNING, series)

    assert activity.heart_rate.samples[-1].time_from_start > activity.moving_time


def test_only_running_and_cycling_exist_displayed_as_the_reference_prints_them():
    assert [t.value for t in ActivityType] == ["Running", "Cycling"]


@pytest.mark.parametrize("moving", [timedelta(0), timedelta(minutes=-1)])
def test_a_moving_time_of_zero_or_less_is_refused(moving):
    with pytest.raises(ValueError) as refusal:
        TrainingActivity("A-1", ANY_START, moving, ActivityType.RUNNING)

    assert str(refusal.value) == "A session's moving time must be a positive span of time."


def test_an_activity_type_outside_the_defined_set_is_refused():
    with pytest.raises(ValueError) as refusal:
        TrainingActivity("A-1", ANY_START, FORTY_FIVE, "Swimming")  # type: ignore[arg-type]

    assert str(refusal.value) == "A session must be classified as either running or cycling."


@pytest.mark.parametrize("start", [None, datetime(2026, 3, 1, 7, 30)], ids=["missing", "naive"])
def test_a_missing_start_time_or_one_without_an_offset_is_refused(start):
    with pytest.raises(ValueError) as refusal:
        TrainingActivity("A-1", start, FORTY_FIVE, ActivityType.RUNNING)  # type: ignore[arg-type]

    assert str(refusal.value) == "A session must have a start time."


@pytest.mark.parametrize("external_id", [None, "", "   "])
def test_a_missing_or_blank_external_identifier_is_refused(external_id):
    with pytest.raises(ValueError) as refusal:
        TrainingActivity(external_id, ANY_START, FORTY_FIVE, ActivityType.RUNNING)  # type: ignore[arg-type]

    assert str(refusal.value) == "A session must carry an external identifier."


def test_the_refusals_are_checked_in_the_reference_order():
    # Identifier, then start, then moving time, then type: each refusal names the first broken rule.
    with pytest.raises(ValueError, match="external identifier"):
        TrainingActivity("", None, timedelta(0), "x")  # type: ignore[arg-type]
    with pytest.raises(ValueError, match="start time"):
        TrainingActivity("A-1", None, timedelta(0), "x")  # type: ignore[arg-type]
    with pytest.raises(ValueError, match="moving time"):
        TrainingActivity("A-1", ANY_START, timedelta(0), "x")  # type: ignore[arg-type]
