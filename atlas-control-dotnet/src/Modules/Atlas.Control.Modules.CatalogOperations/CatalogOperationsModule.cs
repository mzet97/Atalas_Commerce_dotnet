using Atlas.Control.Modules.Contracts;
using Atlas.Control.Modules.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Results = Microsoft.AspNetCore.Http.Results;

namespace Atlas.Control.Modules.CatalogOperations;

/// <summary>Título localizado (DTO serializável — tuplas ValueTuple viram [{}] no JSON).</summary>
public sealed record CatalogTitleDto(string Locale, string Title);

/// <summary>Linha de produtos (grade do Control): rascunho vindo do owner.</summary>
public sealed record CatalogProductRow(
    string ProductId, string Status, long Version, string MarketId,
    IReadOnlyList<CatalogTitleDto> Titles, int SkuCount, string CreatedBy);

public sealed record CatalogSkuDto(string SkuId, string Code);

public sealed record CatalogProductDetail(
    string ProductId, string Status, long Version,
    IReadOnlyList<CatalogTitleDto> Titles, IReadOnlyList<CatalogSkuDto> Skus);

/// <summary>Port do Control para o Catalog owner (CRUD + unpublish; DIP: adapter no Host).</summary>
public interface ICatalogOwnerCatalog
{
    Task<(IReadOnlyList<CatalogProductRow> Items, long Total)> ListAsync(
        string? status, string? market, string? search, int page, int pageSize, CancellationToken ct);
    Task<CatalogProductDetail?> GetAsync(string productId, CancellationToken ct);
    Task<(bool Accepted, string ProductId, long Version, string? Reason)> UpsertAsync(
        string requestId, string actorSub, string marketId,
        IReadOnlyList<CatalogTitleDto> titles, IReadOnlyList<string> skuCodes, CancellationToken ct);
    Task<(bool Accepted, long Version, string? Reason)> UpdateAsync(
        string productId, long expectedVersion, string actorSub,
        IReadOnlyList<CatalogTitleDto> titles, IReadOnlyDictionary<string, string> attributes, CancellationToken ct);
    Task<(bool Accepted, string? Reason)> DeleteAsync(string productId, CancellationToken ct);
    Task<(bool Accepted, long Version, string? Reason)> SubmitAsync(
        string requestId, string actorSub, string productId, long expectedVersion, CancellationToken ct);
    Task<(bool Accepted, long Version, string? Reason, string? EventId)> UnpublishAsync(
        string requestId, string actorSub, string productId, long expectedVersion,
        string approvalToken, string reason, CancellationToken ct);
    Task<(bool Found, long Version)> GetHeadVersionAsync(string productId, CancellationToken ct);
}

