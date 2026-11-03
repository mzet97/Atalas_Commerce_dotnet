namespace Atlas.Control.Modules.Kernel;

using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

/// <summary>
/// Trilha de auditoria append-only com HASH ENCADEADO (SPEC-004 req.3 / REQ-ADM-003):
/// hash = SHA256(prevHash | seq | payload). Qualquer alteração retroativa quebra a
/// cadeia e é detectada pelo verificador (GET /modules/audit/verify).
/// </summary>
public sealed record AuditChainEntry(
    long Seq, string PrevHash, string Hash, string ActorSub, string Action,
    string Decision, string? Detail, string CorrelationId, DateTimeOffset OccurredAtUtc)
{
    public static string ComputeHash(long seq, string prevHash, string actorSub, string action,
        string decision, string? detail, string correlationId, DateTimeOffset occurredAtUtc) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{prevHash}|{seq}|{actorSub}|{action}|{decision}|{detail ?? ""}|{correlationId}|{occurredAtUtc:O}"))).ToLowerInvariant();

    public static AuditChainEntry Create(long seq, string prevHash, string actorSub, string action,
        string decision, string? detail, string correlationId, DateTimeOffset nowUtc) =>
        new(seq, prevHash,
            ComputeHash(seq, prevHash, actorSub, action, decision, detail, correlationId, nowUtc),
            actorSub, action, decision, detail, correlationId, nowUtc);
}

/// <summary>Store append-only do ledger de auditoria (control.audit_chain, seq única).</summary>
public sealed class AuditChainStore
{
    public static AuditChainStore Create(string connectionString, string databaseName)
    {
        var store = new AuditChainStore(new MongoClient(connectionString).GetDatabase(databaseName));
        store.EnsureIndexes();
        return store;
    }

    private readonly IMongoDatabase _db;
    private AuditChainStore(IMongoDatabase db) => _db = db;

    private void EnsureIndexes()
    {
        Entries().Indexes.CreateOne(new CreateIndexModel<AuditEntryDoc>(
            Builders<AuditEntryDoc>.IndexKeys.Ascending(d => d.Seq),
            new CreateIndexOptions { Unique = true }));
    }

    private IMongoCollection<AuditEntryDoc> Entries() => _db.GetCollection<AuditEntryDoc>("control.audit_chain");

    /// <summary>Append atômico: seq = max+1 (FindOneAndUpdate $inc no documento contador), prev = hash do último.</summary>
    public async Task<AuditChainEntry> AppendAsync(string actorSub, string action, string decision,
        string? detail, string correlationId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var counters = _db.GetCollection<BsonDocument>("control.counters");
        var counter = await counters.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.Eq("_id", "audit_chain"),
            Builders<BsonDocument>.Update.Inc("seq", 1),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken: ct);
        var seq = counter["seq"].ToInt64();

        var last = await Entries().Find(Builders<AuditEntryDoc>.Filter.Empty)
            .SortByDescending(d => d.Seq).Limit(1).FirstOrDefaultAsync(ct);
        var prevHash = last?.Hash ?? "GENESIS";

        var entry = AuditChainEntry.Create(seq, prevHash, actorSub, action, decision, detail, correlationId, now);
        await Entries().ReplaceOneAsync(
            d => d.Seq == entry.Seq, AuditEntryDoc.From(entry),
            new ReplaceOptions { IsUpsert = true }, cancellationToken: ct);
        return entry;
    }

    public async Task<IReadOnlyList<AuditChainEntry>> LatestAsync(int limit, CancellationToken ct)
    {
        var docs = await Entries().Find(Builders<AuditEntryDoc>.Filter.Empty)
            .SortByDescending(d => d.Seq).Limit(limit).ToListAsync(ct);
        return [.. docs.Select(d => d.ToEntry()).Reverse()];
    }

    /// <summary>Consulta server-side com paginação e filtro por action/decision (SPEC-004 req.5).</summary>
    public async Task<(IReadOnlyList<AuditChainEntry> Items, long Total)> QueryAsync(
        string? action, string? decision, int page, int pageSize, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<AuditEntryDoc>>();
        if (!string.IsNullOrWhiteSpace(action))
            filters.Add(Builders<AuditEntryDoc>.Filter.Regex(d => d.Action, new BsonRegularExpression(action, "i")));
        if (!string.IsNullOrWhiteSpace(decision))
            filters.Add(Builders<AuditEntryDoc>.Filter.Regex(d => d.Decision, new BsonRegularExpression(decision, "i")));
        var filter = filters.Count == 0
            ? Builders<AuditEntryDoc>.Filter.Empty
            : Builders<AuditEntryDoc>.Filter.And(filters);
        var total = await Entries().CountDocumentsAsync(filter, cancellationToken: ct);
        var docs = await Entries().Find(filter)
            .SortBy(d => d.Seq)
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 200))
            .Limit(Math.Clamp(pageSize, 1, 200))
            .ToListAsync(ct);
        return ([.. docs.Select(d => d.ToEntry())], total);
    }

    /// <summary>Verifica a cadeia inteira: re-hash de cada registro e encadeamento prev→hash.</summary>
    public async Task<(bool Valid, long Count, long? BrokenAtSeq, string? Reason)> VerifyAsync(CancellationToken ct)
    {
        var docs = await Entries().Find(Builders<AuditEntryDoc>.Filter.Empty).SortBy(d => d.Seq).ToListAsync(ct);
        var expectedPrev = "GENESIS";
        var expectedSeq = 1L;
        foreach (var doc in docs)
        {
            var e = doc.ToEntry();
            if (e.Seq != expectedSeq) return (false, docs.Count, e.Seq, $"seq fora de ordem ({e.Seq} != {expectedSeq})");
            if (!string.Equals(e.PrevHash, expectedPrev, StringComparison.Ordinal))
                return (false, docs.Count, e.Seq, "prev_hash divergente");
            var recomputed = AuditChainEntry.ComputeHash(e.Seq, e.PrevHash, e.ActorSub, e.Action, e.Decision, e.Detail, e.CorrelationId, e.OccurredAtUtc);
            if (!string.Equals(e.Hash, recomputed, StringComparison.Ordinal))
                return (false, docs.Count, e.Seq, "hash não confere (registro alterado)");
            expectedPrev = e.Hash;
            expectedSeq++;
        }
        return (true, docs.Count, null, null);
    }

    public sealed class AuditEntryDoc
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; } = Guid.NewGuid();
        public long Seq { get; set; }
        public string PrevHash { get; set; } = default!;
        public string Hash { get; set; } = default!;
        public string ActorSub { get; set; } = default!;
        public string Action { get; set; } = default!;
        public string Decision { get; set; } = default!;
        public string? Detail { get; set; }
        public string CorrelationId { get; set; } = default!;

        [BsonElement("at")]
        public DateTimeOffset OccurredAtUtc { get; set; }

        public static AuditEntryDoc From(AuditChainEntry e) => new()
        {
            Seq = e.Seq, PrevHash = e.PrevHash, Hash = e.Hash, ActorSub = e.ActorSub,
            Action = e.Action, Decision = e.Decision, Detail = e.Detail,
            CorrelationId = e.CorrelationId, OccurredAtUtc = e.OccurredAtUtc,
        };

        public AuditChainEntry ToEntry() => new(Seq, PrevHash, Hash, ActorSub, Action, Decision, Detail, CorrelationId, OccurredAtUtc);
    }
}
