"""Ported from HeartRateZoneTests.cs, MeasuredTrainingLoadTests.cs and EstimatedTrainingLoadTests.cs (001 FR-008 – FR-016)."""

import dataclasses
from datetime import datetime, timedelta, timezone
from decimal import Decimal

import pytest

from tla.domain.activity import ActivityType, LoadProvenance, TrainingActivity, TrainingLoad, training_load
from tla.domain.heart_rate import HeartRateSample, HeartRateSeries, heart_rate_zone_weight

ANY_START = datetime(2026, 3, 1, 7, 30, tzinfo=timezone(timedelta(hours=2)))


def series(*pairs: tuple[int, int]) -> HeartRateSeries:
    return HeartRateSeries([HeartRateSample(timedelta(minutes=m), bpm) for m, bpm in pairs])


def measured(heart_rate: HeartRateSeries, activity_type: ActivityType = ActivityType.RUNNING) -> TrainingActivity:
    return TrainingActivity("A-1", ANY_START, timedelta(minutes=45), activity_type, heart_rate)


def estimated(minutes: int) -> TrainingActivity:
    return TrainingActivity("A-1", ANY_START, timedelta(minutes=minutes), ActivityType.RUNNING)


# Zones, at maximum 200 so every boundary lands on a whole bpm. Lower bound inclusive.
@pytest.mark.parametrize(
    ("bpm", "weight"),
    [(99, 0), (100, 1), (119, 1), (120, 2), (139, 2), (140, 3), (159, 3), (160, 4), (179, 4), (180, 5), (199, 5), (200, 5)],
)
def test_each_zone_boundary_classifies_exactly(bpm, weight):
    assert heart_rate_zone_weight(bpm, 200) == weight


def test_a_heart_rate_above_the_stated_maximum_counts_in_zone_five():
    assert heart_rate_zone_weight(200, 190) == 5


def test_a_session_with_heart_rate_data_reports_a_measured_load_of_eighty_points():
    load = training_load(measured(series((0, 150), (10, 175), (20, 160))), 190)

    assert load.points == Decimal("80")
    assert load.provenance is LoadProvenance.MEASURED


def test_samples_sixty_seconds_apart_yield_the_hand_computed_value_exactly():
    # 59 one-minute intervals at 150 bpm (zone 3 at 190): exactly 177, the reference's H3f session.
    heart_rate = HeartRateSeries([HeartRateSample(timedelta(seconds=60 * i), 150) for i in range(60)])

    assert training_load(measured(heart_rate), 190).points == Decimal("177")


def test_the_longer_of_two_equally_intense_sessions_has_the_strictly_greater_load():
    shorter = training_load(measured(series((0, 150), (30, 150))), 190)
    longer = training_load(measured(series((0, 150), (60, 150))), 190)

    assert longer.points > shorter.points


def test_a_zone_five_session_scores_five_times_a_zone_one_session_of_equal_duration():
    zone_one = training_load(measured(series((0, 100), (30, 100))), 200)
    zone_five = training_load(measured(series((0, 180), (30, 180))), 200)

    assert zone_one.points == Decimal("30")
    assert zone_five.points == Decimal("150")


def test_two_intensities_inside_the_same_zone_produce_equal_loads():
    assert training_load(measured(series((0, 141), (30, 141))), 200) == training_load(measured(series((0, 159), (30, 159))), 200)


def test_a_session_held_below_fifty_percent_of_maximum_is_a_measured_zero():
    load = training_load(measured(series((0, 90), (30, 95))), 200)

    assert load == TrainingLoad(Decimal("0"), LoadProvenance.MEASURED)


def test_repeated_computation_from_the_same_inputs_yields_the_same_value():
    activity = measured(series((0, 150), (10, 175), (20, 160)))
    first = training_load(activity, 190)

    assert all(training_load(activity, 190) == first for _ in range(100))


def test_a_run_and_a_ride_with_identical_series_produce_identical_loads():
    heart_rate = series((0, 150), (10, 175), (20, 160))

    assert training_load(measured(heart_rate, ActivityType.RUNNING), 190) == training_load(measured(heart_rate, ActivityType.CYCLING), 190)


@pytest.mark.parametrize("maximum", [0, -1])
def test_a_measured_load_is_refused_when_the_maximum_heart_rate_is_not_positive(maximum):
    with pytest.raises(ValueError) as refusal:
        training_load(measured(series((0, 150), (10, 175))), maximum)

    assert str(refusal.value) == (
        "A measured training load needs a positive maximum heart rate to classify zones against."
    )


def test_a_session_without_heart_rate_data_reports_an_estimated_load_from_moving_time():
    load = training_load(estimated(40), 190)

    assert load.points == Decimal("80")
    assert load.provenance is LoadProvenance.ESTIMATED


def test_forty_five_minutes_without_heart_rate_data_is_ninety_estimated_points():
    assert training_load(estimated(45), 190) == TrainingLoad(Decimal("90"), LoadProvenance.ESTIMATED)


def test_a_measured_and_an_estimated_load_of_the_same_value_stay_distinguishable():
    measured_load = training_load(measured(series((0, 150), (10, 175), (20, 160))), 190)
    estimated_load = training_load(estimated(40), 190)

    assert measured_load.points == estimated_load.points
    assert measured_load.provenance is not estimated_load.provenance
    assert measured_load != estimated_load


def test_an_estimate_is_produced_even_when_the_maximum_heart_rate_is_not_positive():
    assert training_load(estimated(40), 0) == TrainingLoad(Decimal("80"), LoadProvenance.ESTIMATED)


def test_a_training_load_offers_no_points_without_their_provenance():
    # 001 SC-007: the only fields are the pair, and the value is frozen.
    assert [f.name for f in dataclasses.fields(TrainingLoad)] == ["points", "provenance"]
    load = TrainingLoad(Decimal("1"), LoadProvenance.MEASURED)
    with pytest.raises(dataclasses.FrozenInstanceError):
        load.points = Decimal("2")  # type: ignore[misc]
    assert not hasattr(load, "__float__") and not hasattr(load, "__int__")
