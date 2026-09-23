import time


class StravaApiClient:
    def __init__(self, http, sleep=time.sleep):
        self._http = http

    def list_activities(self, access_token, after, page): raise NotImplementedError

    def get_streams(self, access_token, activity_id): raise NotImplementedError
