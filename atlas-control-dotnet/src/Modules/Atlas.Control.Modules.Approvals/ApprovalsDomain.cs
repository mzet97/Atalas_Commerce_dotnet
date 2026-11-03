namespace Atlas.Control.Modules.Approvals;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Agregado Approval (blueprint #02 §2; INV-ADMIN-001): request de um operador e decisão
/// de OUTRO (four-eyes), com payload hash e emissão do approval_token assinado para o owner.
/// </summary>
public sealed class Approval
{
    public Guid Id { get; }
    public string RequestedBy { get; }
    public string ProductId { get; }
    public long ExpectedVersion { get; }
    public string MarketId { get; }
    public string Justification { get; }
    public string PayloadHash { get; }
    public ApprovalState State { get; private set; }
    public string? DecidedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }

    private Approval(Guid id, string requestedBy, string productId, long expectedVersion,
        string marketId, string justification, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(requestedBy)) throw new ArgumentException("requested_by obrigatório");
        if (string.IsNullOrWhiteSpace(justification)) throw new ArgumentException("justificativa obrigatória (INV-ADMIN-001)");
        Id = id;
        RequestedBy = requestedBy;
        ProductId = productId;
        ExpectedVersion = expectedVersion;
        MarketId = marketId;
        Justification = justification;
        CreatedAtUtc = createdAtUtc;
        PayloadHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{productId}|{expectedVersion}|{marketId}"))).ToLowerInvariant();
    }

    public static Approval Create(string requestedBy, string productId, long expectedVersion,
        string marketId, string justification) =>
        new(Guid.NewGuid(), requestedBy, productId, expectedVersion, marketId, justification, DateTimeOffset.UtcNow);

    public static Approval Hydrate(Guid id, string requestedBy, string productId, long expectedVersion,
        string marketId, string justification, ApprovalState state, string? decidedBy,
        string? rejectionReason, DateTimeOffset createdAtUtc, DateTimeOffset? decidedAtUtc) =>
        new(id, requestedBy, productId, expectedVersion, marketId, justification, createdAtUtc)
        {
            State = state, DecidedBy = decidedBy, RejectionReason = rejectionReason, DecidedAtUtc = decidedAtUtc,
        };

    /// <summary>Decide a aprovação. Self-approval é VIOLAÇÃO registrada, não fluxo válido.</summary>
    public ApprovalTokenIssuer.Payload Approve(string approverSub, DateTimeOffset nowUtc, TimeSpan ttl, string tokenSecret)
    {
        if (State != ApprovalState.Requested)
            throw new InvalidOperationException($"Transição inválida: {State} → Approved");
        if (string.Equals(approverSub, RequestedBy, StringComparison.Ordinal))
            throw new SelfApprovalException(RequestedBy);
        DecidedBy = approverSub;
        DecidedAtUtc = nowUtc;
        State = ApprovalState.Approved;
        return new ApprovalTokenIssuer.Payload(approverSub, ProductId, ExpectedVersion, nowUtc.Add(ttl).ToUnixTimeSeconds());
    }

    public void Reject(string approverSub, string reason)
    {
        if (State != ApprovalState.Requested) throw new InvalidOperationException($"Transição inválida: {State} → Rejected");
        if (string.Equals(approverSub, RequestedBy, StringComparison.Ordinal)) throw new SelfApprovalException(RequestedBy);
        DecidedBy = approverSub;
        RejectionReason = reason;
        DecidedAtUtc = DateTimeOffset.UtcNow;
        State = ApprovalState.Rejected;
    }

    /// <summary>Marca o resultado do despacho ao owner (Executed/Failed).</summary>
    public void MarkDispatched(ApprovalState finalState)
    {
        if (State != ApprovalState.Approved) throw new InvalidOperationException($"Transição inválida: {State} → {finalState}");
        State = finalState;
    }
}

public enum ApprovalState { Requested, Approved, Rejected, Dispatched, Executed, Failed }

public sealed class SelfApprovalException(string sub) : InvalidOperationException($"four-eyes violado: {sub} não pode aprovar a própria solicitação");

/// <summary>
/// Emissor HMAC do approval_token — MESMO algoritmo validado pelo owner (Atlas.Catalog.Application
/// ApprovalToken). Duplicado de propósito enquanto não houver pacote de contratos publicado;
/// consolidar no release (ADR-0012). O teste de compatibilidade cruzada garante os dois lados.
/// </summary>
public static class ApprovalTokenIssuer
{
    public sealed record Payload(
        [property: System.Text.Json.Serialization.JsonPropertyName("sub")] string Sub,
        [property: System.Text.Json.Serialization.JsonPropertyName("product_id")] string ProductId,
        [property: System.Text.Json.Serialization.JsonPropertyName("expected_version")] long ExpectedVersion,
        [property: System.Text.Json.Serialization.JsonPropertyName("exp")] long ExpUnixSeconds);

    public static string Issue(Payload payload, string secret)
    {
        var json = JsonSerializer.Serialize(payload);
        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=');
        var sig = Convert.ToBase64String(Sign(body, secret)).TrimEnd('=');
        return $"{body}.{sig}";
    }

    private static byte[] Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
    }
}

/// <summary>Trilha de auditoria append-only (REQ-ADM-003): toda ação E toda negação.</summary>
public sealed record AuditRecord(
    Guid Id, string ActorSub, string Action, string Decision, string? Detail,
    string CorrelationId, DateTimeOffset OccurredAtUtc)
{
    public static AuditRecord Create(string actorSub, string action, string decision, string? detail, string correlationId) =>
        new(Guid.NewGuid(), actorSub, action, decision, detail, correlationId, DateTimeOffset.UtcNow);
}
