# Coding Interview: Account & Transaction Reconciliation Pipeline

## Scenario

You're joining the Payments platform team. Two legacy branch systems ("East" and "West") export
account data as CSV, and a newer transaction system streams transaction data as JSON batches. The
two sources were never designed to talk to each other: account IDs are numeric in the legacy CSVs,
string-based in the transaction JSON, and the CSV exports are known to be dirty (missing names,
missing dates of birth, inconsistent casing, occasional malformed rows).

Your job: build a small ETL service that reads both sources, cleans and reconciles the data into a
single canonical shape, enforces that shape as a **contract** on write, and publishes valid records
to a message stream. Records that can't be made to satisfy the contract should not be silently
dropped or silently published — they need to go somewhere you can account for.

## Inputs

- `data/accounts_branch_east.csv` — account records from the East branch legacy system
- `data/accounts_branch_west.csv` — account records from the West branch legacy system
- `data/transactions_batch_2026_01.json` — a batch of transactions
- `data/transactions_batch_2026_02.json` — a second batch of transactions (delivered later)

Treat the two account CSVs as two independent, occasionally-overlapping sources of truth about the
same underlying set of accounts — they are not guaranteed to agree with each other. Treat the two
transaction JSON files as batches that could be re-delivered or arrive out of order — don't assume
each file is internally perfect or that the two files are disjoint.

## Output contract

Design (and write down) a canonical schema for two record types — **Account** and **Transaction**
— and enforce it before anything is published. At minimum:

- **Account IDs and Transaction IDs must be strings** in the published record, regardless of how
  they were represented in the source.
- Every required field must be present and correctly typed.
- Dates/timestamps must be normalized to a single consistent format.
- Enumerated fields (status, account type, transaction type, currency) must be normalized to a
  consistent representation.
- A transaction must reference an account that actually exists in the reconciled account set.

You decide the rest of the schema (what's required vs. optional, what the valid enum values are,
what a "duplicate" means for each record type, how to resolve two sources disagreeing about the
same account). Be ready to justify your choices.

## Publishing

You do not need a real Kafka cluster for this exercise. Treat the message stream as an interface:

```
publish(topic: str, key: str, value: dict) -> None
```

A trivial in-memory implementation is provided in the `starter/` folder (pick whichever language
you're most comfortable in, or write your own in a couple of minutes — that's a fine use of time
too). Use it as a stand-in for a Kafka producer. We're evaluating how you'd shape topics, keys, and
payloads, not your familiarity with a specific client library.

Records that fail contract validation should not go to the same topic as valid ones — decide where
they should go and what information travels with them (e.g. which rule failed and why), so someone
downstream could actually act on it.

## What we're looking for

1. **Communication.** Talk through trade-offs as you go, especially anywhere you chose to defer
   something rather than solve it fully. We are explicitly evaluating your ability to communicate
   how you go about solving this problem and the tools you use to get there.
2. **Decomposition and planning.** Before writing code, sketch (out loud, on the whiteboard, or in
   comments) how you're splitting this into stages — extract, validate/normalize, reconcile
   across sources, load/publish — and where the contract boundary lives.
3. **Edge case handling.** The data is deliberately messy. We're watching whether you notice issues
   as you go, categorize them (missing data vs. malformed data vs. cross-source conflict vs.
   referential integrity), and make a deliberate call on each rather than either crashing or
   silently passing bad data through.
4. **Working end-to-end implementation.** By the end of the session we'd like to see the pipeline
   run start to finish on the provided files with something actually published to both a "valid"
   and a "rejected" topic — even if not every edge case is handled, and even if some parts are
   stubbed with a clear TODO and a one-line explanation of what you'd do with more time.

## Explicitly not required

- Standing up real Kafka, Docker, or any external infrastructure.
- Handling every conceivable edge case in the data — perfect coverage is not the bar, a systematic
  and explainable approach is.
- Persistence beyond the running process (no database needed).
