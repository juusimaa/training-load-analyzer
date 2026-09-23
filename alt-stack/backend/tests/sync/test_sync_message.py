"""Ported from SyncMessageTests.cs; every row of the http-api.md §3 table against golden/probes/display.json."""

from datetime import datetime

import pytest

from tests.golden import load_probes
from tla.dashboard.sync_message import CONNECTION_REQUIRED, for_status
from tla.sync.coordinator import SyncStatus
from tla.sync.results import SyncOutcome, SyncResult

ROWS = load_probes()["syncMessage"]


def status_of(row: dict) -> SyncStatus:
    described = row["status"]
    result = None
    if described["outcome"] is not None:
        outcome = SyncOutcome(described["outcome"]) if described["outcome"] != "Unrecognised" else "an outcome added later"
        result = SyncResult(imported=described["imported"], updated=described["updated"], outcome=outcome)  # type: ignore[arg-type]
    parse = lambda text: None if text is None else datetime.fromisoformat(text)  # noqa: E731
    return SyncStatus(
        is_running=described["isRunning"],
        result=result,
        failure=described["failure"],
        finished_at=parse(described["finishedAt"]),
        retry_after_local=parse(described["retryAfterLocal"]),
    )


@pytest.mark.parametrize("row", ROWS, ids=lambda r: r["row"])
def test_every_row_of_the_message_table_matches_the_reference(row):
    assert for_status(status_of(row)) == row["message"]


def test_the_table_covers_every_row_the_contract_lists():
    assert {r["row"] for r in ROWS} == {
        "running", "notConnected", "never", "completedImported", "completedNothingNew", "rateLimitedKnown",
        "rateLimitedUnknown", "interrupted", "reconnectionRequired", "refused", "unrecognised"}


def test_the_connection_prompt_is_the_reference_s():
    assert CONNECTION_REQUIRED == "Strava connection required."
