"""The integration's failures. Messages are the reference's and carry no credential (005 FR-005, FR-006)."""

from http import HTTPStatus

from tla.strava.rate_limit import RateLimitStatus


def _status_text(status: int) -> str:
    """`{code} {name}` as .NET prints an HttpStatusCode, e.g. "503 ServiceUnavailable"."""
    try:
        return f"{status} {HTTPStatus(status).phrase.replace(' ', '')}"
    except ValueError:
        return f"{status} {status}"


class StravaRateLimited(Exception):
    """A read limit was reached. A stop signal, never retried (005 C68)."""

    def __init__(self, status: RateLimitStatus | None):
        super().__init__("Strava's read limit has been reached; the sync stopped rather than exceeding it.")
        self.status = status


class StravaRequestFailed(Exception):
    """Strava answered something a retry will not improve, or a transient failure outlasted the retries."""

    def __init__(self, status: int | None):
        super().__init__(
            "The connection to Strava failed and did not recover."
            if status is None
            else f"Strava answered {_status_text(status)}."
        )
        self.status = status


class StravaTokenRejected(Exception):
    """Strava refused an exchange or a renewal. Raised only by the OAuth client."""

    def __init__(self, status: int):
        super().__init__(f"Strava refused the token request with {_status_text(status)}.")
        self.status = status


class InsufficientScope(Exception):
    """The athlete approved the consent page but unticked a scope (005 FR-002a)."""

    def __init__(self, requested: str, granted: str):
        super().__init__(
            f"Strava granted '{granted}' but the analyzer needs '{requested}'. Without activity:read_all "
            "your private activities would be silently missing from every figure. Reconnect and "
            "approve access to all your activities."
        )
        self.requested = requested
        self.granted = granted


class ReconnectionRequired(Exception):
    """Strava rejected the stored renewal credential (005 FR-006)."""

    def __init__(self, athlete_id: int):
        super().__init__(
            f"Strava would not renew access for athlete {athlete_id}. Reconnect the account to continue "
            "synchronising; activities already imported are untouched."
        )
        self.athlete_id = athlete_id
