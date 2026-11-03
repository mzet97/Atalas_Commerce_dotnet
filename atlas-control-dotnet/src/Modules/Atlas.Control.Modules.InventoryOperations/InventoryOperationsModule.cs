using Atlas.Control.Modules.Contracts;
using Atlas.Control.Modules.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Results = Microsoft.AspNetCore.Http.Results;

namespace Atlas.Control.Modules.InventoryOperations;

/// <summary>
/// Operações de risco de estoque via FOUR-EYES completo (SPEC-004, INV-ADMIN-001/INV-SEC-001):
/// request (grant do operador) → approve (grant do approver + ≠ requester) → dispatch
/// (grant AINDA ativo — revogação reflete aqui + nonce uso-único) → owner REVALIDA o token
/// (assinatura/exp/action/subject) e aplica com CAS. TUDO auditado na cadeia de hash.
/// </summary>
/// <summary>Dispatcher gRPC ao Inventory owner (implementado no Host; DIP).</summary>
public interface IInventoryStockOwner
{
    Task<(bool Ok, long PositionVersion, string Error)> AdjustWithApprovalAsync(
        string requestId, string warehouseId, string skuId, int delta, string reason,
        string actorSub, string approvalToken, string correlationId, CancellationToken ct);
    Task<(bool Found, long Version)> GetHeadVersionAsync(string warehouseId, string skuId, CancellationToken ct);
}

