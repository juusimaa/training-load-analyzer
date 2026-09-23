"""Turns a history fixture (parity.md §2) into domain values, as the generator does with the reference's."""

from datetime import date, datetime, timedelta
from decimal import Decimal

from tla.domain.activity import ActivityType, TrainingActivity
from tla.domain.heart_rate import HeartRateSample, HeartRateSeries


def seconds(value) -> timedelta:
    return timedelta(microseconds=int(Decimal(str(value)) * 1_000_000))


def activity(entry: dict) -> TrainingActivity:
    samples = entry["heartRate"]
    series = None if samples is None else HeartRateSeries([HeartRateSample(seconds(s), int(bpm)) for s, bpm in samples])
    return TrainingActivity(
        entry["id"],
        datetime.fromisoformat(entry["start"]),
        seconds(entry["movingSeconds"]),
        ActivityType(entry["type"]),
        series,
    )


def activities(fixture: dict) -> list[TrainingActivity]:
    return [activity(a) for a in fixture["activities"]]


def day(text: str) -> date:
    return date.fromisoformat(text)
