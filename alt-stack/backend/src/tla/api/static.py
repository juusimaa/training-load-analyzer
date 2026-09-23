"""Run mode: the built SPA from frontend/dist, on the same origin as the API (research R15)."""

from pathlib import Path

from fastapi import FastAPI, HTTPException
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles


def mount_spa(app: FastAPI, dist: Path | None) -> None:
    """Every GET that no route claims gets index.html, so React renders the dashboard at / and Not
    Found everywhere else. Unknown /api paths stay a JSON 404. Without a build, nothing is mounted."""
    if dist is None or not (dist / "index.html").is_file():
        return

    root = dist.resolve()
    if (root / "assets").is_dir():
        app.mount("/assets", StaticFiles(directory=root / "assets"), name="assets")

    @app.get("/{path:path}", include_in_schema=False)
    def spa(path: str) -> FileResponse:
        if path == "api" or path.startswith("api/"):
            raise HTTPException(status_code=404)
        candidate = (root / path).resolve()
        if path and candidate.is_file() and root in candidate.parents:
            return FileResponse(candidate)
        return FileResponse(root / "index.html")
