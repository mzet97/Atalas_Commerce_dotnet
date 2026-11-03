namespace Atlas.Control.Modules.Approvals;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

/// <summary>
/// Estado PRÓPRIO do Control (ADR-0001): approvals e audit em Mongo dedicado
/// (database atlas_control — NUNCA nas coleções dos owners).
/// </summary>
public sealed class ApprovalsStore
{
    private static readonly bool _guid = RegisterGuid();
    private static bool RegisterGuid()
    {
        try { BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); }
        catch (BsonSerializationException) { }
        return true;
    }

    private readonly IMongoDatabase _db;

    public ApprovalsStore(IMongoDatabase db) => _db = db;

    public static ApprovalsStore Create(string connectionString, string databaseName) =>
        new(new MongoClient(connectionString).GetDatabase(databaseName));

    private IMongoCollection<ApprovalDoc> Approvals() => _db.GetCollection<ApprovalDoc>("control.approvals");
    private IMongoCollection<AuditDoc> Audit() => _db.GetCollection<AuditDoc>("control.audit");

    public async Task<Guid> InsertApprovalAsync(Approval approval, CancellationToken ct)
    {
        await Approvals().InsertOneAsync(ApprovalDoc.From(approval), cancellationToken: ct);
        return approval.Id;
    }

    public async Task<Approval?> GetAsync(Guid id, CancellationToken ct)
    {
        var doc = await Approvals().Find(a => a.Id == id).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task SaveAsync(Approval approval, CancellationToken ct)
    {
        // ToDomain não hidrata CreatedAt no ctor → preservar do doc original
        var original = await Approvals().Find(a => a.Id == approval.Id).FirstOrDefaultAsync(ct);
        if (original is null) return;
        var doc = ApprovalDoc.From(approval);
        doc.CreatedAtUtc = original.CreatedAtUtc;
        await Approvals().ReplaceOneAsync(a => a.Id == approval.Id, doc, cancellationToken: ct);
    }

    /// <summary>Append-only: negações (403/409) e ações entram na trilha (REQ-ADM-003).</summary>
    public async Task AppendAuditAsync(AuditRecord record, CancellationToken ct)
    {
        await Audit().InsertOneAsync(AuditDoc.From(record), cancellationToken: ct);
    }

    public async Task<int> AuditCountAsync(string actorSub, string decision, CancellationToken ct) =>
        (int)await Audit().CountDocumentsAsync(a => a.ActorSub == actorSub && a.Decision == decision, cancellationToken: ct);

    // ---- documentos ----
    public sealed class ApprovalDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string RequestedBy { get; set; } = default!;
        public string ProductId { get; set; } = default!;
        public long ExpectedVersion { get; set; }
        public string MarketId { get; set; } = default!;
        public string Justification { get; set; } = default!;
        public string PayloadHash { get; set; } = default!;
        public ApprovalState State { get; set; }
        public string? DecidedBy { get; set; }
        public string? RejectionReason { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? DecidedAtUtc { get; set; }

        public static ApprovalDoc From(Approval a) => new()
        {
            Id = a.Id, RequestedBy = a.RequestedBy, ProductId = a.ProductId,
            ExpectedVersion = a.ExpectedVersion, MarketId = a.MarketId,
            Justification = a.Justification, PayloadHash = a.PayloadHash,
            State = a.State, DecidedBy = a.DecidedBy, RejectionReason = a.RejectionReason,
            CreatedAtUtc = a.CreatedAtUtc, DecidedAtUtc = a.DecidedAtUtc,
        };

        public Approval ToDomain() => Approval.Hydrate(
            Id, RequestedBy, ProductId, ExpectedVersion, MarketId, Justification,
            State, DecidedBy, RejectionReason, CreatedAtUtc, DecidedAtUtc);
    }

    public sealed class AuditDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string ActorSub { get; set; } = default!;
        public string Action { get; set; } = default!;
        public string Decision { get; set; } = default!;
        public string? Detail { get; set; }
        public string CorrelationId { get; set; } = default!;
        public DateTimeOffset OccurredAtUtc { get; set; }

        public static AuditDoc From(AuditRecord r) => new()
        {
            Id = r.Id, ActorSub = r.ActorSub, Action = r.Action,
            Decision = r.Decision, Detail = r.Detail,
            CorrelationId = r.CorrelationId, OccurredAtUtc = r.OccurredAtUtc,
        };
    }
}
