class NotConnected(Exception): pass


class ActivitySync:
    def __init__(self, conn, client, authorization, clock): pass

    def run(self): raise NotImplementedError
