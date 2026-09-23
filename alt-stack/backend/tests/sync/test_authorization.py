"""Ported from ConnectionTests.cs (005 FR-001 – FR-008), plus the specified renewal (research R10, Amendment 1(b)1)."""

from datetime import UTC, datetime, timedelta

import httpx
import pytest

from tests.fakes import FixedClock, json_transport
from tla.persistence.activity_store import ConnectionStore
from tla.persistence.rows import ConnectionRow
from tla.persistence.schema import open_database
from tla.strava.errors import InsufficientScope, ReconnectionRequired
from tla.strava.oauth import StravaOAuthClient
from tla.sync.authorization import AthleteMismatch, StravaAuthorization

NOW = datetime(2026, 9, 17, 10, 7, 33, tzinfo=UTC)
SIX_HOURS = int((NOW + timedelta(hours=6)).timestamp())


def tokens(access="test-access-1", refresh="test-refresh-1", athlete=900001, scope="read,activity:read_all", expires=SIX_HOURS):
    body = {"token_type": "Bearer", "access_token": access, "refresh_token": refresh, "expires_at": expires, "expires_in": 21600}
    if athlete is not None:
        body["athlete"] = {"id": athlete}
    if scope is not None:
        body["scope"] = scope
    return body


def authorization(tmp_path, routes, clock=None):
    transport, requests = json_transport(routes)
    conn = open_database(tmp_path / "t.db")
    store = ConnectionStore(conn)
    oauth = StravaOAuthClient(httpx.Client(transport=transport), "12345", "test-client-secret")
    return StravaAuthorization(store, oauth, clock or FixedClock(NOW)), store, requests


def test_exchanging_the_code_stores_a_connection_for_the_authorizing_athlete(tmp_path):
    auth, store, _ = authorization(tmp_path, [("POST", "oauth/token", 200, tokens(), None)])

    connection = auth.exchange("an-authorization-code")

    assert store.get() == connection
    assert (connection.athlete_id, connection.access_token, connection.refresh_token) == (900001, "test-access-1", "test-refresh-1")
    assert (connection.expires_at, connection.granted_scopes, connection.connected_at) == (NOW + timedelta(hours=6), "read,activity:read_all", NOW)


def test_a_grant_without_private_activity_access_is_refused_and_nothing_is_stored(tmp_path):
    auth, store, _ = authorization(tmp_path, [("POST", "oauth/token", 200, tokens(scope="read,activity:read"), None)])

    with pytest.raises(InsufficientScope) as refusal:
        auth.exchange("a-code")

    assert "activity:read_all" in str(refusal.value)
    assert (refusal.value.requested, refusal.value.granted) == ("read,activity:read_all", "read,activity:read")
    assert store.get() is None


def test_scopes_separated_by_spaces_are_accepted(tmp_path):
    auth, _, _ = authorization(tmp_path, [("POST", "oauth/token", 200, tokens(scope="read activity:read_all"), None)])

    assert auth.exchange("a-code").granted_scopes == "read activity:read_all"


def test_authorizing_a_different_athlete_is_refused_and_the_held_connection_kept(tmp_path):
    auth, store, _ = authorization(tmp_path, [
        ("POST", "oauth/token", 200, tokens(athlete=900001), None),
        ("POST", "oauth/token", 200, tokens(access="test-access-2", athlete=900002), None),
    ])
    auth.exchange("code-1")

    with pytest.raises(AthleteMismatch) as refusal:
        auth.exchange("code-2")

    assert str(refusal.value) == (
        "This analyzer already holds training for Strava athlete 900001, and athlete 900002 authorized instead. "
        "Disconnect the existing account before connecting a different one, so two athletes' training is never combined."
    )
    assert (store.get().athlete_id, store.get().access_token) == (900001, "test-access-1")


def test_the_same_athlete_again_replaces_the_tokens(tmp_path):
    auth, store, _ = authorization(tmp_path, [
        ("POST", "oauth/token", 200, tokens(access="test-access-1"), None),
        ("POST", "oauth/token", 200, tokens(access="test-access-2", refresh="test-refresh-2"), None),
    ])

    auth.exchange("code-1")
    auth.exchange("code-2")

    assert (store.get().access_token, store.get().refresh_token) == ("test-access-2", "test-refresh-2")


def test_a_renewal_stores_both_rotated_tokens_and_keeps_the_scopes(tmp_path):
    auth, store, _ = authorization(tmp_path, [
        ("POST", "oauth/token", 200, tokens(), None),
        ("POST", "oauth/token", 200, tokens(access="test-access-2", refresh="test-refresh-2", athlete=None, scope=None, expires=SIX_HOURS + 3600), None),
    ])
    auth.exchange("a-code")

    renewed = auth.refresh()

    assert store.get() == renewed
    assert (renewed.access_token, renewed.refresh_token, renewed.granted_scopes) == ("test-access-2", "test-refresh-2", "read,activity:read_all")


@pytest.mark.parametrize("expires_in", [timedelta(seconds=60), timedelta(seconds=0), timedelta(hours=-1)])
def test_ensure_fresh_renews_a_token_expiring_within_sixty_seconds(tmp_path, expires_in):
    auth, store, requests = authorization(tmp_path, [
        ("POST", "oauth/token", 200, tokens(access="test-access-2", refresh="test-refresh-2", athlete=None, scope=None), None),
    ])
    store.save(ConnectionRow(900001, "test-access-1", "test-refresh-1", NOW + expires_in, "read,activity:read_all", NOW))

    fresh = auth.ensure_fresh()

    assert len(requests) == 1
    assert (fresh.access_token, fresh.refresh_token) == ("test-access-2", "test-refresh-2")
    assert store.get() == fresh


def test_ensure_fresh_leaves_a_token_with_more_than_sixty_seconds_alone(tmp_path):
    auth, store, requests = authorization(tmp_path, [])
    held = ConnectionRow(900001, "test-access-1", "test-refresh-1", NOW + timedelta(seconds=61), "read,activity:read_all", NOW)
    store.save(held)

    assert auth.ensure_fresh() == held
    assert requests == []


@pytest.mark.parametrize("status", [400, 401])
def test_a_rejected_renewal_asks_for_reconnection_and_stores_nothing(tmp_path, status):
    auth, store, _ = authorization(tmp_path, [("POST", "oauth/token", status, {"message": "Bad Request"}, None)])
    held = ConnectionRow(900001, "test-access-1", "test-refresh-1", NOW - timedelta(hours=1), "read,activity:read_all", NOW)
    store.save(held)

    with pytest.raises(ReconnectionRequired) as refusal:
        auth.ensure_fresh()

    assert refusal.value.athlete_id == 900001
    assert store.get() == held
