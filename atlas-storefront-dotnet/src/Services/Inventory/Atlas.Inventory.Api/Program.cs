using System.Text.Json;
using Atlas.Inventory.Api.Grpc;
using Atlas.Inventory.Application;
using MongoInventoryStore = Atlas.Inventory.Infrastructure.MongoInventoryStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry(); // traces nativos; OTLP via env
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddGrpc();

var mongoConn = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://127.0.0.1:27017/?directConnection=true";
var mongoDb = builder.Configuration["Mongo:Database"] ?? "atlas_inventory";
builder.Services.AddSingleton(_ => MongoInventoryStore.Create(mongoConn, mongoDb));
builder.Services.AddSingleton<Atlas.Inventory.Application.IInventoryStore>(sp => sp.GetRequiredService<MongoInventoryStore>());
builder.Services.AddSingleton<ReserveStockHandler>();
builder.Services.AddSingleton<InventoryOwnerGrpcService>();

builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenLocalhost(5291, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    o.ListenLocalhost(5292, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

var app = builder.Build();
app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapGrpcService<InventoryOwnerGrpcService>();
// DEV/OPS ONLY (M2/M3 smoke): ReceiveStock direto no owner — caminho de produção é Admin→Control→gRPC.
app.MapPost("/admin/positions", async (MongoInventoryStore store, JsonElement body, CancellationToken ct) =>
{
    var warehouseId = body.GetProperty("warehouseId").GetString()!;
    var skuId = body.GetProperty("skuId").GetString()!;
    var quantity = body.GetProperty("quantity").GetInt32();
    var key = Atlas.Inventory.Domain.StockKey.Create(Guid.Parse(warehouseId), skuId);
    var position = await store.LoadPositionAsync(key, ct)
                    ?? Atlas.Inventory.Domain.StockPosition.Open(key);
    position.Receive(quantity);
    await store.SavePositionAsync(key, position, ct);
    return Results.Ok(new { key = key.ToString(), onHand = position.OnHand });
});

app.MapGet("/", () => Results.Ok(new { service = "Atlas.Inventory.Api", milestone = "M2", owner = "inventory" }));

app.Run();

public partial class Program; // para Mvc.Testing
