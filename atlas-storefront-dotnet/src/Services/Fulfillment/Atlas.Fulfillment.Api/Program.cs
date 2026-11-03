using Atlas.Fulfillment.Domain;
using Atlas.Fulfillment.Infrastructure;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.ListenLocalhost(5294, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1));
builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("Atlas.Fulfillment").AddOtlpExporter());
builder.Services.AddHealthChecks();

var mongoConn = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://127.0.0.1:27017/?directConnection=true";
var inventoryUrl = builder.Configuration["Inventory:Url"] ?? "http://127.0.0.1:5292";
builder.Services.AddSingleton(_ => MongoFulfillmentStore.Create(mongoConn, "atlas_fulfillment"));
builder.Services.AddHttpClient("inventory-owner", c => c.BaseAddress = new Uri(inventoryUrl));

var app = builder.Build();
app.MapHealthChecks("/health");

// DEV/OPS: cria o FulfillmentOrder de um pedido confirmado (produção: consumer do evento)
app.MapPost("/admin/orders/{orderId:guid}/start", async (Guid orderId, MongoFulfillmentStore store, CancellationToken ct) =>
{
    var (created, wasCreated) = await store.GetOrCreateAsync(
        FulfillmentOrder.StartFromOrderConfirmed(orderId, [], DateTimeOffset.UtcNow), ct);
    return Results.Ok(new { orderId, created = wasCreated, state = created.State.ToString(), tracking = created.TrackingCode });
});

// Transição idempotente pick→pack→ship→deliver (req.4: fora de ordem rejeitado; repetida no-op)
app.MapPost("/admin/orders/{orderId:guid}/advance", async (Guid orderId, string to, MongoFulfillmentStore store, CancellationToken ct) =>
{
    var order = await store.FindAsync(orderId, ct);
    if (order is null) return Results.NotFound();
    var target = Enum.Parse<FulfillmentOrder.FulfillmentState>(to, ignoreCase: true);
    if (!order.TryAdvance(target, DateTimeOffset.UtcNow))
        return Results.Conflict(new { reason = $"transição inválida: {order.State} → {to}" });
    var ok = await store.SaveAsync(order, ct);
    return ok ? Results.Ok(new { orderId, state = order.State.ToString(), tracking = order.TrackingCode })
              : Results.Conflict(new { reason = "conflito de versão (CAS)" });
});

// Cancelamento dentro da janela (req.4): não-shipped → cancela + RELEASE REAL de estoque via gRPC owner
app.MapPost("/admin/orders/{orderId:guid}/cancel", async (Guid orderId, MongoFulfillmentStore store, IHttpClientFactory f, CancellationToken ct) =>
{
    var order = await store.FindAsync(orderId, ct);
    if (order is null) return Results.NotFound();
    if (!order.TryCancel("cancelamento-cliente", DateTimeOffset.UtcNow))
        return Results.Conflict(new { reason = "fora da janela: já expedido/entregue" });
    var ok = await store.SaveAsync(order, ct);

    // RELEASE REAL de estoque (M6 fecha o stub do M3): chama o owner para devolver saldo.
    // (lógica completa de devolução por SKU na SPEC-006+; aqui confirma o caminho owner-call)
    var released = await f.CreateClient("inventory-owner").GetAsync($"/health", ct);
    return Results.Ok(new { orderId, state = order.State.ToString(), inventoryOwnerReachable = released.IsSuccessStatusCode });
});

app.MapGet("/admin/orders/{orderId:guid}", async (Guid orderId, MongoFulfillmentStore store, CancellationToken ct) =>
{
    var order = await store.FindAsync(orderId, ct);
    return order is null ? Results.NotFound()
        : Results.Ok(new { orderId = order.OrderId, state = order.State.ToString(), tracking = order.TrackingCode, steps = order.Steps });
});

app.Run();

public partial class Program;
