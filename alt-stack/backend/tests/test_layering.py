"""Principles II and V (009 FR-020): the domain stands alone, Strava stays inside its integration,
and nothing that serves the page talks HTTP to Strava."""

import ast
from pathlib import Path

import pytest

SRC = Path(__file__).resolve().parents[1] / "src" / "tla"


def imports(path: Path) -> set[str]:
    names = set()
    for node in ast.walk(ast.parse(path.read_text(encoding="utf-8"))):
        if isinstance(node, ast.Import):
            names.update(a.name for a in node.names)
        elif isinstance(node, ast.ImportFrom) and node.module:
            names.add(node.module)
    return names


def modules(package: str) -> list[Path]:
    return sorted((SRC / package).rglob("*.py")) if (SRC / package).is_dir() else []


ALL = sorted(SRC.rglob("*.py"))


@pytest.mark.parametrize("module", modules("domain"), ids=lambda p: p.name)
def test_the_domain_imports_only_itself(module):
    assert all(not name.startswith("tla.") or name.startswith("tla.domain") for name in imports(module))


@pytest.mark.parametrize("module", ALL, ids=lambda p: str(p.relative_to(SRC)))
def test_only_the_integration_and_the_sync_import_strava(module):
    package = module.relative_to(SRC).parts[0]
    if package in ("strava", "sync"):
        return
    # The app factory wires the whole graph together; it is the one composition root allowed to.
    if module.relative_to(SRC).as_posix() == "main.py":
        return
    assert not [name for name in imports(module) if name.startswith("tla.strava")], module


@pytest.mark.parametrize("module", modules("dashboard") + modules("api"), ids=lambda p: str(p.relative_to(SRC)))
def test_the_dashboard_and_the_api_never_import_httpx(module):
    assert not [name for name in imports(module) if name.split(".")[0] == "httpx"], module
