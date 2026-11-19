using Atlas.Control.Modules.Contracts;
using Atlas.Control.Modules.Approvals;
using Atlas.Control.Modules.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Results = Microsoft.AspNetCore.Http.Results;

namespace Atlas.Control.Modules.Payments;

/// <summary>
/// Four-eyes de REFUND (SPEC-006 req.2 / REQ-RET-001): requester registra a RMA;
/// approver DISTINTO emite approval_token HMAC {sub, order_id, amount_minor, exp}
/// validado pelo Payments owner. Self-approval → 409 auditado. Mesma convenção M2/M4.
/// </summary>
/// <summary>Port do Control para consulta read-only de intents (diagnóstico/auditoria).</summary>
public interface IPaymentsIntents
{
    Task<object?> GetByOrderAsync(string orderId, CancellationToken ct);
}

public sealed class RefundApprovalsModule(
    IConfiguration config,
    AuditChainStore audit,
    IPaymentsIntents? intents = null) : IAtlasModule
{
    public string Name => "payments-refunds";

    private readonly string _secret = config["Approval:TokenSecret"] ?? "atlas-dev-approval-secret";
    private readonly AuditChainStore _audit = audit;
    private readonly IPaymentsIntents? _intents = intents;

    public void Configure(IModuleEndpoints endpoints)
    {
        endpoints.MapGet("/modules/payments-refunds/status", () =>
            Results.Ok(new { module = Name, fourEyes = true }));

        endpoints.MapPost("/modules/payments-refunds/token", "role:approver",
            async (HttpContext http, RefundTokenRequest req, CancellationToken ct) =>
            {
                var approver = http.User.FindFirst("sub")?.Value
                               ?? http.User.FindFirst("preferred_username")?.Value ?? "approver";
                if (string.Equals(approver, req.RequesterSub, StringComparison.Ordinal))
                    return Results.Conflict(new { error = "four-eyes violado: approver = requester", audited = true });

                var payload = new RefundApprovalToken.Payload(
                    Sub: approver, OrderId: req.OrderId, AmountMinor: req.AmountMinor,
                    ExpUnixSeconds: DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());
                return Results.Ok(new { approvalToken = RefundApprovalToken.Issue(payload, _secret), approver });
            });

        // Diagnóstico read-only: intent + webhooks do pedido (sem token, sem mutação).
        endpoints.MapGet("/modules/payments-refunds/intents", async (HttpContext http, string orderId, CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("sub")?.Value ?? http.User.FindFirst("preferred_username")?.Value ?? "anonymous";
            if (_intents is null)
            {
                await _audit.AppendAsync(actor, "refund.intent.lookup", "NO_OWNER", $"order={orderId}", Corr(), ct);
                return Results.Json(new { error = "intents indisponível (sem owner)", audited = true }, statusCode: 502);
            }
            var intent = await _intents.GetByOrderAsync(orderId, ct);
            if (intent is null)
            {
                await _audit.AppendAsync(actor, "refund.intent.lookup", "NOT_FOUND", $"order={orderId}", Corr(), ct);
                return Results.NotFound(new { error = "sem intent para este pedido", audited = true });
            }
            await _audit.AppendAsync(actor, "refund.intent.lookup", "FOUND", $"order={orderId}", Corr(), ct);
            return Results.Ok(intent);
        });
    }

    private static string Corr() => $"m4-payments-{Guid.NewGuid():N}";

    public sealed record RefundTokenRequest(string RequesterSub, string OrderId, long AmountMinor, string RmaId);
}
