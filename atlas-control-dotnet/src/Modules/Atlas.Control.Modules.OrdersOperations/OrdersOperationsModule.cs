using Atlas.Control.Modules.Contracts;
using Atlas.Control.Modules.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Results = Microsoft.AspNetCore.Http.Results;

namespace Atlas.Control.Modules.Orders;

/// <summary>Linha da grade de pedidos (leitura da view order_tracking_view).</summary>
public sealed record OrderRowDto(
    string OrderId, string CustomerSub, string Status,
    long TotalMinor, string Currency, long Version);

/// <summary>Port do Control para o Fulfillment owner (transições de entrega; HTTP+JSON).</summary>
public interface IFulfillmentOrders
{
    Task<(bool Found, string State, string Tracking, string Steps, string? Reason)> GetAsync(string orderId, CancellationToken ct);
    Task<(bool Accepted, string State, string? Reason)> StartAsync(string orderId, CancellationToken ct);
    Task<(bool Accepted, string State, string? Reason)> AdvanceAsync(string orderId, string targetState, CancellationToken ct);
    Task<(bool Accepted, string State, string? Reason)> CancelAsync(string orderId, CancellationToken ct);
}

/// <summary>
/// Pedidos no Control (ERP operacional): grade/detalhe leem a view PG
/// (ReadModel, mesmo padrão do inventory); transições chamam o Fulfillment
/// owner, que revalida tudo. Avançar/cancelar exigem role approver + auditoria.
/// </summary>
public sealed class OrdersOperationsModule(
    IFulfillmentOrders fulfillment,
    AuditChainStore audit,
    IConfiguration config) : IAtlasModule
{
    public string Name => "orders-operations";

    public void Configure(IModuleEndpoints endpoints)
    {
        endpoints.MapGet("/modules/orders-operations/status", () =>
            Results.Ok(new { module = Name }));

        // ---- grade (leitura operacional; filtro por estado) ----
        endpoints.MapGet("/modules/orders", async (string? status, CancellationToken ct) =>
        {
            var conn = config["ReadModel:ConnectionString"] ?? "Host=127.0.0.1;Username=atlas;Password=atlas_dev_only;Database=atlas_read";
            await using var connection = new NpgsqlConnection(conn);
            await connection.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT order_id, customer_sub, status, total_minor, currency, source_version FROM order_tracking_view WHERE (@st IS NULL OR status = @st) ORDER BY projected_at DESC LIMIT 100", connection);
            cmd.Parameters.Add(new NpgsqlParameter("st", NpgsqlDbType.Text) { Value = (object?)status ?? DBNull.Value });
            var items = new List<object>();
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                    items.Add(new OrderRowDto(r.GetGuid(0).ToString(), r.GetString(1), r.GetString(2),
                        r.GetInt64(3), r.GetString(4), r.GetInt64(5)));
            return Results.Ok(new { items, total = items.Count });
        });

        // ---- detalhe (view + state machine do owner) ----
        endpoints.MapGet("/modules/orders/{id}", async (string id, CancellationToken ct) =>
        {
            var conn = config["ReadModel:ConnectionString"] ?? "Host=127.0.0.1;Username=atlas;Password=atlas_dev_only;Database=atlas_read";
            await using var connection = new NpgsqlConnection(conn);
            await connection.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT order_id, customer_sub, status, total_minor, currency, source_version FROM order_tracking_view WHERE order_id = @id", connection);
            cmd.Parameters.AddWithValue("id", Guid.Parse(id));
            object? row = null;
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                if (await r.ReadAsync(ct))
                    row = new OrderRowDto(r.GetGuid(0).ToString(), r.GetString(1), r.GetString(2),
                        r.GetInt64(3), r.GetString(4), r.GetInt64(5));
            var (found, state, tracking, steps, _) = await fulfillment.GetAsync(id, ct);
            return Results.Ok(new
            {
                view = row,
                fulfillment = found ? new { state, tracking, steps } : null,
            });
        });

        // ---- iniciar fulfillment (cria a execução a partir do pedido confirmado) ----
        endpoints.MapPost("/modules/orders/{id}/start", "role:approver",
            async (HttpContext http, string id, CancellationToken ct) =>
        {
            var actor = Sub(http);
            var (accepted, state, reason) = await fulfillment.StartAsync(id, ct);
            if (!accepted)
            {
                await audit.AppendAsync(actor, "order.start", "DENIED", $"order={id} motivo={reason}", Corr(), ct);
                return Results.Conflict(new { error = reason, audited = true });
            }
            await audit.AppendAsync(actor, "order.start", "STARTED", $"order={id} state={state}", Corr(), ct);
            return Results.Ok(new { orderId = id, state });
        });

        // ---- avançar pick→pack→ship→deliver (owner revalida ordem/janela) ----
        endpoints.MapPost("/modules/orders/{id}/advance", "role:approver",
            async (HttpContext http, string id, AdvanceRequest req, CancellationToken ct) =>
        {
            var actor = Sub(http);
            var (accepted, state, reason) = await fulfillment.AdvanceAsync(id, req.To, ct);
            if (!accepted)
            {
                await audit.AppendAsync(actor, "order.advance", "DENIED", $"order={id} to={req.To} motivo={reason}", Corr(), ct);
                return Results.Conflict(new { error = reason, audited = true });
            }
            await audit.AppendAsync(actor, "order.advance", "ADVANCED", $"order={id} to={state}", Corr(), ct);
            return Results.Ok(new { orderId = id, state });
        });

        // ---- cancelar dentro da janela (owner revalida; fora dela → 409) ----
        endpoints.MapPost("/modules/orders/{id}/cancel", "role:approver",
            async (HttpContext http, string id, CancellationToken ct) =>
        {
            var actor = Sub(http);
            var (accepted, state, reason) = await fulfillment.CancelAsync(id, ct);
            if (!accepted)
            {
                await audit.AppendAsync(actor, "order.cancel", "DENIED", $"order={id} motivo={reason}", Corr(), ct);
                return Results.Conflict(new { error = reason, audited = true });
            }
            await audit.AppendAsync(actor, "order.cancel", "CANCELLED", $"order={id} state={state}", Corr(), ct);
            return Results.Ok(new { orderId = id, state });
        });
    }

    private static string Sub(HttpContext http) =>
        http.User.FindFirst("sub")?.Value
        ?? http.User.FindFirst("preferred_username")?.Value
        ?? http.User.Identity?.Name
        ?? "anonymous";

    private static string Corr() => $"m4-orders-{Guid.NewGuid():N}";

    public sealed record AdvanceRequest(string To);
}
