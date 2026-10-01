import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

/**
 * Trivial in-memory stand-in for the Reconciliation Ingest API (see api/openapi.yaml).
 * Hand this to the candidate as-is, or let them write their own equivalent.
 *
 * What it DOES model: per-ID idempotency (201 / 200 / 409) and the transaction ->
 * account reference check (422). What it does NOT do: schema validation. Enforcing
 * the contract before you POST is your job.
 *
 * How to run (JDK 17+, no build step), from the repo root:
 *   java starter/MockApiClient.java              # reads ./data
 *   java starter/MockApiClient.java path/to/data # or point it at another folder
 */
public class MockApiClient {

    public static final String ACCOUNTS = "/v1/accounts";
    public static final String TRANSACTIONS = "/v1/transactions";
    private static final Map<String, String> ID_FIELD =
            Map.of(ACCOUNTS, "account_id", TRANSACTIONS, "transaction_id");

    public record Request(String path, Map<String, Object> body) {}

    public record Response(int status, Map<String, Object> body) {}

    private final List<Request> log = new ArrayList<>();
    private final Map<String, Map<String, Map<String, Object>>> store =
            Map.of(ACCOUNTS, new HashMap<>(), TRANSACTIONS, new HashMap<>());

    public Response post(String path, Map<String, Object> body) {
        String idField = ID_FIELD.get(path);
        if (idField == null) {
            return new Response(404, Map.of("code", "NOT_FOUND", "message", path));
        }
        log.add(new Request(path, body));
        if (!(body.get(idField) instanceof String id)) {
            return new Response(400, Map.of("code", "VALIDATION_FAILED", "message", idField + " must be a string"));
        }
        if (path.equals(TRANSACTIONS) && !store.get(ACCOUNTS).containsKey(body.get("account_id"))) {
            return new Response(422, Map.of("code", "UNKNOWN_ACCOUNT",
                    "message", "account_id " + body.get("account_id") + " not found"));
        }
        Map<String, Object> existing = store.get(path).get(id);
        if (existing == null) {
            store.get(path).put(id, body);
            return new Response(201, Map.of("id", id, "status", "created"));
        }
        if (existing.equals(body)) {
            return new Response(200, Map.of("id", id, "status", "unchanged"));
        }
        return new Response(409, Map.of("code", "CONFLICT", "message", "different payload already stored for " + id));
    }

    public List<Request> requests() {
        return new ArrayList<>(log);
    }

    public List<Request> requests(String path) {
        return log.stream().filter(r -> r.path().equals(path)).collect(Collectors.toList());
    }

    public List<Map<String, Object>> stored(String path) {
        return new ArrayList<>(store.get(path).values());
    }

    public static void main(String[] args) throws IOException {
        Path dataDir = Path.of(args.length > 0 ? args[0] : "data");
        System.out.println("Input files in " + dataDir.toAbsolutePath().normalize() + ":");
        try (var files = Files.list(dataDir)) {
            files.map(p -> p.getFileName().toString()).sorted().forEach(f -> System.out.println("  " + f));
        }

        MockApiClient client = new MockApiClient();
        List<Map<String, Object>> rejected = new ArrayList<>(); // your dead-letter sink: source record + rule + reason

        // TODO 1. Extract:   read data/accounts_branch_*.csv and data/transactions_batch_*.json
        // TODO 2. Validate / normalize each record against api/openapi.yaml
        // TODO 3. Reconcile: merge East + West accounts, dedupe transactions across batches
        // TODO 4. Load:      POST accounts first, then transactions; route failures to `rejected`
        //
        // Example call:
        //   Response resp = client.post(ACCOUNTS, Map.of("account_id", "100001", ...));
        //   if (resp.status() != 200 && resp.status() != 201) {
        //       rejected.add(Map.of("record", ..., "rule", resp.body().get("code"), "reason", resp.body().get("message")));
        //   }

        System.out.println();
        System.out.println("Summary:");
        System.out.println("  requests sent:          " + client.requests().size());
        System.out.println("  accounts accepted:      " + client.stored(ACCOUNTS).size());
        System.out.println("  transactions accepted:  " + client.stored(TRANSACTIONS).size());
        System.out.println("  rejected:               " + rejected.size());
    }
}
