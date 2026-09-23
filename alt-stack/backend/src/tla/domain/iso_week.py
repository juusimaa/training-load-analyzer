"""One ISO-8601 week, Monday to Sunday (002 FR-002)."""

from dataclasses import dataclass, field
from datetime import date, timedelta


@dataclass(frozen=True, slots=True, order=True)
class IsoWeek:
    """Ordered by its Monday alone, never by (year, week): the ISO year is not the calendar year
    at the boundary, and 2026 has 53 weeks (004 research R9)."""

    monday: date
    year: int = field(compare=False)
    week: int = field(compare=False)

    @classmethod
    def for_day(cls, day: date) -> "IsoWeek":
        year, week, weekday = day.isocalendar()
        return cls(monday=day - timedelta(days=weekday - 1), year=year, week=week)

    @property
    def sunday(self) -> date:
        return self.monday + timedelta(days=6)

    @property
    def designation(self) -> str:
        """As 2026-W38: the ISO year, never the calendar year of the day."""
        return f"{self.year}-W{self.week:02d}"
