"""The stretch of calendar days an aggregation is asked about, both ends included (002 FR-009)."""

from collections.abc import Iterator
from dataclasses import dataclass
from datetime import date, timedelta


@dataclass(frozen=True, slots=True)
class DateRange:
    start: date
    end: date

    def __post_init__(self) -> None:
        # The missing-bound checks run first: the order is load-bearing (002 FR-021).
        if self.start is None:
            raise ValueError("The range starts unbounded. A range must be bounded at both ends.")
        if self.end is None:
            raise ValueError("The range ends unbounded. A range must be bounded at both ends.")
        if self.end < self.start:
            raise ValueError(
                f"The range ends on {self.end.isoformat()}, before it starts on {self.start.isoformat()}."
            )

    def days(self) -> Iterator[date]:
        """Every day from start to end inclusive, ascending (002 FR-005)."""
        day = self.start
        while day <= self.end:
            yield day
            day += timedelta(days=1)
