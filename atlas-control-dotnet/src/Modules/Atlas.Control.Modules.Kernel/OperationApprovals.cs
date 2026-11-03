namespace Atlas.Control.Modules.Kernel;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

/// <summary>
/// Escopo ABAC dinâmico (SPEC-004 req.2): grant de {subject, action, warehouse} com
/// expiração. A REVOGAÇÃO reflete na próxima chamada — o Control consulta o grant
/// no REQUEST, no APPROVE e no DISPATCH (owner revalida o token assinado; INV-SEC-001).
/// </summary>
public sealed class Grant
{
    public Guid Id { get; private set; }
    public string Subject { get; private set; } = default!;
    public string Action { get; private set; } = default!;      // ex.: inventory:adjust
    public string WarehouseId { get; private set; } = default!; // escopo ABAC
    public GrantState State { get; private set; } = GrantState.Active;
    public string GrantedBy { get; private set; } = default!;
    public string? RevokedBy { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public enum GrantState { Active, Revoked }

    private Grant() { }

    public static Grant Create(string subject, string action, string warehouseId, TimeSpan ttl, string grantedBy, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("subject obrigatório");
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("action obrigatória");
        return new()
        {
            Id = Guid.NewGuid(), Subject = subject, Action = action, WarehouseId = warehouseId,
            GrantedBy = grantedBy, ExpiresAtUtc = nowUtc.Add(ttl), CreatedAtUtc = nowUtc, State = GrantState.Active,
        };
    }

    /// <summary>Reidratação a partir do store — preserva estado e expiração.</summary>
    public static Grant Hydrate(Guid id, string subject, string action, string warehouseId,
        GrantState state, string grantedBy, string? revokedBy, DateTimeOffset expiresAtUtc, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = id, Subject = subject, Action = action, WarehouseId = warehouseId, State = state,
            GrantedBy = grantedBy, RevokedBy = revokedBy, ExpiresAtUtc = expiresAtUtc, CreatedAtUtc = createdAtUtc,
        };

    public bool IsActiveAt(DateTimeOffset nowUtc) => State == GrantState.Active && nowUtc <= ExpiresAtUtc;

    public void Revoke(string revokedBy, DateTimeOffset nowUtc)
    {
        if (State != GrantState.Active) return; // idempotente
        if (nowUtc > ExpiresAtUtc) return;      // já expirado
        State = GrantState.Revoked;
        RevokedBy = revokedBy;
    }
}

/// <summary>
/// Approval de operação de risco GENERALIZADO (SPEC-004 req.1 — four-eyes): {requestedBy,
/// action, subject, justification} com token v2 {sub, action, subject, exp, jti} emitido na
/// decisão do approver distinto. Sucessor do Approval de publish (M2); consolidação no
/// release dos contratos (ADR-0012). Owner REVALIDA assinatura/exp/action/subject.
/// </summary>
public sealed class OperationApproval
{
    public Guid Id { get; private set; }
    public string RequestedBy { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string Subject { get; private set; } = default!;
    public string Justification { get; private set; } = default!;
    public OperationApprovalState State { get; private set; } = OperationApprovalState.Requested;
    public string? DecidedBy { get; private set; }
    public string? ApprovalToken { get; private set; }
    public string? Jti { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public DateTimeOffset? DispatchedAtUtc { get; private set; }

    private OperationApproval() { }

    /// <summary>Reidratação a partir do store — preserva estado, token e jti.</summary>
    public static OperationApproval Hydrate(Guid id, string requestedBy, string action, string subject,
        string justification, OperationApprovalState state, string? decidedBy, string? approvalToken,
        string? jti, DateTimeOffset createdAtUtc, DateTimeOffset? decidedAtUtc, DateTimeOffset? dispatchedAtUtc) =>
        new()
        {
            Id = id, RequestedBy = requestedBy, Action = action, Subject = subject,
            Justification = justification, State = state, DecidedBy = decidedBy,
            ApprovalToken = approvalToken, Jti = jti, CreatedAtUtc = createdAtUtc,
            DecidedAtUtc = decidedAtUtc, DispatchedAtUtc = dispatchedAtUtc,
        };

    public static OperationApproval Create(string requestedBy, string action, string subject, string justification, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(justification)) throw new ArgumentException("justificativa obrigatória (INV-ADMIN-001)");
        return new()
        {
            Id = Guid.NewGuid(), RequestedBy = requestedBy, Action = action, Subject = subject,
            Justification = justification, CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>Decisão do approver. Self-approval é VIOLAÇÃO registrada, nunca fluxo válido.</summary>
    public void Approve(string approverSub, DateTimeOffset nowUtc)
    {
        if (State != OperationApprovalState.Requested)
            throw new InvalidOperationException($"Transição inválida: {State} → Approved");
        if (string.Equals(approverSub, RequestedBy, StringComparison.Ordinal))
            throw new SelfApprovalException(RequestedBy);
        DecidedBy = approverSub;
        DecidedAtUtc = nowUtc;
        State = OperationApprovalState.Approved;
    }

    public void Reject(string approverSub, string reason, DateTimeOffset nowUtc)
    {
        if (State != OperationApprovalState.Requested)
            throw new InvalidOperationException($"Transição inválida: {State} → Rejected");
        if (string.Equals(approverSub, RequestedBy, StringComparison.Ordinal))
            throw new SelfApprovalException(RequestedBy);
        DecidedBy = approverSub;
        DecidedAtUtc = nowUtc;
        State = OperationApprovalState.Rejected;
    }

    public void MarkTokenIssued(string token, string jti)
    {
        ApprovalToken = token;
        Jti = jti;
    }

    public void MarkDispatched(DateTimeOffset nowUtc)
    {
        if (State != OperationApprovalState.Approved) throw new InvalidOperationException($"Transição inválida: {State} → Dispatched");
        State = OperationApprovalState.Dispatched;
        DispatchedAtUtc = nowUtc;
    }

    public void MarkFinal(OperationApprovalState final, DateTimeOffset nowUtc)
    {
        if (State != OperationApprovalState.Dispatched) throw new InvalidOperationException($"Transição inválida: {State} → {final}");
        State = final;
    }
}

public enum OperationApprovalState { Requested, Approved, Rejected, Dispatched, Executed, Failed }

/// <summary>Four-eyes violado: o approver tentou aprovar a própria solicitação (auditado como DENIED_SELF_APPROVAL).</summary>
public sealed class SelfApprovalException(string sub) : InvalidOperationException($"four-eyes violado: {sub} não pode aprovar a própria solicitação");

/// <summary>Emissor do token v2 no Control — mesmo algoritmo validado pelo owner (StockApprovalToken).</summary>
public static class StockTokenIssuer
{
    public static string Issue(string approverSub, string action, string subject, TimeSpan ttl, string secret, DateTimeOffset nowUtc, out string jti)
    {
        jti = Guid.NewGuid().ToString();
        var payload = new
        {
            sub = approverSub,
            action,
            subject,
            exp = nowUtc.Add(ttl).ToUnixTimeSeconds(),
            jti,
        };
        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))).TrimEnd('=');
        var sig = Convert.ToBase64String(new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(body))).TrimEnd('=');
        return $"{body}.{sig}";
    }
}

