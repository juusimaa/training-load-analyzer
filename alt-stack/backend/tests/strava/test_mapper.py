"""Ported from ActivityMappingTests.cs (005 FR-009 – FR-020; research R17 and its correction)."""

from datetime import UTC, date, datetime, timedelta

import pytest

from tla.domain.activity import ActivityType, LoadProvenance, training_load
from tla.strava.mapper import MappedActivity, SkippedActivity, SkipReason, map_activity, to_series
from tla.strava.shapes import StravaActivitySummary, StravaStreamSet


def summary(id="11000000001", sport_type="Run", start="2026-09-10T04:30:00Z", offset=10800, moving=3120,
            has_heartrate=False, trainer=False, private=False, manual=False) -> StravaActivitySummary:
    return StravaActivitySummary.from_json({
        "id": id, "sport_type": sport_type, "start_date": start, "utc_offset": offset, "moving_time": moving,
        "elapsed_time": 4080, "has_heartrate": has_heartrate, "trainer": trainer, "private": private, "manual": manual})


@pytest.mark.parametrize(
    ("sport_type", "expected"),
    [("Run", ActivityType.RUNNING), ("TrailRun", ActivityType.RUNNING), ("VirtualRun", ActivityType.RUNNING),
     ("Ride", ActivityType.CYCLING), ("GravelRide", ActivityType.CYCLING), ("MountainBikeRide", ActivityType.CYCLING),
     ("VirtualRide", ActivityType.CYCLING)],
)
def test_every_sport_type_in_scope_maps_to_its_activity_type(sport_type, expected):
    mapped = map_activity(summary(sport_type=sport_type), None)

    assert isinstance(mapped, MappedActivity) and mapped.activity.type is expected


@pytest.mark.parametrize("sport_type", ["Swim", "EBikeRide", "EMountainBikeRide", "Hike", "WeightTraining", "Handcycle", "Velomobile", "SomeSportStravaAddsLater", "run"])
def test_every_sport_type_out_of_scope_is_skipped_with_its_reason(sport_type):
    assert map_activity(summary(id="id-x", sport_type=sport_type), None) == SkippedActivity("id-x", SkipReason.SPORT_OUT_OF_SCOPE)


def test_an_indoor_run_private_or_manual_activity_is_imported_like_any_other():
    for s in (summary(trainer=True), summary(private=True), summary(manual=True)):
        assert isinstance(map_activity(s, None), MappedActivity)


def test_the_start_carries_the_athletes_offset_from_utc():
    mapped = map_activity(summary(), None)

    assert mapped.activity.started_at == datetime(2026, 9, 10, 4, 30, tzinfo=UTC)
    assert mapped.activity.started_at.utcoffset() == timedelta(hours=3)
    assert mapped.activity.started_at.date() == date(2026, 9, 10)


def test_an_offset_of_five_and_a_half_hours_is_applied():
    mapped = map_activity(summary(start="2026-09-10T18:45:00Z", offset=19800), None)

    assert mapped.activity.started_at.utcoffset() == timedelta(hours=5, minutes=30)
    assert mapped.activity.started_at.date() == date(2026, 9, 11)


@pytest.mark.parametrize("offset", [10800.0, 19800.5])
def test_a_whole_or_fractional_float_offset_is_accepted_after_truncation(offset):
    mapped = map_activity(summary(offset=offset), None)

    assert isinstance(mapped, MappedActivity)
    assert mapped.activity.started_at.utcoffset() == timedelta(seconds=int(offset))


@pytest.mark.parametrize("offset", [19830, 50401, -50401])
def test_an_offset_that_is_not_whole_minutes_or_beyond_fourteen_hours_is_unusable(offset):
    assert map_activity(summary(id="x", offset=offset), None) == SkippedActivity("x", SkipReason.UNUSABLE_BY_DOMAIN)


@pytest.mark.parametrize("offset", [50400, -43200])
def test_the_extreme_valid_offsets_are_accepted(offset):
    assert isinstance(map_activity(summary(offset=offset), None), MappedActivity)


def test_moving_time_is_used_and_elapsed_time_is_not():
    assert map_activity(summary(), None).activity.moving_time == timedelta(minutes=52)


def test_the_external_identifier_is_carried_verbatim():
    assert map_activity(summary(), None).activity.external_id == "11000000001"


def test_an_activity_the_domain_refuses_is_skipped_with_its_reason():
    assert map_activity(summary(id="11000000007", moving=0), None) == SkippedActivity("11000000007", SkipReason.UNUSABLE_BY_DOMAIN)


def test_mapping_the_same_summary_twice_produces_the_same_session():
    assert map_activity(summary(), None) == map_activity(summary(), None)


def test_implausible_samples_are_discarded_and_the_rest_of_the_series_is_kept():
    series, discarded = to_series(StravaStreamSet(time=[0, 60, 120, 180], heartrate=[0, 0, 142, 150]))

    mapped = map_activity(summary(), series)

    assert discarded == 2
    assert [(s.time_from_start, s.bpm) for s in mapped.activity.heart_rate.samples] == [(timedelta(seconds=120), 142), (timedelta(seconds=180), 150)]
    assert training_load(mapped.activity, 190).provenance is LoadProvenance.MEASURED


def test_repeated_times_are_discarded_and_counted():
    series, discarded = to_series(StravaStreamSet(time=[0, 1, 1, 2], heartrate=[140, 141, 142, 143]))

    assert discarded == 1
    assert [s.bpm for s in series.samples] == [140, 141, 143]


def test_heart_rate_samples_beyond_the_time_stream_are_counted_as_discarded():
    series, discarded = to_series(StravaStreamSet(time=[0, 1], heartrate=[140, 141, 142]))

    assert (len(series.samples), discarded) == (2, 1)


def test_a_series_with_fewer_than_two_surviving_samples_is_unusable():
    assert to_series(StravaStreamSet(time=[0, 1], heartrate=[300, 150])) == (None, 1)


def test_a_missing_stream_yields_no_series_and_nothing_discarded():
    assert to_series(StravaStreamSet(time=[0, 1], heartrate=None)) == (None, 0)
    assert to_series(StravaStreamSet(time=None, heartrate=[140, 150])) == (None, 0)
