"""GET /api/dashboard: the whole view, always 200 (http-api.md §2)."""

from fastapi import APIRouter, Request

from tla.dashboard.reader import read_dashboard
from tla.dashboard.view_json import to_json

router = APIRouter()


@router.get("/api/dashboard")
def get_dashboard(request: Request) -> dict:
    settings = request.app.state.settings
    return to_json(read_dashboard(settings.database_path, request.app.state.clock, settings.maximum_heart_rate))