/// <summary>Store Mongo do Control: operation_approvals + grants + used_tokens (nonce replay).</summary>
public sealed class OperationApprovalsStore
{
    public static OperationApprovalsStore Create(string connectionString, string databaseName)
    {
        var store = new OperationApprovalsStore(new MongoClient(connectionString).GetDatabase(databaseName));
        store.EnsureIndexes();
        return store;
    }

    private readonly IMongoDatabase _db;
    private OperationApprovalsStore(IMongoDatabase db) => _db = db;

    private void EnsureIndexes()
    {
        // _id é único por natureza (sem índice adicional). jti único: replay → 11000 → negado.
        UsedTokens().Indexes.CreateOne(new CreateIndexModel<UsedTokenDoc>(
            Builders<UsedTokenDoc>.IndexKeys.Ascending(d => d.Jti),
            new CreateIndexOptions { Unique = true }));
    }

    private IMongoCollection<OperationApprovalDoc> Approvals() => _db.GetCollection<OperationApprovalDoc>("control.operation_approvals");
    private IMongoCollection<GrantDoc> Grants() => _db.GetCollection<GrantDoc>("control.grants");
    private IMongoCollection<UsedTokenDoc> UsedTokens() => _db.GetCollection<UsedTokenDoc>("control.used_tokens");

    public Task InsertApprovalAsync(OperationApproval approval, CancellationToken ct) =>
        Approvals().ReplaceOneAsync(d => d.Id == approval.Id, OperationApprovalDoc.From(approval),
            new ReplaceOptions { IsUpsert = true }, cancellationToken: ct);

