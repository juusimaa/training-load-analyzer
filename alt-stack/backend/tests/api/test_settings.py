"""Ported from StartupTests.cs (006 FR-014, FR-015; http-api.md §5; 009 FR-018, FR-019)."""

import os
import socket
import subprocess
import sys
from pathlib import Path

import pytest

from tla.settings import ConfigurationError, Settings

BACKEND = Path(__file__).resolve().parents[2]
NOT_CONFIGURED = (
    "'TLA_ATHLETE_MAXIMUM_HEART_RATE' is not configured. Every measured training load is computed from the "
    "athlete's maximum heart rate, and there is no sensible default for it. Set it in the environment or in "
    "alt-stack/backend/.env before starting."
)


@pytest.mark.parametrize("env", [{}, {"TLA_ATHLETE_MAXIMUM_HEART_RATE": ""}, {"TLA_ATHLETE_MAXIMUM_HEART_RATE": "   "}])
def test_a_missing_or_blank_maximum_heart_rate_refuses_start_up(env):
    with pytest.raises(ConfigurationError) as refusal:
        Settings.from_env(env)

    assert str(refusal.value) == NOT_CONFIGURED


@pytest.mark.parametrize("value", ["abc", "0", "-5", "180.5"])
def test_a_maximum_heart_rate_that_is_not_a_positive_whole_number_refuses_start_up(value):
    with pytest.raises(ConfigurationError) as refusal:
        Settings.from_env({"TLA_ATHLETE_MAXIMUM_HEART_RATE": value})

    assert str(refusal.value) == (
        f"'TLA_ATHLETE_MAXIMUM_HEART_RATE' is '{value}', which is not a positive whole number of beats per "
        "minute. A training load computed from it would be meaningless."
    )


def test_a_valid_configuration_is_read():
    settings = Settings.from_env({"TLA_ATHLETE_MAXIMUM_HEART_RATE": "190", "TLA_STRAVA_CLIENT_ID": "12345",
                                  "TLA_STRAVA_CLIENT_SECRET": "test-client-secret", "TLA_DATABASE_PATH": "/tmp/x.db"})

    assert (settings.maximum_heart_rate, settings.strava_client_id, settings.strava_client_secret, settings.database_path) == (
        190, "12345", "test-client-secret", Path("/tmp/x.db"))


def test_the_database_defaults_to_the_backend_directory_and_missing_credentials_do_not_refuse():
    settings = Settings.from_env({"TLA_ATHLETE_MAXIMUM_HEART_RATE": "190"})

    assert settings.database_path == BACKEND / "training-load.db"
    assert (settings.strava_client_id, settings.strava_client_secret) == ("", "")


def test_the_repr_of_the_settings_carries_no_client_secret():
    assert "test-client-secret" not in repr(Settings.from_env({"TLA_ATHLETE_MAXIMUM_HEART_RATE": "190", "TLA_STRAVA_CLIENT_SECRET": "test-client-secret"}))


def free_port() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def test_the_server_refuses_to_start_before_binding_a_port():
    port = free_port()
    env = {k: v for k, v in os.environ.items() if not k.startswith("TLA_")}
    env["PYTHONPATH"] = str(BACKEND / "src")

    completed = subprocess.run(
        [sys.executable, "-m", "uvicorn", "tla.main:app", "--factory", "--workers", "1", "--port", str(port)],
        cwd=BACKEND, env=env, capture_output=True, text=True, timeout=60)

    assert completed.returncode != 0
    assert NOT_CONFIGURED in completed.stderr
    assert "Uvicorn running" not in completed.stderr + completed.stdout
