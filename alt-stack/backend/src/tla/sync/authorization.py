class AthleteMismatch(Exception): pass


class StravaAuthorization:
    def __init__(self, connections, oauth, clock): pass

    def exchange(self, code): raise NotImplementedError

    def refresh(self): raise NotImplementedError

    def ensure_fresh(self): raise NotImplementedError
