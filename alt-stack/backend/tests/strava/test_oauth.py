"""Ported from the OAuth cases of ConnectionTests.cs (005 FR-001, FR-002, FR-004, FR-005)."""

from urllib.parse import parse_qs, urlsplit

import httpx
import pytest

from tests.fakes import json_transport
from tla.strava.errors import StravaTokenRejected
from tla.strava.oauth import REQUIRED_SCOPES, StravaOAuthClient

TOKENS = {"token_type": "Bearer", "access_token": "test-access-1", "refresh_token": "test-refresh-1",
          "expires_at": 1789661253, "expires_in": 21600, "scope": "read,activity:read_all", "athlete": {"id": 900001}}


def oauth(routes=()):
    transport, requests = json_transport(list(routes))
    return StravaOAuthClient(httpx.Client(transport=transport), "12345", "test-client-secret"), requests


def test_the_authorize_url_carries_the_parameters_strava_requires_in_the_reference_order():
    client, _ = oauth()

    url = client.authorize_url("http://localhost:8000/strava/callback", "a-state-value")

    assert url == (
        "https://www.strava.com/oauth/authorize?client_id=12345"
        "&redirect_uri=http%3A%2F%2Flocalhost%3A8000%2Fstrava%2Fcallback"
        "&response_type=code&scope=read%2Cactivity%3Aread_all&state=a-state-value"
    )
    assert REQUIRED_SCOPES == "read,activity:read_all"


def test_the_authorize_url_neither_takes_a_password_nor_asks_to_write():
    client, _ = oauth()

    url = client.authorize_url("http://localhost/callback", "s").lower()

    assert "password" not in url and ":write" not in url


def test_a_code_is_exchanged_by_posting_the_form_fields():
    client, requests = oauth([("POST", "oauth/token", 200, TOKENS, None)])

    tokens = client.exchange("an-authorization-code")

    assert str(requests[0].url) == "https://www.strava.com/oauth/token"
    assert parse_qs(requests[0].content.decode()) == {
        "client_id": ["12345"], "client_secret": ["test-client-secret"], "code": ["an-authorization-code"], "grant_type": ["authorization_code"]}
    assert tokens.athlete_id == 900001


def test_a_renewal_posts_the_refresh_token():
    client, requests = oauth([("POST", "oauth/token", 200, TOKENS, None)])

    client.refresh("test-refresh-1")

    assert parse_qs(requests[0].content.decode()) == {
        "client_id": ["12345"], "client_secret": ["test-client-secret"], "grant_type": ["refresh_token"], "refresh_token": ["test-refresh-1"]}


@pytest.mark.parametrize("status", [400, 401])
def test_a_rejected_renewal_is_a_token_rejection(status):
    client, _ = oauth([("POST", "oauth/token", status, {"message": "Bad Request"}, None)])

    with pytest.raises(StravaTokenRejected) as rejected:
        client.refresh("test-refresh-secret")

    assert "test-refresh-secret" not in str(rejected.value)
    assert str(rejected.value) == f"Strava refused the token request with {status} {'BadRequest' if status == 400 else 'Unauthorized'}."