/// <summary>
/// Operações de catálogo do Control (ERP master data):
/// CRUD de rascunhos direto (role catalog_editor + auditoria) e despublicação por
/// FOUR-EYES (action catalog.unpublish, escopo "catalog", token HMAC de uso único —
/// mesmo padrão do InventoryOperationsModule, sem bypass do owner).
/// Leitura (lista/detalhe) é aberta; escrita exige role; risco exige four-eyes.
/// </summary>
public sealed class CatalogOperationsModule(
    ICatalogOwnerCatalog catalog,
    OperationApprovalsStore approvals,
    AuditChainStore audit,
    IConfiguration config) : IAtlasModule
{
    public const string UnpublishAction = "catalog:unpublish"; // dois-pontos, como inventory:adjust
    public const string Scope = "catalog"; // escopo ABAC reaproveitando o slot warehouse
    public string Name => "catalog-operations";

    private string Secret => config["Approval:TokenSecret"] ?? "atlas-dev-approval-secret";
    private TimeSpan TokenTtl => TimeSpan.FromSeconds(config.GetValue("Approval:TokenTtlSeconds", 600));

    public void Configure(IModuleEndpoints endpoints)
    {
        endpoints.MapGet("/modules/catalog-operations/status", async (CancellationToken ct) =>
        {
            var (ok, version) = await catalog.GetHeadVersionAsync("00000000-0000-0000-0000-000000000000", ct);
            return Results.Ok(new { module = Name, fourEyes = true, abac = "grants", ownerReachable = ok, _ = version });
        });

        // ---- grade de produtos (leitura operacional) ----
        endpoints.MapGet("/modules/catalog/products", async (
            string? status, string? market, string? q, int? page, int? pageSize, CancellationToken ct) =>
        {
            var (items, total) = await catalog.ListAsync(status, market, q, page ?? 1, pageSize ?? 50, ct);
            return Results.Ok(new { items, total, page = page ?? 1, pageSize = pageSize ?? 50 });
        });

        endpoints.MapGet("/modules/catalog/products/{id}", async (string id, CancellationToken ct) =>
        {
            var detail = await catalog.GetAsync(id, ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        // ---- CRUD de rascunhos (direto + auditado; publicar/despublicar é four-eyes) ----
        endpoints.MapPost("/modules/catalog/products", "role:catalog_editor",
            async (HttpContext http, CatalogProductRequest req, CancellationToken ct) =>
        {
            var actor = Sub(http);
            if (req.Titles.Count == 0)
                return Results.Json(new { error = "ao menos um título é obrigatório", audited = true }, statusCode: 400);
            var (accepted, productId, version, reason) = await catalog.UpsertAsync(
                $"web-{Guid.NewGuid():N}", actor, req.MarketId ?? "BR",
                [.. req.Titles.Select(t => new CatalogTitleDto(t.Locale, t.Title))], req.SkuCodes ?? [], ct);
            if (!accepted) return Results.Json(new { error = reason, audited = true }, statusCode: 502);
            await audit.AppendAsync(actor, "catalog.draft.created", "CREATED",
                $"product={productId} market={req.MarketId}", Corr(), ct);
            return Results.Created($"/modules/catalog/products/{productId}", new { productId, version });
        });

        endpoints.MapPut("/modules/catalog/products/{id}", "role:catalog_editor",
            async (HttpContext http, string id, CatalogProductUpdate req, CancellationToken ct) =>
        {
            var actor = Sub(http);
            var (accepted, version, reason) = await catalog.UpdateAsync(
                id, req.ExpectedVersion, actor,
                [.. req.Titles.Select(t => new CatalogTitleDto(t.Locale, t.Title))],
                new Dictionary<string, string>(req.Attributes ?? new Dictionary<string, string>()), ct);
            if (!accepted)
            {
                await audit.AppendAsync(actor, "catalog.draft.update", "DENIED", $"product={id} motivo={reason}", Corr(), ct);
                var notFound = reason == "produto inexistente";
                return Results.Json(new { error = reason, audited = true }, statusCode: notFound ? 404 : 409);
            }
            await audit.AppendAsync(actor, "catalog.draft.update", "UPDATED", $"product={id} v={version}", Corr(), ct);
            return Results.Ok(new { productId = id, version });
        });

        endpoints.MapDelete("/modules/catalog/products/{id}", "role:catalog_editor",
            async (HttpContext http, string id, CancellationToken ct) =>
        {
            var actor = Sub(http);
            var (accepted, reason) = await catalog.DeleteAsync(id, ct);
            if (!accepted)
            {
                await audit.AppendAsync(actor, "catalog.draft.delete", "DENIED", $"product={id} motivo={reason}", Corr(), ct);
                var notFound = reason == "produto inexistente";
                return Results.Json(new { error = reason, audited = true }, statusCode: notFound ? 404 : 409);
            }
            await audit.AppendAsync(actor, "catalog.draft.delete", "DELETED", $"product={id}", Corr(), ct);
            return Results.Ok(new { deleted = true });
        });

        endpoints.MapPost("/modules/catalog/products/{id}/submit", "role:catalog_editor",
            async (HttpContext http, string id, SubmitDraftRequest req, CancellationToken ct) =>
        {
            var actor = Sub(http);
            var (accepted, version, reason) = await catalog.SubmitAsync(
                $"web-{Guid.NewGuid():N}", actor, id, req.ExpectedVersion, ct);
            if (!accepted)
            {
                await audit.AppendAsync(actor, "catalog.draft.submit", "DENIED", $"product={id} motivo={reason}", Corr(), ct);
                return Results.Json(new { error = reason, audited = true }, statusCode: 409);
            }
            await audit.AppendAsync(actor, "catalog.draft.submit", "SUBMITTED", $"product={id} v={version}", Corr(), ct);
            return Results.Ok(new { productId = id, version });
        });

        // ---- despublicação four-eyes: request → decide → dispatch ----
        endpoints.MapPost("/modules/catalog/unpublish/requests", "role:catalog_editor",
            async (HttpContext http, UnpublishRequest req, CancellationToken ct) =>
        {
            var requester = Sub(http);
            if (!await approvals.HasActiveGrantAsync(requester, UnpublishAction, Scope, ct))
            {
                await audit.AppendAsync(requester, "catalog.unpublish.request", "DENIED_NO_GRANT",
                    $"product={req.ProductId}", Corr(), ct);
                return Results.Json(new { error = $"sem grant ativo {UnpublishAction}", audited = true }, statusCode: 403);
            }
            if (string.IsNullOrWhiteSpace(req.Reason))
                return Results.Json(new { error = "motivo obrigatório (auditoria)", audited = true }, statusCode: 400);
            var approval = OperationApproval.Create(requester, UnpublishAction, req.ProductId, req.Reason, DateTimeOffset.UtcNow);
            await approvals.InsertApprovalAsync(approval, ct);
            await audit.AppendAsync(requester, "catalog.unpublish.request", "CREATED",
                $"approval={approval.Id} product={req.ProductId} motivo={req.Reason}", Corr(), ct);
            return Results.Created($"/modules/approvals/{approval.Id}", new { approvalId = approval.Id, state = approval.State.ToString() });
        });

        endpoints.MapPost("/modules/catalog/approvals/{id}/decide", "role:approver",
            async (HttpContext http, string id, DecideRequest req, CancellationToken ct) =>
        {
            var approver = Sub(http);
            var approval = await approvals.GetApprovalAsync(Guid.Parse(id), ct);
            if (approval is null) return Results.NotFound();
            if (approval.Action != UnpublishAction)
                return Results.Json(new { error = "aprovação não é de despublicação", audited = true }, statusCode: 409);
            try
            {
                if (!await approvals.HasActiveGrantAsync(approver, UnpublishAction, Scope, ct))
                {
                    await audit.AppendAsync(approver, "catalog.unpublish.decide", "DENIED_APPROVER_NO_GRANT", $"approval={id}", Corr(), ct);
                    return Results.Json(new { error = "approver sem grant ativo no escopo", audited = true }, statusCode: 403);
                }
                if (req.Decision == "APPROVED")
                {
                    approval.Approve(approver, DateTimeOffset.UtcNow);
                    // Token HMAC amarrado à head atual: revalidação de versão acontece no owner (CAS).
                    var (found, headVersion) = await catalog.GetHeadVersionAsync(approval.Subject, ct);
                    if (!found)
                    {
                        await audit.AppendAsync(approver, "catalog.unpublish.decide", "DENIED_NOT_FOUND", $"approval={id}", Corr(), ct);
                        return Results.Json(new { error = "produto inexistente no owner", audited = true }, statusCode: 409);
                    }
                    var payload = new ApprovalTokenIssuer.Payload(approver, approval.Subject, headVersion, DateTimeOffset.UtcNow.Add(TokenTtl).ToUnixTimeSeconds());
                    var token = ApprovalTokenIssuer.Issue(payload, Secret);
                    var jti = Guid.NewGuid().ToString("N");
                    approval.MarkTokenIssued(token, jti);
                }
                else
                {
                    approval.Reject(approver, req.Reason ?? "sem motivo", DateTimeOffset.UtcNow);
                }
                await approvals.SaveApprovalAsync(approval, ct);
                await audit.AppendAsync(approver, "catalog.unpublish.decide", req.Decision, $"approval={id} product={approval.Subject}", Corr(), ct);
                return Results.Ok(new { state = approval.State.ToString() });
            }
            catch (SelfApprovalException ex)
            {
                await audit.AppendAsync(approver, "catalog.unpublish.decide", "DENIED_SELF_APPROVAL", ex.Message, Corr(), ct);
                return Results.Conflict(new { error = ex.Message, audited = true });
            }
            catch (InvalidOperationException ex)
            {
                await audit.AppendAsync(approver, "catalog.unpublish.decide", "DENIED_INVALID_STATE", ex.Message, Corr(), ct);
                return Results.Conflict(new { error = ex.Message, audited = true });
            }
        });

        endpoints.MapPost("/modules/catalog/approvals/{id}/dispatch", "role:approver",
            async (HttpContext http, string id, CancellationToken ct) =>
        {
            var dispatcher = Sub(http);
            var approval = await approvals.GetApprovalAsync(Guid.Parse(id), ct);
            if (approval is null) return Results.NotFound();
            if (approval.State != OperationApprovalState.Approved || approval.ApprovalToken is null || approval.Jti is null)
            {
                await audit.AppendAsync(dispatcher, "catalog.unpublish.dispatch", "DENIED_INVALID_STATE",
                    $"approval={id} state={approval.State}", Corr(), ct);
                return Results.Conflict(new { error = $"estado {approval.State} não é despachável", audited = true });
            }
            if (!await approvals.HasActiveGrantAsync(approval.RequestedBy, UnpublishAction, Scope, ct)
                || !await approvals.HasActiveGrantAsync(approval.DecidedBy!, UnpublishAction, Scope, ct))
            {
                await audit.AppendAsync(dispatcher, "catalog.unpublish.dispatch", "DENIED_GRANT_REVOKED", $"approval={id}", Corr(), ct);
                return Results.Json(new { error = "grant revogado/expirado desde a aprovação", audited = true }, statusCode: 403);
            }
            if (!await approvals.TryBurnTokenAsync(approval.Jti, id, ct))
            {
                await audit.AppendAsync(dispatcher, "catalog.unpublish.dispatch", "DENIED_TOKEN_REPLAY", $"approval={id} jti={approval.Jti}", Corr(), ct);
                return Results.Conflict(new { error = "token já utilizado (replay)", audited = true });
            }
            var (found, headVersion) = await catalog.GetHeadVersionAsync(approval.Subject, ct);
            if (!found)
            {
                await audit.AppendAsync(dispatcher, "catalog.unpublish.dispatch", "DENIED_NOT_FOUND", $"approval={id}", Corr(), ct);
                return Results.Conflict(new { error = "produto inexistente no owner", audited = true });
            }
            var (ok, version, reason, eventId) = await catalog.UnpublishAsync(
                $"web-{Guid.NewGuid():N}", approval.DecidedBy!, approval.Subject, headVersion,
                approval.ApprovalToken, approval.Justification, ct);
            approval.MarkDispatched(DateTimeOffset.UtcNow);
            approval.MarkFinal(ok ? OperationApprovalState.Executed : OperationApprovalState.Failed, DateTimeOffset.UtcNow);
            await approvals.SaveApprovalAsync(approval, ct);
            await audit.AppendAsync(dispatcher, "catalog.unpublish.dispatch", ok ? "EXECUTED" : "FAILED",
                $"approval={id} product={approval.Subject} version={version} erro={reason} event={eventId}", Corr(), ct);
            return ok
                ? Results.Ok(new { state = "Executed", positionVersion = version, eventId })
                : Results.Json(new { state = "Failed", error = reason, audited = true }, statusCode: 502);
        });
    }

    private static string Sub(HttpContext http) =>
        http.User.FindFirst("sub")?.Value
        ?? http.User.FindFirst("preferred_username")?.Value
        ?? http.User.Identity?.Name
        ?? "anonymous";

    private static string Corr() => $"m4-catalog-{Guid.NewGuid():N}";

    public sealed record CatalogTitle(string Locale, string Title);
    public sealed record CatalogProductRequest(string? MarketId, List<CatalogTitle> Titles, List<string>? SkuCodes);
    public sealed record CatalogProductUpdate(long ExpectedVersion, List<CatalogTitle> Titles, Dictionary<string, string>? Attributes);
    public sealed record SubmitDraftRequest(long ExpectedVersion);
    public sealed record UnpublishRequest(string ProductId, string Reason);
    public sealed record DecideRequest(string Decision, string? Reason);
}
