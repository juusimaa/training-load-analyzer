"""Reads activities and streams from Strava (005 FR-027, FR-034 – FR-037), ported from StravaApiClient.cs."""

import time
from collections.abc import Callable
from datetime import UTC, datetime, timedelta

import httpx

from tla.strava.errors import StravaRateLimited, StravaRequestFailed
from tla.strava.rate_limit import RateLimitStatus
from tla.strava.shapes import StravaActivitySummary, StravaStreamSet

# Three attempts with backoff, hand-written rather than a resilience package (005 research R17).
_ATTEMPTS = 3

# Undocumented but reliable; it makes 1,200 activities six requests, not forty (005 research R15).
_PAGE_SIZE = 200

_ACTIVITIES_URL = "https://www.strava.com/api/v3/athlete/activities"
_EPOCH = datetime(1970, 1, 1, tzinfo=UTC)


class StravaApiClient:
    def __init__(self, http: httpx.Client, sleep: Callable[[float], None] = time.sleep):
        self._http = http
        self._sleep = sleep

    def list_activities(self, access_token: str, after: datetime, page: int) -> list[StravaActivitySummary]:
        """One page of activities at or after `after`. The order Strava returns is not depended on."""
        seconds = (after - _EPOCH) // timedelta(seconds=1)
        url = f"{_ACTIVITIES_URL}?after={seconds}&page={page}&per_page={_PAGE_SIZE}"
        response = self._send(url, access_token)
        return [StravaActivitySummary.from_json(item) for item in (response.json() or [])]

    def get_streams(self, access_token: str, activity_id: str) -> StravaStreamSet | None:
        """Time and heart-rate streams at full resolution, or None when the activity has none (404)."""
        url = f"https://www.strava.com/api/v3/activities/{activity_id}/streams?keys=time,heartrate&key_by_type=true"
        response = self._send(url, access_token, not_found_is_empty=True)
        return None if response.status_code == 404 else StravaStreamSet.from_json(response.json())

    def _send(self, url: str, access_token: str, not_found_is_empty: bool = False) -> httpx.Response:
        """Retries only what is plausibly transient: a dropped connection and a 5xx."""
        response: httpx.Response | None = None

        for attempt in range(1, _ATTEMPTS + 1):
            response = None
            try:
                response = self._http.get(url, headers={"Authorization": f"Bearer {access_token}"})
            except httpx.TransportError:
                pass

            if response is not None:
                if response.status_code == 429:
                    raise StravaRateLimited(RateLimitStatus.from_headers(response.headers))

                if response.is_success or (not_found_is_empty and response.status_code == 404):
                    budget = RateLimitStatus.from_headers(response.headers)
                    # Stop before the limit is exceeded, not after (005 FR-034).
                    if budget is not None and budget.is_exhausted:
                        raise StravaRateLimited(budget)
                    return response

                if response.status_code < 500:
                    raise StravaRequestFailed(response.status_code)

            if attempt < _ATTEMPTS:
                self._sleep(0.01 * 2 ** (attempt - 1))

        raise StravaRequestFailed(None if response is None else response.status_code)
