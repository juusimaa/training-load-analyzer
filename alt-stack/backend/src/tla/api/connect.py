"""GET /connect and GET /strava/callback, a port of StravaConnectEndpoints (http-api.md §4)."""

import hmac
import logging
import secrets

from fastapi import APIRouter, Request
from fastapi.responses import JSONResponse, RedirectResponse, Response

from tla.sync.authorization import AthleteMismatch, InsufficientScope

router = APIRouter()
_log = logging.getLogger("tla.connect")

# SameSite=Lax is load-bearing: the callback is a cross-site top-level navigation from Strava.
_STATE_COOKIE = "tla.oauth.state"


@router.get("/connect")
def connect(request: Request) -> Response:
    state = secrets.token_hex(16)
    with request.app.state.authorization() as authorization:
        url = authorization.authorize_url(f"{request.url.scheme}://{request.headers['host']}/strava/callback", state)
    response = RedirectResponse(url, status_code=302)
    response.set_cookie(_STATE_COOKIE, state, max_age=600, httponly=True, samesite="lax", secure=request.url.scheme == "https")
    return response


@router.get("/strava/callback")
def callback(request: Request, code: str | None = None, state: str | None = None, error: str | None = None) -> Response:
    expected = request.cookies.get(_STATE_COOKIE)

    def answer(response: Response) -> Response:
        response.delete_cookie(_STATE_COOKIE)
        return response

    if error:
        # The athlete said no on Strava's consent screen: an answer, not a failure.
        return answer(RedirectResponse("/?connect=declined", status_code=302))

    # Checked before the exchange, so a forged code is never handed to Strava.
    if not state or not expected or not hmac.compare_digest(state.encode(), expected.encode()):
        _log.warning("A Strava callback arrived whose state did not match the one issued. No token exchange was attempted.")
        return answer(JSONResponse("This sign-in could not be verified. Start again from the dashboard.", status_code=400))

    if not code:
        return answer(JSONResponse("Strava returned no authorization code.", status_code=400))

    try:
        with request.app.state.authorization() as authorization:
            authorization.exchange(code)
        return answer(RedirectResponse("/", status_code=302))
    except InsufficientScope:
        _log.warning("A Strava connection was refused for a withheld scope.")
        return answer(RedirectResponse("/?connect=scope", status_code=302))
    except AthleteMismatch:
        _log.warning("A Strava connection was refused for an athlete mismatch.")
        return answer(RedirectResponse("/?connect=mismatch", status_code=302))
