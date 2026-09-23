REQUIRED_SCOPES = ""


class StravaOAuthClient:
    def __init__(self, http, client_id, client_secret):
        pass

    def authorize_url(self, redirect_uri, state): raise NotImplementedError

    def exchange(self, code): raise NotImplementedError

    def refresh(self, refresh_token): raise NotImplementedError
