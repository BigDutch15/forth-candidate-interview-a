/**
 * Trivial in-memory stand-in for the Reconciliation Ingest API (see api/openapi.yaml).
 * Hand this to the candidate as-is, or let them write their own equivalent.
 *
 * What it DOES model: per-ID idempotency (201 / 200 / 409) and the transaction ->
 * account reference check (422). What it does NOT do: schema validation. Enforcing
 * the contract before you POST is your job.
 *
 * How to run (Node 18+), from the repo root:
 *   npx tsx starter/mockApiClient.ts              # reads ./data next to starter/
 *   npx tsx starter/mockApiClient.ts path/to/data # or point it at another folder
 */
import * as fs from "node:fs";
import * as path from "node:path";

export const ACCOUNTS = "/v1/accounts";
export const TRANSACTIONS = "/v1/transactions";
const ID_FIELD: Record<string, string> = { [ACCOUNTS]: "account_id", [TRANSACTIONS]: "transaction_id" };

export interface Request {
  path: string;
  body: Record<string, unknown>;
}

export interface Response {
  status: number;
  body: Record<string, unknown>;
}

const canonical = (v: Record<string, unknown>): string =>
  JSON.stringify(Object.keys(v).sort().map((k) => [k, v[k]]));

export class MockApiClient {
  private log: Request[] = [];
  private store: Record<string, Map<string, Record<string, unknown>>> = {
    [ACCOUNTS]: new Map(),
    [TRANSACTIONS]: new Map(),
  };

  post(path: string, body: Record<string, unknown>): Response {
    const idField = ID_FIELD[path];
    if (!idField) return { status: 404, body: { code: "NOT_FOUND", message: path } };
    this.log.push({ path, body });
    const id = body[idField];
    if (typeof id !== "string") {
      return { status: 400, body: { code: "VALIDATION_FAILED", message: `${idField} must be a string` } };
    }
    if (path === TRANSACTIONS && !this.store[ACCOUNTS].has(body.account_id as string)) {
      return { status: 422, body: { code: "UNKNOWN_ACCOUNT", message: `account_id ${String(body.account_id)} not found` } };
    }
    const existing = this.store[path].get(id);
    if (!existing) {
      this.store[path].set(id, body);
      return { status: 201, body: { id, status: "created" } };
    }
    if (canonical(existing) === canonical(body)) return { status: 200, body: { id, status: "unchanged" } };
    return { status: 409, body: { code: "CONFLICT", message: `different payload already stored for ${id}` } };
  }

  requests(path?: string): Request[] {
    if (!path) return [...this.log];
    return this.log.filter((r) => r.path === path);
  }

  stored(path: string): Record<string, unknown>[] {
    return [...this.store[path].values()];
  }
}

function main(argv: string[]): number {
  const dataDir = argv[2] ?? path.join(path.dirname(path.resolve(argv[1])), "..", "data");
  console.log(`Input files in ${dataDir}:`);
  for (const f of fs.readdirSync(dataDir).sort()) console.log(`  ${f}`);

  const client = new MockApiClient();
  const rejected: Record<string, unknown>[] = []; // your dead-letter sink: source record + rule + reason

  // TODO 1. Extract:   read data/accounts_branch_*.csv and data/transactions_batch_*.json
  // TODO 2. Validate / normalize each record against api/openapi.yaml
  // TODO 3. Reconcile: merge East + West accounts, dedupe transactions across batches
  // TODO 4. Load:      POST accounts first, then transactions; route failures to `rejected`
  //
  // Example call:
  //   const resp = client.post(ACCOUNTS, { account_id: "100001", ... });
  //   if (resp.status !== 200 && resp.status !== 201) {
  //     rejected.push({ record: ..., rule: resp.body.code, reason: resp.body.message });
  //   }

  console.log("\nSummary:");
  console.log(`  requests sent:          ${client.requests().length}`);
  console.log(`  accounts accepted:      ${client.stored(ACCOUNTS).length}`);
  console.log(`  transactions accepted:  ${client.stored(TRANSACTIONS).length}`);
  console.log(`  rejected:               ${rejected.length}`);
  return 0;
}

// Run only when executed directly, not when imported.
if (process.argv[1] && path.basename(process.argv[1]).startsWith("mockApiClient")) {
  process.exit(main(process.argv));
}
