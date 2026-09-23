"""The domain stands alone (Principle II; 009 FR-008, FR-020): no I/O, no framework, no Strava."""

import ast
import re
from pathlib import Path

import pytest

DOMAIN = Path(__file__).resolve().parents[2] / "src" / "tla" / "domain"
ALLOWED_STDLIB = {"decimal", "datetime", "math", "enum", "dataclasses", "typing", "collections"}
MODULES = sorted(DOMAIN.glob("*.py"))


def imported_modules(path: Path) -> set[str]:
    tree = ast.parse(path.read_text(encoding="utf-8"))
    names = set()
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            names.update(alias.name for alias in node.names)
        elif isinstance(node, ast.ImportFrom):
            names.add(node.module or "")
    return names


@pytest.mark.parametrize("module", MODULES, ids=lambda p: p.name)
def test_a_domain_module_imports_only_the_domain_and_a_short_list_of_stdlib(module):
    for name in imported_modules(module):
        if name.startswith("tla."):
            assert name.startswith("tla.domain"), f"{module.name} imports {name}"
        else:
            assert name.split(".")[0] in ALLOWED_STDLIB, f"{module.name} imports {name}"


@pytest.mark.parametrize("module", MODULES, ids=lambda p: p.name)
def test_no_domain_module_names_strava(module):
    assert not re.search("strava", module.read_text(encoding="utf-8"), re.IGNORECASE), module.name
