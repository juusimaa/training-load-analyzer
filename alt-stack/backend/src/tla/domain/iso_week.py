from dataclasses import dataclass
from datetime import date


@dataclass(frozen=True, slots=True)
class IsoWeek:
    year: int
    week: int
    monday: date

    @classmethod
    def for_day(cls, day: date) -> "IsoWeek":
        raise NotImplementedError
