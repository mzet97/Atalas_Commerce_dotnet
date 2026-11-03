using Atlas.Control.Modules.Contracts;
using Atlas.Control.Modules.Approvals;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Results = Microsoft.AspNetCore.Http.Results;

namespace Atlas.Control.Modules.Payments;

/// <summary>
/// Four-eyes de REFUND (SPEC-006 req.2 / REQ-RET-001): requester registra a RMA;
/// approver DISTINTO emite approval_token HMAC {sub, order_id, amount_minor, exp}
/// validado pelo Payments owner. Self-approval → 409 auditado. Mesma convenção M2/M4.
/// </summary>
public sealed class RefundApprovalsModule : IAtlasModule
{
    public string Name => "payments-refunds";

    private readonly string _secret;
    public RefundApprovalsModule(IConfiguration config) =>
        _secret = config["Approval:TokenSecret"] ?? "atlas-dev-approval-secret";

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
    }

    public sealed record RefundTokenRequest(string RequesterSub, string OrderId, long AmountMinor, string RmaId);
}
