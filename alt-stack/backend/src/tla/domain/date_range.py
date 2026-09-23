from dataclasses import dataclass
from datetime import date


@dataclass(frozen=True, slots=True)
class DateRange:
    start: date
    end: date

    def days(self):
        raise NotImplementedError
