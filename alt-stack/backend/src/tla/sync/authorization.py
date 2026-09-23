"""Obtains and keeps permission to read one athlete's Strava data (005 FR-001 – FR-008).

Ported from StravaAuthorization.cs, plus the renewal the specification requires and the reference
never calls (research R3(1), R10; Amendment 1(b)1). Lives in `sync/` because it knows both Strava
and storage.
"""

from dataclasses import replace
from datetime import UTC, datetime, timedelta

from tla.persistence.activity_store import ConnectionStore
from tla.persistence.rows import ConnectionRow
from tla.strava.errors import InsufficientScope, ReconnectionRequired, StravaTokenRejected
from tla.strava.oauth import REQUIRED_SCOPES, StravaOAuthClient
from tla.strava.shapes import StravaTokens


class AthleteMismatch(Exception):
    """A different athlete authorized while one is held; two histories must never combine (005 FR-008)."""

    def __init__(self, held: int, authorized: int):
        super().__init__(
            f"This analyzer already holds training for Strava athlete {held}, and athlete {authorized} "
            "authorized instead. Disconnect the existing account before connecting a different one, so "
            "two athletes' training is never combined."
        )


class StravaAuthorization:
    def __init__(self, connections: ConnectionStore, oauth: StravaOAuthClient, clock):
        self._connections = connections
        self._oauth = oauth
        self._clock = clock

    def exchange(self, code: str) -> ConnectionRow:
        """A one-time code for a stored connection. Refused before anything is stored when the grant
        cannot see private activities (005 FR-002a)."""
        tokens = self._oauth.exchange(code)

        granted = tokens.scope or ""
        if "activity:read_all" not in granted.replace(" ", ",").split(","):
            raise InsufficientScope(REQUIRED_SCOPES, granted)

        athlete_id = tokens.athlete_id or 0
        held = self._connections.get()
        if held is not None and held.athlete_id != athlete_id:
            raise AthleteMismatch(held.athlete_id, athlete_id)

        connection = _apply(tokens, ConnectionRow(
            athlete_id=athlete_id,
            access_token="",
            refresh_token="",
            expires_at=self._clock.now_utc(),
            granted_scopes=granted,
            connected_at=self._clock.now_utc(),
        ))
        self._connections.save(connection)
        return connection

    def refresh(self) -> ConnectionRow:
        """Renews the access token without the athlete (005 FR-004). Strava rotates the refresh token
        too, so both are stored together (005 C48)."""
        held = self._connections.get()
        try:
            tokens = self._oauth.refresh(held.refresh_token)
        except StravaTokenRejected:
            raise ReconnectionRequired(held.athlete_id) from None
        renewed = _apply(tokens, held)
        self._connections.save(renewed)
        return renewed

    def ensure_fresh(self, margin: timedelta = timedelta(seconds=60)) -> ConnectionRow | None:
        """The held connection, renewed first when its token has expired or expires within `margin`.

        The margin covers clock skew and a long first page. Renewing on a 401 instead would spend a
        request of a rate-limited budget to learn what the stored expiry already says (research R10).
        """
        held = self._connections.get()
        if held is None or held.expires_at > self._clock.now_utc() + margin:
            return held
        return self.refresh()


def _apply(tokens: StravaTokens, connection: ConnectionRow) -> ConnectionRow:
    """Both tokens move together, in one place; the scopes only when the response carries them."""
    return replace(
        connection,
        access_token=tokens.access_token,
        refresh_token=tokens.refresh_token,
        expires_at=datetime.fromtimestamp(tokens.expires_at, UTC),
        granted_scopes=tokens.scope if tokens.scope is not None else connection.granted_scopes,
    )