public sealed class InventoryOperationsModule(
    OperationApprovalsStore approvals,
    AuditChainStore audit,
    IInventoryStockOwner stockOwner,
    OperationsOverview ops,
    IConfiguration config) : IAtlasModule
{
    public const string GrantAction = "inventory:adjust";
    public const string TokenAction = "inventory.adjust"; // convenção do token v2 (StockApprovalToken)

    public string Name => "inventory-operations";

    private string Secret => config["Approval:TokenSecret"] ?? "atlas-dev-approval-secret";
    private TimeSpan TokenTtl => TimeSpan.FromSeconds(config.GetValue("Approval:TokenTtlSeconds", 90));

    public void Configure(IModuleEndpoints endpoints)
    {
        endpoints.MapGet("/modules/inventory-operations/status", () =>
            Results.Ok(new { module = Name, fourEyes = true, abac = "grants", audit = "hash-chain" }));

        // ---- grants (ABAC dinâmico) ----
        endpoints.MapPost("/modules/iam/grants", "role:iam_admin", async (HttpContext http, IamGrantRequest req, CancellationToken ct) =>
        {
            var actor = Sub(http);
            var grant = Grant.Create(req.Subject, req.Action, req.WarehouseId,
                TimeSpan.FromMinutes(req.TtlMinutes), actor, DateTimeOffset.UtcNow);
            await approvals.InsertGrantAsync(grant, ct);
            await audit.AppendAsync(actor, "iam.grant.created", "CREATED",
                $"subject={req.Subject} action={req.Action} wh={req.WarehouseId} ttl={req.TtlMinutes}m", Corr(), ct);
            return Results.Created($"/modules/iam/grants/{grant.Id}", new { grantId = grant.Id, expiresAtUtc = grant.ExpiresAtUtc });
        });

        endpoints.MapDelete("/modules/iam/grants/{grantId}", "role:iam_admin", async (HttpContext http, string grantId, CancellationToken ct) =>
        {
            var actor = Sub(http);
            var revoked = await approvals.RevokeGrantAsync(Guid.Parse(grantId), actor, ct);
            await audit.AppendAsync(actor, "iam.grant.revoke", revoked ? "REVOKED" : "NOT_FOUND",
                $"grant={grantId}", Corr(), ct);
            return revoked ? Results.Ok(new { revoked = true }) : Results.NotFound(new { revoked = false });
        });

        endpoints.MapGet("/modules/iam/grants", async (string? subject, CancellationToken ct) =>
            Results.Ok(new { grants = await approvals.ListGrantsAsync(subject, ct) }));

        // ---- four-eyes: request → approve → dispatch ----
        endpoints.MapPost("/modules/inventory-operations/adjust/requests", "role:inventory_operator",
            async (HttpContext http, AdjustRequest req, CancellationToken ct) =>
        {
            var requester = Sub(http);
            if (!await approvals.HasActiveGrantAsync(requester, GrantAction, req.WarehouseId, ct))
            {
                await audit.AppendAsync(requester, "inventory.adjust.request", "DENIED_NO_GRANT",
                    $"wh={req.WarehouseId} sku={req.SkuId} delta={req.Delta}", Corr(), ct);
                return Results.Json(new { error = $"sem grant ativo {GrantAction} no warehouse {req.WarehouseId}", audited = true },
                    statusCode: 403);
            }
            var approval = OperationApproval.Create(requester, TokenAction,
                StockSubject(req.WarehouseId, req.SkuId), req.Reason, DateTimeOffset.UtcNow);
            await approvals.InsertApprovalAsync(approval, ct);
            await audit.AppendAsync(requester, "inventory.adjust.request", "CREATED",
                $"approval={approval.Id} wh={req.WarehouseId} sku={req.SkuId} delta={req.Delta} motivo={req.Reason}", Corr(), ct);
            return Results.Created($"/modules/approvals/{approval.Id}", new { approvalId = approval.Id, state = approval.State.ToString() });
        });

        endpoints.MapPost("/modules/approvals/{id}/decide", "role:approver", async (HttpContext http, string id, DecideRequest req, CancellationToken ct) =>
        {
            var approver = Sub(http);
            var approval = await approvals.GetApprovalAsync(Guid.Parse(id), ct);
            if (approval is null) return Results.NotFound();
            try
            {
                if (req.Decision == "APPROVED")
                {
                    // four-eyes: ≠ requester (409 auditado); approver também precisa de grant ativo
                    if (!await approvals.HasActiveGrantAsync(approver, GrantAction, WarehouseOf(approval.Subject), ct))
                    {
                        await audit.AppendAsync(approver, "inventory.adjust.decide", "DENIED_APPROVER_NO_GRANT",
                            $"approval={id}", Corr(), ct);
                        return Results.Json(new { error = "approver sem grant ativo no escopo", audited = true }, statusCode: 403);
                    }
                    approval.Approve(approver, DateTimeOffset.UtcNow);
                    var token = StockTokenIssuer.Issue(approver, approval.Action, approval.Subject,
                        TokenTtl, Secret, DateTimeOffset.UtcNow, out var jti);
                    approval.MarkTokenIssued(token, jti);
                }
                else
                {
                    approval.Reject(approver, req.Reason ?? "sem motivo", DateTimeOffset.UtcNow);
                }
                await approvals.SaveApprovalAsync(approval, ct);
                await audit.AppendAsync(approver, "inventory.adjust.decide", req.Decision,
                    $"approval={id} subject={approval.Subject}", Corr(), ct);
                return Results.Ok(new { state = approval.State.ToString(), approvalToken = req.Decision == "APPROVED" ? approval.ApprovalToken : null });
            }
            catch (SelfApprovalException ex)
            {
                await audit.AppendAsync(approver, "inventory.adjust.decide", "DENIED_SELF_APPROVAL", ex.Message, Corr(), ct);
                return Results.Conflict(new { error = ex.Message, audited = true });
            }
            catch (InvalidOperationException ex)
            {
                await audit.AppendAsync(approver, "inventory.adjust.decide", "DENIED_INVALID_STATE", ex.Message, Corr(), ct);
                return Results.Conflict(new { error = ex.Message, audited = true });
            }
        });

        endpoints.MapPost("/modules/approvals/{id}/dispatch", "role:approver", async (HttpContext http, string id, DispatchRequest req, CancellationToken ct) =>
        {
            var dispatcher = Sub(http);
            var approval = await approvals.GetApprovalAsync(Guid.Parse(id), ct);
            if (approval is null) return Results.NotFound();
            var (wh, sku) = SplitSubject(approval.Subject);
            var correlation = Corr();

            if (approval.State != OperationApprovalState.Approved || approval.ApprovalToken is null || approval.Jti is null)
            {
                await audit.AppendAsync(dispatcher, "inventory.adjust.dispatch", "DENIED_INVALID_STATE",
                    $"approval={id} state={approval.State}", correlation, ct);
                return Results.Conflict(new { error = $"estado {approval.State} não é despachável", audited = true });
            }

            // REVOGAÇÃO REFLETE NA PRÓXIMA CHAMADA: grant revalidado NO dispatch (req.2)
            if (!await approvals.HasActiveGrantAsync(approval.RequestedBy, GrantAction, wh, ct)
                || !await approvals.HasActiveGrantAsync(approval.DecidedBy!, GrantAction, wh, ct))
            {
                await audit.AppendAsync(dispatcher, "inventory.adjust.dispatch", "DENIED_GRANT_REVOKED",
                    $"approval={id} wh={wh}", correlation, ct);
                return Results.Json(new { error = "grant revogado/expirado desde a aprovação", audited = true }, statusCode: 403);
            }

            // token de USO ÚNICO: nonce queimado antes do despacho (replay → negado auditado)
            if (!await approvals.TryBurnTokenAsync(approval.Jti, id, ct))
            {
                await audit.AppendAsync(dispatcher, "inventory.adjust.dispatch", "DENIED_TOKEN_REPLAY",
                    $"approval={id} jti={approval.Jti}", correlation, ct);
                return Results.Conflict(new { error = "token já utilizado (replay)", audited = true });
            }

            approval.MarkDispatched(DateTimeOffset.UtcNow);
            await approvals.SaveApprovalAsync(approval, ct);
            var (ok, version, error) = await stockOwner.AdjustWithApprovalAsync(
                req.RequestId, wh, sku, req.Delta, req.Reason ?? approval.Justification,
                approval.DecidedBy!, approval.ApprovalToken, correlation, ct);
            approval.MarkFinal(ok ? OperationApprovalState.Executed : OperationApprovalState.Failed, DateTimeOffset.UtcNow);
            await approvals.SaveApprovalAsync(approval, ct);
            await audit.AppendAsync(dispatcher, "inventory.adjust.dispatch", ok ? "EXECUTED" : "FAILED",
                $"approval={id} wh={wh} sku={sku} delta={req.Delta} version={version} erro={error}", correlation, ct);
            return ok
                ? Results.Ok(new { state = "Executed", positionVersion = version })
                : Results.Json(new { state = "Failed", error, audited = true }, statusCode: 502);
        });

        endpoints.MapGet("/modules/approvals", async (string? state, int? page, int? pageSize, CancellationToken ct) =>
        {
            var (items, total) = await approvals.ListApprovalsAsync(state, page ?? 1, pageSize ?? 50, ct);
            return Results.Ok(new { items, total, page = page ?? 1, pageSize = Math.Clamp(pageSize ?? 50, 1, 200) });
        });

        endpoints.MapGet("/modules/approvals/{id}", async (string id, CancellationToken ct) =>
        {
            var approval = await approvals.GetApprovalAsync(Guid.Parse(id), ct);
            return approval is null
                ? Results.NotFound()
                : Results.Ok(new { approvalId = approval.Id, state = approval.State.ToString(), approval.RequestedBy, approval.DecidedBy, approval.Action, approval.Subject });
        });

        // ---- auditoria encadeada (REQ-ADM-003) — consulta server-side (req.5) + export ----
        endpoints.MapGet("/modules/audit", async (string? action, string? decision, int? page, int? pageSize, CancellationToken ct) =>
        {
            var (items, total) = await audit.QueryAsync(action, decision, page ?? 1, pageSize ?? 50, ct);
            return Results.Ok(new { items, total, page = page ?? 1, pageSize = Math.Clamp(pageSize ?? 50, 1, 200) });
        });

        endpoints.MapGet("/modules/audit/export", async (string? format, string? action, CancellationToken ct) =>
        {
            var (items, total) = await audit.QueryAsync(action, null, 1, 200, ct); // export até 200 (job assíncrono p/ volumes maiores na SPEC-004+)
            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("seq,occurredAtUtc,actorSub,action,decision,correlationId,prevHash,hash");
                foreach (var e in items)
                    csv.Append(System.Globalization.CultureInfo.InvariantCulture,
                        $"\"{e.Seq}\",\"{e.OccurredAtUtc:O}\",\"{e.ActorSub}\",\"{e.Action}\",\"{e.Decision}\",\"{e.CorrelationId}\",\"{e.PrevHash}\",\"{e.Hash}\"\n");
                return Results.File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv",
                    $"atlas-audit-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
            }
            return Results.Ok(new { total, entries = items, exportedAtUtc = DateTimeOffset.UtcNow });
        });

        // ---- visão operacional read-only (req.6) ----
        endpoints.MapGet("/modules/ops/overview", async (CancellationToken ct) => await ops.SnapshotAsync(ct));

        endpoints.MapGet("/modules/audit/verify", async (CancellationToken ct) =>
        {
            var (valid, count, brokenAt, reason) = await audit.VerifyAsync(ct);
            return Results.Ok(new { valid, count, brokenAtSeq = brokenAt, reason });
        });

        // ---- lag CQRS visível (SPEC-004 req.4): head do OWNER vs source_version projetada ----
        endpoints.MapGet("/modules/inventory-operations/positions", async (string? skuContains, int? page, int? pageSize, CancellationToken ct) =>
        {
            var conn = config["ReadModel:ConnectionString"] ?? "Host=127.0.0.1;Username=atlas;Password=atlas_dev_only;Database=atlas_read";
            await using var connection = new NpgsqlConnection(conn);
            await connection.OpenAsync(ct);
            var pageSizeEff = Math.Clamp(pageSize ?? 50, 1, 200);
            await using var countCmd = new NpgsqlCommand(
                "SELECT count(*) FROM inventory_admin_view WHERE (@sku IS NULL OR sku_id LIKE '%' || @sku || '%')", connection);
            countCmd.Parameters.AddWithValue("sku", (object?)skuContains ?? DBNull.Value);
            var total = (long)(await countCmd.ExecuteScalarAsync(ct))!;
            await using var cmd = new NpgsqlCommand(
                "SELECT position_key, sku_id, on_hand, reserved, source_version, projected_at FROM inventory_admin_view " +
                "WHERE (@sku IS NULL OR sku_id LIKE '%' || @sku || '%') ORDER BY projected_at DESC LIMIT @limit OFFSET @offset", connection);
            cmd.Parameters.AddWithValue("sku", (object?)skuContains ?? DBNull.Value);
            cmd.Parameters.AddWithValue("limit", pageSizeEff);
            cmd.Parameters.AddWithValue("offset", (Math.Max(page ?? 1, 1) - 1) * pageSizeEff);
            var rows = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                rows.Add(new
                {
                    positionKey = reader.GetString(0), skuId = reader.GetString(1),
                    onHand = reader.GetInt32(2), reserved = reader.GetInt32(3),
                    sourceVersion = reader.GetInt64(4), projectedAtUtc = reader.GetFieldValue<DateTimeOffset>(5),
                });
            return Results.Ok(new { items = rows, total, page = page ?? 1, pageSize = pageSizeEff });
        });

        endpoints.MapGet("/modules/inventory-operations/cqrs-lag", async (string warehouseId, string skuId, CancellationToken ct) =>
        {
            var (found, ownerVersion) = await stockOwner.GetHeadVersionAsync(warehouseId, skuId, ct);
            var projected = await ReadProjectionAsync(warehouseId, skuId, ct);
            return Results.Ok(new
            {
                warehouseId,
                skuId,
                ownerHeadVersion = found ? ownerVersion : (long?)null,
                projectedSourceVersion = projected?.Item1,
                projectedAtUtc = projected?.Item2,
                projectedAgeSeconds = projected is null ? null : (long?)(DateTimeOffset.UtcNow - projected.Value.Item2).TotalSeconds,
                lag = projected is null || !found ? null : (long?)(ownerVersion - projected.Value.Item1),
                note = "lag > 0 = leitura defasada; decisões críticas SEMPRE revalidam no owner (INV-CQRS-001)",
            });
        });
    }

    private static string StockSubject(string warehouseId, string skuId) => $"{warehouseId}:{skuId}";

    private static (string Wh, string Sku) SplitSubject(string subject)
    {
        var idx = subject.IndexOf(':');
        return idx < 0 ? (subject, "") : (subject[..idx], subject[(idx + 1)..]);
    }

    private static string WarehouseOf(string subject) => SplitSubject(subject).Wh;

    /// <summary>Leitura READ-ONLY da view projetada (PG read model — ADR-0002; nunca write model do owner).</summary>
    private async Task<(long sourceVersion, DateTimeOffset projectedAt)?> ReadProjectionAsync(
        string warehouseId, string skuId, CancellationToken ct)
    {
        var conn = config["ReadModel:ConnectionString"] ?? "Host=127.0.0.1;Username=atlas;Password=atlas_dev_only;Database=atlas_read";
        await using var connection = new NpgsqlConnection(conn);
        await connection.OpenAsync(ct);
        var positionKey = $"{warehouseId.Replace("-", "")}:{skuId}";
        await using var command = new NpgsqlCommand(
            "SELECT source_version, projected_at FROM inventory_admin_view WHERE position_key = @key LIMIT 1",
            connection);
        command.Parameters.AddWithValue("key", positionKey);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (reader.GetInt64(0), reader.GetFieldValue<DateTimeOffset>(1));
    }

    private static string Sub(HttpContext http) =>
        http.User.FindFirst("sub")?.Value
        ?? http.User.FindFirst("preferred_username")?.Value
        ?? http.User.Identity?.Name
        ?? "anonymous";

    private static string Corr() => $"m4-{Guid.NewGuid():N}";

    public sealed record IamGrantRequest(string Subject, string Action, string WarehouseId, int TtlMinutes);
    public sealed record AdjustRequest(string WarehouseId, string SkuId, int Delta, string Reason);
    public sealed record DecideRequest(string Decision, string? Reason);
    public sealed record DispatchRequest(string RequestId, int Delta, string? Reason);
}
