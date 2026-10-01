"""
Trivial in-memory stand-in for the Reconciliation Ingest API (see api/openapi.yaml).
Hand this to the candidate as-is, or let them write their own equivalent.

What it DOES model: per-ID idempotency (201 / 200 / 409) and the transaction ->
account reference check (422). What it does NOT do: schema validation. Enforcing
the contract before you POST is your job.

How to run (Python 3.9+, no dependencies), from the repo root:
    python3 starter/mock_api_client.py              # reads ./data next to starter/
    python3 starter/mock_api_client.py path/to/data # or point it at another folder
"""
from __future__ import annotations

import json
import sys
from pathlib import Path
from dataclasses import dataclass
from typing import Any

ACCOUNTS = "/v1/accounts"
TRANSACTIONS = "/v1/transactions"
_ID_FIELD = {ACCOUNTS: "account_id", TRANSACTIONS: "transaction_id"}


@dataclass
class Request:
    path: str
    body: dict[str, Any]


@dataclass
class Response:
    status: int
    body: dict[str, Any]


class MockApiClient:
    def __init__(self) -> None:
        self._log: list[Request] = []
        self._store: dict[str, dict[str, dict[str, Any]]] = {ACCOUNTS: {}, TRANSACTIONS: {}}

    def post(self, path: str, body: dict[str, Any]) -> Response:
        if path not in _ID_FIELD:
            return Response(404, {"code": "NOT_FOUND", "message": path})
        self._log.append(Request(path, body))
        record_id = body.get(_ID_FIELD[path])
        if not isinstance(record_id, str):
            return Response(400, {"code": "VALIDATION_FAILED", "message": f"{_ID_FIELD[path]} must be a string"})
        if path == TRANSACTIONS and body.get("account_id") not in self._store[ACCOUNTS]:
            return Response(422, {"code": "UNKNOWN_ACCOUNT", "message": f"account_id {body.get('account_id')!r} not found"})
        existing = self._store[path].get(record_id)
        if existing is None:
            self._store[path][record_id] = body
            return Response(201, {"id": record_id, "status": "created"})
        if json.dumps(existing, sort_keys=True) == json.dumps(body, sort_keys=True):
            return Response(200, {"id": record_id, "status": "unchanged"})
        return Response(409, {"code": "CONFLICT", "message": f"different payload already stored for {record_id}"})

    def requests(self, path: str | None = None) -> list[Request]:
        if path is None:
            return list(self._log)
        return [r for r in self._log if r.path == path]

    def stored(self, path: str) -> list[dict[str, Any]]:
        return list(self._store[path].values())


def main(argv: list[str]) -> int:
    data_dir = Path(argv[1]) if len(argv) > 1 else Path(__file__).resolve().parent.parent / "data"
    print(f"Input files in {data_dir}:")
    for f in sorted(data_dir.iterdir()):
        print(f"  {f.name}")

    client = MockApiClient()
    rejected: list[dict[str, Any]] = []  # your dead-letter sink: source record + rule + reason

    # TODO 1. Extract:   read data/accounts_branch_*.csv and data/transactions_batch_*.json
    # TODO 2. Validate / normalize each record against api/openapi.yaml
    # TODO 3. Reconcile: merge East + West accounts, dedupe transactions across batches
    # TODO 4. Load:      POST accounts first, then transactions; route failures to `rejected`
    #
    # Example call:
    #   resp = client.post(ACCOUNTS, {"account_id": "100001", ...})
    #   if resp.status not in (200, 201):
    #       rejected.append({"record": ..., "rule": resp.body["code"], "reason": resp.body["message"]})

    print("\nSummary:")
    print(f"  requests sent:          {len(client.requests())}")
    print(f"  accounts accepted:      {len(client.stored(ACCOUNTS))}")
    print(f"  transactions accepted:  {len(client.stored(TRANSACTIONS))}")
    print(f"  rejected:               {len(rejected)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
