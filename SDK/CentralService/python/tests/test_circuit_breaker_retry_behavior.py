from __future__ import annotations

import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
for project_root in (ROOT / "client", ROOT / "service"):
    project_root_str = str(project_root)
    if project_root_str not in sys.path:
        sys.path.insert(0, project_root_str)

from erp_centralservice_client.transport import (  # noqa: E402
    CentralServiceCircuitBreakerOptions as ClientBreakerOptions,
    CircuitBreakerState as ClientCircuitBreakerState,
)
from erp_centralservice_service.transport import (  # noqa: E402
    CentralServiceCircuitBreakerOptions as ServiceBreakerOptions,
    CircuitBreakerState as ServiceCircuitBreakerState,
)


class CircuitBreakerRetryBehaviorTests(unittest.TestCase):
    def _assert_threshold_open_stops_same_endpoint_retry(self, state: object) -> None:
        self.assertFalse(state.report_failure(100.0))
        allowed, reason = state.try_allow_request(100.0)
        self.assertFalse(allowed)
        self.assertIn("熔断开启", reason or "")

    def _assert_half_open_failure_stops_same_endpoint_retry(self, state: object) -> None:
        self.assertFalse(state.report_failure(100.0))
        allowed, reason = state.try_allow_request(161.0)
        self.assertTrue(allowed)
        self.assertIsNone(reason)

        self.assertFalse(state.report_failure(161.0))
        allowed, reason = state.try_allow_request(161.0)
        self.assertFalse(allowed)
        self.assertIn("熔断开启", reason or "")

    def test_client_breaker_stops_retry_after_open(self) -> None:
        state = ClientCircuitBreakerState(
            ClientBreakerOptions(failure_threshold=1, break_duration_minutes=1, recovery_threshold=1)
        )
        self._assert_threshold_open_stops_same_endpoint_retry(state)

    def test_service_breaker_stops_retry_after_open(self) -> None:
        state = ServiceCircuitBreakerState(
            ServiceBreakerOptions(failure_threshold=1, break_duration_minutes=1, recovery_threshold=1)
        )
        self._assert_threshold_open_stops_same_endpoint_retry(state)

    def test_client_half_open_failure_stops_retry(self) -> None:
        state = ClientCircuitBreakerState(
            ClientBreakerOptions(failure_threshold=1, break_duration_minutes=1, recovery_threshold=1)
        )
        self._assert_half_open_failure_stops_same_endpoint_retry(state)

    def test_service_half_open_failure_stops_retry(self) -> None:
        state = ServiceCircuitBreakerState(
            ServiceBreakerOptions(failure_threshold=1, break_duration_minutes=1, recovery_threshold=1)
        )
        self._assert_half_open_failure_stops_same_endpoint_retry(state)


if __name__ == "__main__":
    unittest.main()
