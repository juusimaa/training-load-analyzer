"""Test inputs, not mocks: a clock the test sets, and a transport stub that replays a scenario.

`httpx.MockTransport` is httpx's own transport-level stub, the equivalent of the reference's
StubHttpMessageHandler (plan, Principle IV). Nothing here records calls on production objects.
"""

import json
from datetime import UTC, datetime, timedelta, timezone

import httpx
import pytest

FIXTURE_OFFSET = timezone(timedelta(hours=3))

# The reference's FixedLocalClock default: 2026-09-18T04:00Z, which is 07:00 at +03:00 (research R16).
DEFAULT_UTC = datetime(2026, 9, 18, 4, 0, 0, tzinfo=UTC)


class FixedClock:
    """A clock pinned in both respects: the instant, and the athlete's zone."""

    def __init__(self, now_utc: datetime = DEFAULT_UTC, zone: timezone = FIXTURE_OFFSET):
        self._now = now_utc
        self._zone = zone

    def now_utc(self) -> datetime:
        return self._now.astimezone(UTC)

    def now_local(self) -> datetime:
        return self._now.astimezone(self._zone)

    @property
    def zone(self) -> timezone:
        return self._zone

    def set(self, now_utc: datetime) -> None:
        self._now = now_utc


def scenario_clock(scenario: dict) -> FixedClock:
    utc = datetime.fromisoformat(scenario["clock"]["utc"])
    offset = datetime.fromisoformat("2000-01-01T00:00:00" + scenario["clock"]["localOffset"]).utcoffset()
    return FixedClock(utc, timezone(offset))


def scenario_transport(scenario: dict, exchanges: list | None = None) -> tuple[httpx.MockTransport, list]:
    """Replays the scenario's exchanges strictly in order.

    Returns the transport and the list of `(method, url)` it was asked for. A request that does
    not match the next recorded exchange fails the test: an unanticipated request is a failure of
    the code under test, not a case to be defaulted.
    """
    recorded = list(scenario["exchanges"] if exchanges is None else exchanges)
    requests: list[tuple[str, str]] = []

    def handle(request: httpx.Request) -> httpx.Response:
        method, url = request.method, str(request.url)
        requests.append((method, url))
        if len(requests) > len(recorded):
            pytest.fail(f"unexpected request {method} {url} after the last recorded exchange")
        exchange = recorded[len(requests) - 1]
        if (exchange["method"], exchange["url"]) != (method, url):
            pytest.fail(f"request {method} {url} does not match recorded {exchange['method']} {exchange['url']}")
        response = exchange["response"]
        if response.get("drop"):
            raise httpx.ConnectError("Connection dropped (recorded).", request=request)
        body = (scenario["directory"] / response["body"]).read_text(encoding="utf-8")
        return httpx.Response(response["status"], headers=response.get("headers", {}), content=body.encode())

    return httpx.MockTransport(handle), requests


def json_transport(routes: list[tuple[str, str, int, object, dict | None]]) -> tuple[httpx.MockTransport, list]:
    """A small ordered stub for unit tests: each entry is (method, url fragment, status, body, headers)."""
    queue = list(routes)
    requests: list[httpx.Request] = []

    def handle(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        for i, (method, fragment, status, body, headers) in enumerate(queue):
            if method == request.method and fragment in str(request.url):
                queue.pop(i)
                if isinstance(body, Exception):
                    raise body
                content = body if isinstance(body, bytes) else json.dumps(body).encode()
                return httpx.Response(status, headers=headers or {}, content=content)
        pytest.fail(f"no stubbed response for {request.method} {request.url}")

    return httpx.MockTransport(handle), requests
