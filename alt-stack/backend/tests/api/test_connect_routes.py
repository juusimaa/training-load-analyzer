"""Ported from ConnectEndpointTests.cs, in the order of http-api.md §4 (005 FR-001 – FR-008; 006 FR-016 – FR-018)."""

import re
from urllib.parse import parse_qs, urlsplit

import httpx
from fastapi.testclient import TestClient

from tests.fakes import json_transport
from tla.main import create_app
from tla.persistence.activity_store import ConnectionStore
from tla.persistence.schema import open_database

TOKENS = {"token_type": "Bearer", "access_token": "test-access-1", "refresh_token": "test-refresh-1",
          "expires_at": 1789661253, "scope": "read,activity:read_all", "athlete": {"id": 900001}}


def client_with(settings, clock, routes=()):
    transport, requests = json_transport(list(routes))
    return TestClient(create_app(settings, clock, transport), follow_redirects=False), requests


def connect(client) -> str:
    response = client.get("/connect")
    return parse_qs(urlsplit(response.headers["location"]).query)["state"][0]


def test_connect_sets_the_state_cookie_and_redirects_to_strava(settings, clock):
    client, _ = client_with(settings, clock)

    response = client.get("/connect")

    assert response.status_code == 302
    location = response.headers["location"]
    assert location.startswith("https://www.strava.com/oauth/authorize?client_id=12345&redirect_uri=http%3A%2F%2Ftestserver%2Fstrava%2Fcallback&response_type=code&scope=read%2Cactivity%3Aread_all&state=")
    cookie = response.headers["set-cookie"]
    state = parse_qs(urlsplit(location).query)["state"][0]
    assert re.fullmatch("[0-9a-f]{32}", state)
    assert cookie.startswith(f"tla.oauth.state={state};")
    assert "httponly" in cookie.lower() and "samesite=lax" in cookie.lower() and "max-age=600" in cookie.lower()
    assert "secure" not in cookie.lower()


def test_each_connect_issues_a_different_state(settings, clock):
    client, _ = client_with(settings, clock)

    assert connect(client) != connect(client)


def test_a_declined_consent_deletes_the_cookie_and_goes_home_marked_declined(settings, clock):
    client, requests = client_with(settings, clock)
    state = connect(client)

    response = client.get("/strava/callback", params={"error": "access_denied", "state": state})

    assert (response.status_code, response.headers["location"]) == (302, "/?connect=declined")
    assert "tla.oauth.state=" in response.headers["set-cookie"] and ("max-age=0" in response.headers["set-cookie"].lower() or "expires=" in response.headers["set-cookie"].lower())
    assert requests == []


def test_a_missing_or_mismatched_state_is_refused_before_any_token_request(settings, clock):
    client, requests = client_with(settings, clock)
    connect(client)

    for params in ({"code": "c"}, {"code": "c", "state": "0" * 32}):
        response = client.get("/strava/callback", params=params)
        assert response.status_code == 400
        assert response.json() == "This sign-in could not be verified. Start again from the dashboard."
    assert requests == []


def test_a_callback_with_no_code_is_refused(settings, clock):
    client, requests = client_with(settings, clock)
    state = connect(client)

    response = client.get("/strava/callback", params={"state": state})

    assert (response.status_code, response.json()) == (400, "Strava returned no authorization code.")
    assert requests == []


def test_a_successful_exchange_stores_the_connection_and_goes_home(settings, clock):
    client, requests = client_with(settings, clock, [("POST", "oauth/token", 200, TOKENS, None)])
    state = connect(client)

    response = client.get("/strava/callback", params={"code": "an-authorization-code", "state": state})

    assert (response.status_code, response.headers["location"]) == (302, "/")
    assert ConnectionStore(open_database(settings.database_path)).get().athlete_id == 900001
    assert len(requests) == 1


def test_a_withheld_scope_goes_home_marked_scope(settings, clock):
    client, _ = client_with(settings, clock, [("POST", "oauth/token", 200, {**TOKENS, "scope": "read,activity:read"}, None)])
    state = connect(client)

    response = client.get("/strava/callback", params={"code": "c", "state": state})

    assert (response.status_code, response.headers["location"]) == (302, "/?connect=scope")
    assert ConnectionStore(open_database(settings.database_path)).get() is None


def test_a_different_athlete_goes_home_marked_mismatch(settings, clock):
    client, _ = client_with(settings, clock, [("POST", "oauth/token", 200, TOKENS, None),
                                              ("POST", "oauth/token", 200, {**TOKENS, "athlete": {"id": 900002}}, None)])
    client.get("/strava/callback", params={"code": "c", "state": connect(client)})

    response = client.get("/strava/callback", params={"code": "c2", "state": connect(client)})

    assert (response.status_code, response.headers["location"]) == (302, "/?connect=mismatch")
