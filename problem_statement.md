# Coding Interview: Account & Transaction Reconciliation Service

## Scenario

You're joining the Payments platform team. Two legacy branch systems ("East" and "West") export
account data as CSV, and a newer transaction system exports transaction data as JSON batches. The
two sources were never designed to talk to each other: account IDs are numeric in the legacy CSVs,
string-based in the transaction JSON, and the CSV exports are known to be dirty (missing names,
missing dates of birth, inconsistent casing, occasional malformed rows).

Your job: build a small ETL service that reads both sources, cleans and reconciles the data into a
single canonical shape, enforces that shape as a **contract** on write, and POSTs valid records
to two downstream ingest endpoints — one for accounts, one for transactions. Records that can't be
made to satisfy the contract should not be silently dropped or silently posted — they need to go
somewhere you can account for.

## Inputs

- `data/accounts_branch_east.csv` — account records from the East branch legacy system
- `data/accounts_branch_west.csv` — account records from the West branch legacy system
- `data/transactions_batch_2026_01.json` — a batch of transactions
- `data/transactions_batch_2026_02.json` — a second batch of transactions (delivered later)
- `api/openapi.yaml` — the Swagger/OpenAPI spec for the two endpoints you'll post to

Treat the two account CSVs as two independent, occasionally-overlapping sources of truth about the
same underlying set of accounts — they are not guaranteed to agree with each other. Treat the two
transaction JSON files as batches that could be re-delivered or arrive out of order — don't assume
each file is internally perfect or that the two files are disjoint.

## Output contract

The contract is defined for you in **`api/openapi.yaml`** (OpenAPI 3.0 / Swagger). It describes two
endpoints:

| Endpoint | Body schema | Natural key |
|---|---|---|
| `POST /v1/accounts` | `Account` | `account_id` |
| `POST /v1/transactions` | `Transaction` | `transaction_id` |

Read the spec before you start. Among other things it requires that:

- **Account IDs and Transaction IDs are strings**, regardless of how they were represented in the
  source.
- Every required field is present and correctly typed (e.g. `amount` is a JSON number, not a
  string).
- Dates are `YYYY-MM-DD` and timestamps are ISO-8601 UTC with a `Z` suffix.
- Enumerated fields (status, account type, transaction type, currency) use the exact upper-case
  values listed in the spec.
- A transaction references an account that was already accepted by `POST /v1/accounts`.

The spec defines the *shape*; it deliberately does not tell you how to get there. You still decide
how to normalize each messy source value, what a "duplicate" means for each record type, and how to
resolve the two branch exports disagreeing about the same account. Be ready to justify your choices.
If you think the spec itself is wrong or under-specified somewhere, say so — that's a valid
finding.

## Posting

You do not need a real server for this exercise. Treat the API as an interface:

```
post(path: str, body: dict) -> Response(status: int, body: dict)
```

A small in-memory implementation is provided in the `starter/` folder (pick whichever language
you're most comfortable in, or write your own in a couple of minutes — that's a fine use of time
too). Each starter is a runnable console app with TODOs marking where your stages go; the comment
at the top of each file says how to run it. It behaves like the real endpoints for idempotency and referential checks (`201` created,
`200` identical replay, `409` conflicting payload for an existing ID, `422` unknown `account_id`),
but it does **not** validate the schema — enforcing the contract is your job, on your side of the
wire. Use it as a stand-in for an HTTP client; we're evaluating how you sequence calls, shape
payloads, and handle responses, not your familiarity with a specific HTTP library.

The spec declares bearer-token auth. The mock ignores it, but if you wire up a token, read it from
the `RECON_API_TOKEN` environment variable — never hardcode it.

Things to think about:

- **Ordering.** Transactions depend on accounts. How do you sequence the two endpoints?
- **Responses.** What do you do with a `409` or `422` that your own validation didn't catch?
- **Re-delivery.** The transaction batches may overlap. What should a second run of the service
  over the same files do?

Records that fail contract validation (or are rejected by the API) must not be lost. Send them to a
rejected/dead-letter sink of your choice (an in-memory list, a JSON file, etc.) and decide what
information travels with them (the source record, which rule failed, why, which source file) so
someone downstream could actually act on it.

## What we're looking for

1. **Communication.** Talk through trade-offs as you go, especially anywhere you chose to defer
   something rather than solve it fully. We are explicitly evaluating your ability to communicate
   how you go about solving this problem and the tools you use to get there.
2. **Decomposition and planning.** Before writing code, sketch (out loud, on the whiteboard, or in
   comments) how you're splitting this into stages — extract, validate/normalize, reconcile
   across sources, load/post — and where the contract boundary lives.
3. **Edge case handling.** The data is deliberately messy. We're watching whether you notice issues
   as you go, categorize them (missing data vs. malformed data vs. cross-source conflict vs.
   referential integrity), and make a deliberate call on each rather than either crashing or
   silently passing bad data through.
4. **Working end-to-end implementation.** By the end of the session we'd like to see the service
   run start to finish on the provided files with valid records accepted by **both** endpoints and
   rejected records captured in your rejected sink — even if not every edge case is handled, and
   even if some parts are stubbed with a clear TODO and a one-line explanation of what you'd do
   with more time.

## Explicitly not required

- Standing up a real HTTP server, Docker, or any external infrastructure.
- Handling every conceivable edge case in the data — perfect coverage is not the bar, a systematic
  and explainable approach is.
- Persistence beyond the running process (no database needed).
