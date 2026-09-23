import httpx
import pytest

from tests.fakes import FixedClock
from tla.settings import Settings


@pytest.fixture
def settings(tmp_path) -> Settings:
    return Settings(maximum_heart_rate=190, strava_client_id="12345", strava_client_secret="test-client-secret",
                    database_path=tmp_path / "api.db")


def refusing_transport() -> httpx.MockTransport:
    """Fails the test if the route under test talks to Strava at all."""
    def handle(request: httpx.Request) -> httpx.Response:
        pytest.fail(f"unexpected request to Strava: {request.method} {request.url}")
    return httpx.MockTransport(handle)


@pytest.fixture
def clock() -> FixedClock:
    return FixedClock()
