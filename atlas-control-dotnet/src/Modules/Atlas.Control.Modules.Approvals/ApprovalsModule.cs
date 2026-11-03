using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
namespace Atlas.Control.Modules.Approvals;

using Atlas.Control.Modules.Contracts;
using Atlas.Control.Modules.Kernel;
using Results = Microsoft.AspNetCore.Http.Results;

/// <summary>
/// Módulo de aprovações four-eyes (INV-ADMIN-001): request → decide (≠ requester) →
/// token assinado → dispatch ao owner (que REVALIDA tudo). Toda negação é auditada (403/409).
/// Rotas sob policy de role do realm (JWT Keycloak real — ADR-0007); policies no Host.
/// </summary>
public sealed class ApprovalsModule(
    ApprovalsStore store,
    AuditChainStore audit,
    IConfiguration config,
    ApprovalsModule.IOwner owner) : IAtlasModule
{
    public string Name => "approvals";

    private string Secret => config["Approval:TokenSecret"] ?? "atlas-dev-approval-secret";

    public void Configure(IModuleEndpoints endpoints)
    {
        endpoints.MapGet("/modules/approvals/status", () =>
            Results.Ok(new { module = Name, fourEyes = true }));

        endpoints.MapPost("/approvals/requests", "role:catalog_editor", async (HttpContext http, [Microsoft.AspNetCore.Mvc.FromBody] RequestApprovalRequest req, CancellationToken ct) =>
        {
            var sub = Sub(http);
            var approval = Approval.Create(sub, req.ProductId, req.ExpectedVersion, req.MarketId, req.Justification);
            await store.InsertApprovalAsync(approval, ct);
            await audit.AppendAsync(sub, "approval.request", "CREATED",
                $"product={req.ProductId} v={req.ExpectedVersion}", Corr(req), ct);
            return Results.Created($"/approvals/{approval.Id}", new { approvalId = approval.Id });
        });

        endpoints.MapPost("/approvals/{id}/decide", "role:approver", async (HttpContext http, string id, [Microsoft.AspNetCore.Mvc.FromBody] DecideRequest req, CancellationToken ct) =>
        {
            var sub = Sub(http);
            var approval = await store.GetAsync(Guid.Parse(id), ct);
            if (approval is null) return Results.NotFound();
            try
            {
                string? token = null;
                if (req.Decision == "APPROVED")
                {
                    var payload = approval.Approve(sub, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10), Secret);
                    token = ApprovalTokenIssuer.Issue(payload, Secret);
                }
                else
                {
                    approval.Reject(sub, req.Reason ?? "sem motivo");
                }
                await store.SaveAsync(approval, ct);
                await audit.AppendAsync(sub, $"approval.decide.{req.Decision}",
                    req.Decision, $"product={approval.ProductId} hash={approval.PayloadHash}", Corr(req), ct);
                return Results.Ok(new { state = approval.State.ToString(), approvalToken = token });
            }
            catch (SelfApprovalException ex)
            {
                await audit.AppendAsync(sub, "approval.decide", "DENIED_SELF_APPROVAL",
                    ex.Message, Corr(req), ct);
                return Results.Conflict(new { error = ex.Message, audited = true });
            }
        });

        endpoints.MapPost("/approvals/{id}/dispatch", "role:approver", async (HttpContext http, string id, [Microsoft.AspNetCore.Mvc.FromBody] DispatchRequest req, CancellationToken ct) =>
        {
            var sub = Sub(http);
            var approval = await store.GetAsync(Guid.Parse(id), ct);
            if (approval is null) return Results.NotFound();
            if (approval.State != ApprovalState.Approved)
            {
                await audit.AppendAsync(sub, "approval.dispatch", "DENIED_INVALID_STATE",
                    $"state={approval.State}", Corr(req), ct);
                return Results.Conflict(new { error = $"estado {approval.State} não é despachável", audited = true });
            }

            // Token em nome do APPROVER; o owner revalida assinatura/payload/estado (nunca bypass).
            var token = ApprovalTokenIssuer.Issue(
                new ApprovalTokenIssuer.Payload(approval.DecidedBy!, approval.ProductId, approval.ExpectedVersion,
                    DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()), Secret);
            var dispatch = await owner.PublishProductAsync(
                new OwnerPublishRequest(approval.ProductId, approval.ExpectedVersion, approval.MarketId, token, sub), ct);
            approval.MarkDispatched(dispatch.Accepted ? ApprovalState.Executed : ApprovalState.Failed);
            await store.SaveAsync(approval, ct);
            await audit.AppendAsync(sub, "approval.dispatch",
                dispatch.Accepted ? "EXECUTED" : "FAILED",
                $"product={approval.ProductId} reason={dispatch.Reason}", Corr(req), ct);
            return dispatch.Accepted
                ? Results.Ok(new { state = approval.State.ToString(), eventId = dispatch.EventId })
                : Results.Conflict(new { error = dispatch.Reason, audited = true });
        });
    }

    private static string Sub(HttpContext http) =>
        http.User.FindFirst("sub")?.Value ?? http.User.Identity?.Name ?? "anonymous";

    private static string Corr(IHasCorrelation req) => string.IsNullOrWhiteSpace(req.CorrelationId) ? Guid.NewGuid().ToString() : req.CorrelationId!;

    // ---- contratos do módulo ----
    public interface IHasCorrelation { string? CorrelationId { get; } }
    public sealed record RequestApprovalRequest(string ProductId, long ExpectedVersion, string MarketId, string Justification, string? CorrelationId) : IHasCorrelation;
    public sealed record DecideRequest(string Decision, string? Reason, string? CorrelationId) : IHasCorrelation;
    public sealed record DispatchRequest(string? CorrelationId) : IHasCorrelation;

    /// <summary>Port de despacho ao owner (gRPC real no Host; substituível nos testes — DIP).</summary>
    public interface IOwner
    {
        Task<OwnerPublishResponse> PublishProductAsync(OwnerPublishRequest request, CancellationToken ct);
    }

    public sealed record OwnerPublishRequest(string ProductId, long ExpectedVersion, string MarketId, string ApprovalToken, string ActorSub);
    public sealed record OwnerPublishResponse(bool Accepted, string? EventId, string? Reason);
}
