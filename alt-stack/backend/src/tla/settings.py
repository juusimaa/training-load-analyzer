"""Configuration from the environment, with the start-up refusal (006 FR-014, FR-015; research R14).

No settings library (009 FR-019): `uv run --env-file .env` loads the file into the environment.
"""

from collections.abc import Mapping
from dataclasses import dataclass, field
from pathlib import Path

_MAXIMUM_HEART_RATE = "TLA_ATHLETE_MAXIMUM_HEART_RATE"

# alt-stack/backend, whatever directory the server is started from.
_BACKEND = Path(__file__).resolve().parents[2]


class ConfigurationError(Exception):
    """The application cannot start: every measured load would be computed from a wrong number."""


@dataclass(frozen=True, slots=True)
class Settings:
    maximum_heart_rate: int
    strava_client_id: str
    strava_client_secret: str = field(repr=False)
    database_path: Path

    @classmethod
    def from_env(cls, env: Mapping[str, str]) -> "Settings":
        configured = env.get(_MAXIMUM_HEART_RATE)
        if configured is None or not configured.strip():
            raise ConfigurationError(
                f"'{_MAXIMUM_HEART_RATE}' is not configured. Every measured training load is computed from the "
                "athlete's maximum heart rate, and there is no sensible default for it. Set it in the "
                "environment or in alt-stack/backend/.env before starting."
            )
        try:
            maximum = int(configured)
        except ValueError:
            maximum = 0
        if maximum <= 0:
            raise ConfigurationError(
                f"'{_MAXIMUM_HEART_RATE}' is '{configured}', which is not a positive whole number of beats per "
                "minute. A training load computed from it would be meaningless."
            )

        # A missing client id or secret does not refuse start-up: the dashboard works without Strava.
        return cls(
            maximum_heart_rate=maximum,
            strava_client_id=env.get("TLA_STRAVA_CLIENT_ID", ""),
            strava_client_secret=env.get("TLA_STRAVA_CLIENT_SECRET", ""),
            database_path=Path(env.get("TLA_DATABASE_PATH") or _BACKEND / "training-load.db"),
        )
