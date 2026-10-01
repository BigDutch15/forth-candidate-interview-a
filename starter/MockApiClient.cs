// How to run (.NET 10+ SDK, no .csproj needed), from the repo root:
//   dotnet run starter/MockApiClient.cs              # reads ./data
//   dotnet run starter/MockApiClient.cs path/to/data # or point it at another folder
// On .NET 8/9, create a console project (`dotnet new console`), copy this file in
// place of Program.cs, and `dotnet run -- path/to/data`.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

var dataDir = args.Length > 0 ? args[0] : "data";
Console.WriteLine($"Input files in {Path.GetFullPath(dataDir)}:");
foreach (var f in Directory.GetFiles(dataDir).Select(Path.GetFileName).Order())
    Console.WriteLine($"  {f}");

var client = new MockApiClient();
var rejected = new List<Dictionary<string, object?>>(); // your dead-letter sink: source record + rule + reason

// TODO 1. Extract:   read data/accounts_branch_*.csv and data/transactions_batch_*.json
// TODO 2. Validate / normalize each record against api/openapi.yaml
// TODO 3. Reconcile: merge East + West accounts, dedupe transactions across batches
// TODO 4. Load:      POST accounts first, then transactions; route failures to `rejected`
//
// Example call:
//   var resp = client.Post(MockApiClient.Accounts, new Dictionary<string, object?> { ["account_id"] = "100001", ... });
//   if (resp.Status is not (200 or 201))
//       rejected.Add(new() { ["record"] = ..., ["rule"] = resp.Body["code"], ["reason"] = resp.Body["message"] });

Console.WriteLine();
Console.WriteLine("Summary:");
Console.WriteLine($"  requests sent:          {client.Requests().Count}");
Console.WriteLine($"  accounts accepted:      {client.Stored(MockApiClient.Accounts).Count}");
Console.WriteLine($"  transactions accepted:  {client.Stored(MockApiClient.Transactions).Count}");
Console.WriteLine($"  rejected:               {rejected.Count}");


/// <summary>
/// Trivial in-memory stand-in for the Reconciliation Ingest API (see api/openapi.yaml).
/// Hand this to the candidate as-is, or let them write their own equivalent.
///
/// What it DOES model: per-ID idempotency (201 / 200 / 409) and the transaction ->
/// account reference check (422). What it does NOT do: schema validation. Enforcing
/// the contract before you POST is your job.
/// </summary>
public record ApiRequest(string Path, IReadOnlyDictionary<string, object?> Body);

public record ApiResponse(int Status, IReadOnlyDictionary<string, object?> Body);

public class MockApiClient
{
    public const string Accounts = "/v1/accounts";
    public const string Transactions = "/v1/transactions";

    private static readonly Dictionary<string, string> IdField = new()
    {
        [Accounts] = "account_id",
        [Transactions] = "transaction_id",
    };

    private readonly List<ApiRequest> _log = new();
    private readonly Dictionary<string, Dictionary<string, IReadOnlyDictionary<string, object?>>> _store = new()
    {
        [Accounts] = new(),
        [Transactions] = new(),
    };

    public ApiResponse Post(string path, IReadOnlyDictionary<string, object?> body)
    {
        if (!IdField.TryGetValue(path, out var idField))
            return Error(404, "NOT_FOUND", path);
        _log.Add(new ApiRequest(path, body));
        if (!body.TryGetValue(idField, out var raw) || raw is not string id)
            return Error(400, "VALIDATION_FAILED", $"{idField} must be a string");
        if (path == Transactions &&
            !(body.TryGetValue("account_id", out var acct) && acct is string a && _store[Accounts].ContainsKey(a)))
            return Error(422, "UNKNOWN_ACCOUNT", $"account_id {acct} not found");
        if (!_store[path].TryGetValue(id, out var existing))
        {
            _store[path][id] = body;
            return Accepted(201, id, "created");
        }
        return Canonical(existing) == Canonical(body)
            ? Accepted(200, id, "unchanged")
            : Error(409, "CONFLICT", $"different payload already stored for {id}");
    }

    public IReadOnlyList<ApiRequest> Requests(string? path = null)
    {
        return path is null
            ? _log.ToList()
            : _log.Where(r => r.Path == path).ToList();
    }

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Stored(string path) => _store[path].Values.ToList();

    private static string Canonical(IReadOnlyDictionary<string, object?> v) =>
        JsonSerializer.Serialize(v.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value));

    private static ApiResponse Accepted(int status, string id, string state) =>
        new(status, new Dictionary<string, object?> { ["id"] = id, ["status"] = state });

    private static ApiResponse Error(int status, string code, string message) =>
        new(status, new Dictionary<string, object?> { ["code"] = code, ["message"] = message });
}
