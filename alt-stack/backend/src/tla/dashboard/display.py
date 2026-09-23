"""How a figure reaches the page: .NET's Display, reproduced without the ambient locale (research R2).

.NET formats a double to 15 significant digits and then rounds half away from zero; Python's own
float formatting rounds the exact binary value half to even, and disagrees on 5 of 13 probes. So a
metric goes through its 15-digit text and a Decimal quantize. No `locale`, no f-string float format.
"""

from datetime import date, timedelta
from decimal import ROUND_HALF_UP, Context, Decimal

MISSING = "—"

_ONE_DECIMAL = Decimal("0.1")
_WHOLE = Decimal("1")
_HUNDRED = Decimal(100)

# Wide enough that quantizing any figure this page can show never runs out of digits.
_CONTEXT = Context(prec=60)


def metric(value: float | None) -> str:
    """Fitness, Fatigue or Form, to one decimal place. A negative that rounds to zero keeps its sign."""
    if value is None:
        return MISSING
    rounded = Decimal(format(value, ".15g")).quantize(_ONE_DECIMAL, rounding=ROUND_HALF_UP, context=_CONTEXT)
    return format(rounded, "f")


def points(value: Decimal | None) -> str:
    """A training load. A decimal that rounds to zero has no sign, as .NET's decimal prints it."""
    if value is None:
        return MISSING
    rounded = value.quantize(_ONE_DECIMAL, rounding=ROUND_HALF_UP, context=_CONTEXT)
    return format(rounded.copy_abs() if rounded == 0 else rounded, "f")


def percent(fraction: Decimal | None) -> str:
    """A whole-number percentage: "+" above zero, "-" below, and "0" when it rounds to zero."""
    if fraction is None:
        return MISSING
    whole = _CONTEXT.multiply(fraction, _HUNDRED).quantize(_WHOLE, rounding=ROUND_HALF_UP, context=_CONTEXT)
    if whole > 0:
        return f"+{format(whole, 'f')}%"
    if whole < 0:
        return f"{format(whole, 'f')}%"
    return "0%"


def week_change(change: Decimal) -> str:
    """MetricRow's caption: a "+" for a change of zero or more, then the decimal."""
    return ("+" if change >= 0 else "") + points(change)


def duration(moving_time: timedelta) -> str:
    """Hours and padded minutes, or minutes alone under an hour. Seconds are dropped, never rounded."""
    total_minutes = moving_time // timedelta(minutes=1)
    hours, minutes = divmod(total_minutes, 60)
    return f"{hours}h {minutes:02d}m" if hours >= 1 else f"{minutes}m"


def day(value: date) -> str:
    return value.isoformat()