    public async Task<OperationApproval?> GetApprovalAsync(Guid id, CancellationToken ct)
    {
        var doc = await Approvals().Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public Task SaveApprovalAsync(OperationApproval approval, CancellationToken ct) => InsertApprovalAsync(approval, ct);

    /// <summary>Queima o nonce do token (uso único). False = jti já usado → replay.</summary>
    public async Task<bool> TryBurnTokenAsync(string jti, string approvalId, CancellationToken ct)
    {
        try
        {
            await UsedTokens().InsertOneAsync(
                new UsedTokenDoc { Jti = jti, ApprovalId = approvalId, UsedAtUtc = DateTimeOffset.UtcNow },
                new InsertOneOptions(), ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            return false; // replay: mesmo jti já consumido
        }
    }

    public Task InsertGrantAsync(Grant grant, CancellationToken ct) =>
        Grants().InsertOneAsync(GrantDoc.From(grant), cancellationToken: ct);

    public async Task<bool> RevokeGrantAsync(Guid grantId, string revokedBy, CancellationToken ct)
    {
        var result = await Grants().UpdateOneAsync(
            d => d.Id == grantId && d.State == Grant.GrantState.Active && d.ExpiresAtUtc > DateTimeOffset.UtcNow,
            Builders<GrantDoc>.Update.Set(d => d.State, Grant.GrantState.Revoked).Set(d => d.RevokedBy, revokedBy),
            cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    /// <summary>Grant ATIVO do par (subject, action) no escopo do warehouse (ou global "*").</summary>
    public async Task<bool> HasActiveGrantAsync(string subject, string action, string warehouseId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var filter = Builders<GrantDoc>.Filter.And(
            Builders<GrantDoc>.Filter.Eq(d => d.Subject, subject),
            Builders<GrantDoc>.Filter.Eq(d => d.Action, action),
            Builders<GrantDoc>.Filter.Eq(d => d.State, Grant.GrantState.Active),
            Builders<GrantDoc>.Filter.Gte(d => d.ExpiresAtUtc, now),
            Builders<GrantDoc>.Filter.Or(
                Builders<GrantDoc>.Filter.Eq(d => d.WarehouseId, warehouseId),
                Builders<GrantDoc>.Filter.Eq(d => d.WarehouseId, "*")));
        return await Grants().Find(filter).AnyAsync(ct);
    }

    /// <summary>Listagem server-side paginada (SPEC-004 req.5).</summary>
    public async Task<(IReadOnlyList<OperationApproval> Items, long Total)> ListApprovalsAsync(
        string? state, int page, int pageSize, CancellationToken ct)
    {
        var filter = string.IsNullOrWhiteSpace(state)
            ? Builders<OperationApprovalDoc>.Filter.Empty
            : Builders<OperationApprovalDoc>.Filter.Eq(d => d.State,
                Enum.Parse<OperationApprovalState>(state, true));
        var total = await Approvals().CountDocumentsAsync(filter, cancellationToken: ct);
        var docs = await Approvals().Find(filter)
            .SortByDescending(d => d.CreatedAtUtc)
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 200))
            .Limit(Math.Clamp(pageSize, 1, 200))
            .ToListAsync(ct);
        return ([.. docs.Select(d => d.ToDomain())], total);
    }

    public async Task<IReadOnlyList<Grant>> ListGrantsAsync(string? subject, CancellationToken ct)
    {
        var filter = subject is null ? Builders<GrantDoc>.Filter.Empty : Builders<GrantDoc>.Filter.Eq(d => d.Subject, subject);
        var docs = await Grants().Find(filter).ToListAsync(ct);
        return [.. docs.Select(d => d.ToDomain())];
    }

    public sealed class UsedTokenDoc
    {
        public string Jti { get; set; } = default!;
        public string ApprovalId { get; set; } = default!;
        public DateTimeOffset UsedAtUtc { get; set; }
    }

    public sealed class OperationApprovalDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string RequestedBy { get; set; } = default!;
        public string Action { get; set; } = default!;
        public string Subject { get; set; } = default!;
        public string Justification { get; set; } = default!;
        public OperationApprovalState State { get; set; }
        public string? DecidedBy { get; set; }
        public string? ApprovalToken { get; set; }
        public string? Jti { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? DecidedAtUtc { get; set; }
        public DateTimeOffset? DispatchedAtUtc { get; set; }

        public static OperationApprovalDoc From(OperationApproval a) => new()
        {
            Id = a.Id, RequestedBy = a.RequestedBy, Action = a.Action, Subject = a.Subject,
            Justification = a.Justification, State = a.State, DecidedBy = a.DecidedBy,
            ApprovalToken = a.ApprovalToken, Jti = a.Jti, CreatedAtUtc = a.CreatedAtUtc,
            DecidedAtUtc = a.DecidedAtUtc, DispatchedAtUtc = a.DispatchedAtUtc,
        };

        public OperationApproval ToDomain() => OperationApproval.Hydrate(
            Id, RequestedBy, Action, Subject, Justification, State, DecidedBy,
            ApprovalToken, Jti, CreatedAtUtc, DecidedAtUtc, DispatchedAtUtc);
    }

    public sealed class GrantDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string Subject { get; set; } = default!;
        public string Action { get; set; } = default!;
        public string WarehouseId { get; set; } = default!;
        public Grant.GrantState State { get; set; }
        public string GrantedBy { get; set; } = default!;
        public string? RevokedBy { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }

        public static GrantDoc From(Grant g) => new()
        {
            Id = g.Id, Subject = g.Subject, Action = g.Action, WarehouseId = g.WarehouseId,
            State = g.State, GrantedBy = g.GrantedBy, RevokedBy = g.RevokedBy,
            ExpiresAtUtc = g.ExpiresAtUtc, CreatedAtUtc = g.CreatedAtUtc,
        };

        public Grant ToDomain() => Grant.Hydrate(
            Id, Subject, Action, WarehouseId, State, GrantedBy, RevokedBy, ExpiresAtUtc, CreatedAtUtc);
    }
}
