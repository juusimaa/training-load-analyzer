"""Readers for the parity fixtures and goldens (parity.md), and the only tolerances allowed (§5)."""

import json
from decimal import Decimal
from pathlib import Path

PARITY = Path(__file__).resolve().parents[3] / "parity"
FIXTURES = PARITY / "fixtures"
GOLDEN = PARITY / "golden"


def _read(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def history_names() -> list[str]:
    return sorted(p.stem for p in (GOLDEN / "histories").glob("*.json"))


def sync_names() -> list[str]:
    return sorted(p.stem for p in (GOLDEN / "sync").glob("*.json"))


def load_history(name: str) -> dict:
    return _read(GOLDEN / "histories" / f"{name}.json")


def load_sync(name: str) -> dict:
    return _read(GOLDEN / "sync" / f"{name}.json")


def load_probes() -> dict:
    return _read(GOLDEN / "probes" / "display.json")


def load_surfaces() -> dict:
    return _read(GOLDEN / "probes" / "surfaces.json")


def history_fixture(name: str) -> dict:
    """Numbers with a fraction come back as Decimal, so a moving time like 30671.58 s stays exact."""
    return json.loads((FIXTURES / "histories" / f"{name}.json").read_text(encoding="utf-8"), parse_float=Decimal)


def sync_scenario(name: str) -> dict:
    """The scenario, with its directory attached so response bodies can be read."""
    directory = FIXTURES / "sync" / name
    scenario = _read(directory / "scenario.json")
    scenario["directory"] = directory
    return scenario


def assert_decimal_close(actual: Decimal, expected: Decimal, tol: Decimal = Decimal("1e-20")) -> None:
    """Loads, totals and trend changes: |a − b| ≤ 1e-20 points (research R1, Amendment 1(a))."""
    assert abs(Decimal(actual) - Decimal(expected)) <= tol, f"{actual} != {expected} (±{tol})"


def assert_float_close(actual: float, expected: float, tol: float = 1e-4) -> None:
    """Fitness, Fatigue and Form: |a − b| ≤ 1e-4 (003 FR-029)."""
    assert abs(float(actual) - float(expected)) <= tol, f"{actual!r} != {expected!r} (±{tol})"
