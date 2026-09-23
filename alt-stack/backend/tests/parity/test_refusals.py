"""009 US1 scenario 6: invalid input is refused naming the same rule, in the reference's words.

The literals are copied from the reference (DateRange.cs, HeartRateSeries.cs,
TrainingMetricsCalculator.cs).
"""

from datetime import date, timedelta
from decimal import Decimal

import pytest

from tla.domain.aggregation import DailyTrainingLoad, LoadBasis
from tla.domain.date_range import DateRange
from tla.domain.heart_rate import HeartRateSample, HeartRateSeries
from tla.domain.metrics import calculate_metrics


def test_an_end_before_the_start():
    with pytest.raises(ValueError) as refusal:
        DateRange(date(2026, 9, 18), date(2026, 9, 1))

    assert str(refusal.value) == "The range ends on 2026-09-01, before it starts on 2026-09-18."


def test_a_19_bpm_sample():
    with pytest.raises(ValueError) as refusal:
        HeartRateSeries([HeartRateSample(timedelta(0), 19)])

    assert str(refusal.value) == "A heart-rate sample of 19 bpm is outside the plausible range of 20-250 bpm."


def test_a_history_with_a_gap():
    history = [DailyTrainingLoad(date(2026, 9, d), Decimal(100), 1, LoadBasis.ESTIMATED) for d in (1, 2, 4)]

    with pytest.raises(ValueError) as refusal:
        calculate_metrics(history, DateRange(date(2026, 9, 1), date(2026, 9, 4)))

    assert str(refusal.value) == (
        "The history is not continuous: the day at index 2 is 2026-09-04, which does not follow 2026-09-02."
    )
