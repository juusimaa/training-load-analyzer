"""The one arithmetic context every load computation runs under (research R1).

Precision 34 (IEEE decimal128), ties to even. Loads are compared with the reference to within
1e-20 points (Amendment 1(a)), because .NET's decimal rounds in different places.
"""

from datetime import timedelta
from decimal import ROUND_HALF_EVEN, Context, Decimal

LOAD = Context(prec=34, rounding=ROUND_HALF_EVEN)

_MICROSECONDS_PER_MINUTE = Decimal(60_000_000)


def minutes(span: timedelta) -> Decimal:
    """A span in minutes, exactly as far as the context allows."""
    return LOAD.divide(Decimal(span // timedelta(microseconds=1)), _MICROSECONDS_PER_MINUTE)
