"""009 FR-010: no token reaches a log record or a result, over every recorded scenario."""

import logging

import pytest

from tests.golden import sync_names, sync_scenario
from tests.parity.test_sync_parity import run


@pytest.mark.parametrize("name", sync_names())
def test_no_log_record_and_no_result_carries_a_token(tmp_path, caplog, name):
    scenario = sync_scenario(name)
    secrets = {scenario["connection"]["accessToken"], scenario["connection"]["refreshToken"]}
    if name == "expired-token":
        secrets |= {"test-access-expired-token-renewed", "test-refresh-expired-token-renewed"}

    with caplog.at_level(logging.DEBUG):
        _, result, message, _ = run(tmp_path, name, scenario.get("renewal"))

    text = "\n".join(r.getMessage() + repr(r.args) for r in caplog.records) + repr(result) + message
    assert not [s for s in secrets if s in text]
