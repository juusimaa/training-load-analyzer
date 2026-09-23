"""Run mode: the backend also serves the built SPA (research R15)."""

from fastapi.testclient import TestClient

from tests.api.conftest import refusing_transport
from tla.main import create_app


def dist(tmp_path):
    root = tmp_path / "dist"
    (root / "assets").mkdir(parents=True)
    (root / "index.html").write_text("<!DOCTYPE html><title>Training Load</title>", encoding="utf-8")
    (root / "assets" / "x.js").write_text("console.log(1)", encoding="utf-8")
    return root


def test_the_spa_is_served_for_every_non_api_path_and_its_assets_as_files(settings, clock, tmp_path):
    client = TestClient(create_app(settings, clock, refusing_transport(), frontend_dist=dist(tmp_path)))

    for path in ("/", "/nowhere", "/not-found", "/Error"):
        response = client.get(path)
        assert response.status_code == 200 and "<title>Training Load</title>" in response.text
    assert client.get("/assets/x.js").text == "console.log(1)"


def test_an_unknown_api_path_is_a_404_not_the_spa(settings, clock, tmp_path):
    response = TestClient(create_app(settings, clock, refusing_transport(), frontend_dist=dist(tmp_path))).get("/api/unknown")

    assert response.status_code == 404
    assert response.headers["content-type"].startswith("application/json")


def test_without_a_build_the_api_still_works(settings, clock, tmp_path):
    client = TestClient(create_app(settings, clock, refusing_transport(), frontend_dist=tmp_path / "missing"))

    assert client.get("/api/sync/status").status_code == 200
