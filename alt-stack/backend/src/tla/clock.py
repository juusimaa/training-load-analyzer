"""The server's clock, in the server's local zone, as the reference's TimeProvider.System (research R16)."""

from datetime import UTC, datetime


class SystemClock:
    def now_utc(self) -> datetime:
        return datetime.now(UTC)

    def now_local(self) -> datetime:
        return datetime.now().astimezone()

    def to_local(self, instant: datetime) -> datetime:
        return instant.astimezone()
