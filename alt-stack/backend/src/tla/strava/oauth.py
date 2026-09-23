"""Strava's OAuth endpoints and nothing else (005 FR-001 – FR-004), ported from StravaOAuthClient.cs."""

from urllib.parse import quote

import httpx

from tla.strava.errors import StravaTokenRejected
from tla.strava.shapes import StravaTokens

_AUTHORIZE_URL = "https://www.strava.com/oauth/authorize"
_TOKEN_URL = "https://www.strava.com/oauth/token"

# activity:read_all makes private activities visible; nothing is ever written (005 FR-002, C70).
REQUIRED_SCOPES = "read,activity:read_all"


def _escape(value: str) -> str:
    """As .NET's Uri.EscapeDataString: everything but the unreserved characters."""
    return quote(value, safe="")


class StravaOAuthClient:
    def __init__(self, http: httpx.Client, client_id: str, client_secret: str):
        self._http = http
        self._client_id = client_id
        self._client_secret = client_secret

    def authorize_url(self, redirect_uri: str, state: str) -> str:
        query = "&".join([
            f"client_id={_escape(self._client_id)}",
            f"redirect_uri={_escape(redirect_uri)}",
            "response_type=code",
            f"scope={_escape(REQUIRED_SCOPES)}",
            f"state={_escape(state)}",
        ])
        return f"{_AUTHORIZE_URL}?{query}"

    def exchange(self, code: str) -> StravaTokens:
        return self._post({
            "client_id": self._client_id,
            "client_secret": self._client_secret,
            "code": code,
            "grant_type": "authorization_code",
        })

    def refresh(self, refresh_token: str) -> StravaTokens:
        return self._post({
            "client_id": self._client_id,
            "client_secret": self._client_secret,
            "grant_type": "refresh_token",
            "refresh_token": refresh_token,
        })

    def _post(self, form: dict[str, str]) -> StravaTokens:
        response = self._http.post(_TOKEN_URL, data=form)
        if not response.is_success:
            raise StravaTokenRejected(response.status_code)
        body = response.json()
        if not body:
            raise ValueError("Strava returned an empty token response.")
        return StravaTokens.from_json(body)
