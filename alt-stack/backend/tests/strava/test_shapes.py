"""Ported from StravaJsonQuirksTests.cs and the shape rules of 005 (FR-010a, FR-020, research R15, R20)."""

import json
from datetime import UTC, datetime

from tla.strava.shapes import StravaActivitySummary, StravaStreamSet, StravaTokens


def summary(**overrides) -> StravaActivitySummary:
    body = {"id": 11000000001, "sport_type": "Run", "start_date": "2026-09-10T04:30:00Z", "utc_offset": 10800,
            "moving_time": 3120, "elapsed_time": 4080, "has_heartrate": False, "manual": False, "private": False}
    body.update(overrides)
    return StravaActivitySummary.from_json(json.loads(json.dumps(body)))


def test_an_id_larger_than_two_to_the_53_is_kept_verbatim_as_text():
    parsed = StravaActivitySummary.from_json(json.loads('{"id": 18014398509481985, "sport_type": "Run"}'))

    assert parsed.external_id == "18014398509481985"


def test_a_string_id_is_kept_as_it_is():
    assert summary(id="fixture-1001").external_id == "fixture-1001"


def test_the_fields_the_sync_reads_are_parsed():
    parsed = summary(has_heartrate=True, trainer=True)

    assert parsed.sport_type == "Run"
    assert parsed.start_date == datetime(2026, 9, 10, 4, 30, tzinfo=UTC)
    assert (parsed.utc_offset, parsed.moving_time) == (10800, 3120)
    assert (parsed.has_heartrate, parsed.manual, parsed.private, parsed.trainer) == (True, False, False, True)


def test_a_utc_offset_with_a_trailing_point_zero_is_accepted_as_whole_seconds():
    assert summary(utc_offset=10800.0).utc_offset == 10800


def test_a_fractional_utc_offset_is_truncated_toward_zero_as_the_reference_does():
    assert summary(utc_offset=19800.5).utc_offset == 19800
    assert summary(utc_offset=-19800.5).utc_offset == -19800


def test_missing_optional_fields_take_the_reference_defaults():
    parsed = StravaActivitySummary.from_json({"id": 1, "sport_type": "Ride", "start_date": "2026-09-10T04:30:00Z"})

    assert (parsed.utc_offset, parsed.moving_time) == (0, 0)
    assert (parsed.has_heartrate, parsed.manual, parsed.private, parsed.trainer) == (False, False, False, False)


def test_unknown_keys_are_ignored_and_start_date_local_and_type_are_never_read():
    base = summary()
    other = summary(start_date_local="2026-09-10T07:30:00Z", type="Ride", some_new_field={"x": 1})

    assert other == base


def test_a_body_lacking_start_date_local_and_type_parses():
    assert summary().sport_type == "Run"


def test_a_stream_body_without_heartrate_has_no_heartrate_stream():
    streams = StravaStreamSet.from_json({"time": {"data": [0, 1, 2], "series_type": "distance"}})

    assert streams.time == [0, 1, 2]
    assert streams.heartrate is None


def test_a_stream_body_with_both_streams_parses_both():
    streams = StravaStreamSet.from_json({"time": {"data": [0, 60]}, "heartrate": {"data": [140, 150]}})

    assert (streams.time, streams.heartrate) == ([0, 60], [140, 150])


def test_a_token_response_parses_and_its_repr_hides_the_tokens():
    tokens = StravaTokens.from_json({
        "token_type": "Bearer", "access_token": "test-access-x", "refresh_token": "test-refresh-x",
        "expires_at": 1789661253, "expires_in": 21600, "scope": "read,activity:read_all", "athlete": {"id": 900001},
    })

    assert (tokens.access_token, tokens.refresh_token, tokens.expires_at) == ("test-access-x", "test-refresh-x", 1789661253)
    assert (tokens.scope, tokens.athlete_id) == ("read,activity:read_all", 900001)
    assert "test-access-x" not in repr(tokens) and "test-refresh-x" not in repr(tokens)


def test_a_renewal_response_has_no_scope_and_no_athlete():
    tokens = StravaTokens.from_json({"access_token": "a", "refresh_token": "r", "expires_at": 1})

    assert (tokens.scope, tokens.athlete_id) == (None, None)
