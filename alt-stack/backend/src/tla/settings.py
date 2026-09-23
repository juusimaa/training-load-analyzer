from dataclasses import dataclass
from pathlib import Path


class ConfigurationError(Exception): pass


@dataclass(frozen=True, slots=True)
class Settings:
    maximum_heart_rate: int
    strava_client_id: str
    strava_client_secret: str
    database_path: Path

    @classmethod
    def from_env(cls, env): raise NotImplementedError
