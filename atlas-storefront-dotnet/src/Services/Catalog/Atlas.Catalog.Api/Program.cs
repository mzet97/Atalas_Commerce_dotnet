using OpenTelemetry.Trace;
using OpenTelemetry;
using Atlas.Catalog.Application;
using Atlas.Catalog.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

// Telemetria: OTel com exportação OTLP condicionada ao endpoint (padrão OTEL_EXPORTER_OTLP_ENDPOINT).
// Em M1 o collector é opcional: sem endpoint, o serviço segue funcional com traces locais.
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Atlas.Catalog", "Microsoft.AspNetCore").AddOtlpExporter()); // endpoint via OTEL_EXPORTER_OTLP_ENDPOINT

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);

// Owner do Catalog (M2): Mongo configurável para isolamento de testes
var mongoConn = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://127.0.0.1:27017/?directConnection=true";
var mongoDbName = builder.Configuration["Mongo:Database"] ?? "atlas_catalog";
builder.Services.AddSingleton<Atlas.Catalog.Infrastructure.MongoCatalogStore>(_ =>
    Atlas.Catalog.Infrastructure.MongoCatalogStore.Create(mongoConn, mongoDbName));
builder.Services.AddSingleton<Atlas.Catalog.Application.ICatalogStore>(sp => sp.GetRequiredService<Atlas.Catalog.Infrastructure.MongoCatalogStore>());
builder.Services.AddSingleton<UpsertDraftHandler>();
builder.Services.AddSingleton<PublishProductHandler>();
builder.Services.AddGrpc();

// gRPC local exige h2c (HTTP/2 cleartext): 5290 para o owner gRPC; 5299 HTTP/1.1 para o resto.
// (smoke M2: Kestrel cleartext default é HTTP/1.1 e o channel gRPC pendura; Listen manual vence --urls)
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenLocalhost(5299, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    o.ListenLocalhost(5290, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

var app = builder.Build();

app.MapOpenApi();
app.MapGrpcService<Atlas.Catalog.Api.Grpc.CatalogOwnerGrpcService>();

app.MapGet("/", () => Results.Ok(new
{
    service = "Atlas.Catalog.Api",
    milestone = "M1",
    status = "foundation",
}));

// Health detalhado para smoke local/CI (TEST-001-002).
app.MapHealthChecks("/health");

// Endpoint de demonstração do domínio mínimo (estrutura presente; lógica real chega na SPEC-002).
app.MapGet("/catalog/drafts/{id:guid}", (Guid id) =>
{
    var product = ProductDraft.Create(new ProductId(id)).WithTitle("pt-BR", "Rascunho de demonstração");
    return Results.Ok(new
    {
        id = product.Id.Value,
        status = product.Status.ToString(),
        titles = product.Titles,
    });
});

// DEV/OPS ONLY (M2 smoke; M5 aceita corpo opcional p/ seed multi-market): cria+submete
// rascunho direto no owner — o caminho de produção é Admin→Control→gRPC (four-eyes).
app.MapPost("/admin/drafts", async (UpsertDraftHandler handler, HttpContext http, CancellationToken ct) =>
{
    var (marketId, titles, skuCodes) = http.Request.HasJsonContentType()
        ? await ParseSeedBodyAsync(http, ct)
        : ("BR", new List<(string, string)> { ("pt-BR", "Produto Smoke M2") }, new List<string> { "SMOKE-M2-1" });
    var requestId = $"dev-{Guid.NewGuid():N}";
    var result = await handler.HandleAsync(new UpsertDraftCommand(
        RequestId: requestId, ActorSub: "dev-seed", MarketId: marketId,
        Titles: titles, SkuCodes: skuCodes), ct);
    var store = app.Services.GetRequiredService<Atlas.Catalog.Infrastructure.MongoCatalogStore>();
    var submitted = await store.SubmitForApprovalAsync(result.ProductId, result.Version, ct);
    return Results.Ok(new { productId = result.ProductId.Value, version = submitted.Version, marketId, skuCodes });
});

// DEV/OPS ONLY (M5 seed): publica o rascunho submetido (outbox versionado na MESMA transação).
app.MapPost("/admin/drafts/{id:guid}/publish", async (Guid id, Atlas.Catalog.Infrastructure.MongoCatalogStore store, CancellationToken ct) =>
{
    var marketId = await store.GetMarketIdAsync(new Atlas.Catalog.Domain.ProductId(id), ct);
    if (marketId is null) return Results.NotFound(new { error = "rascunho inexistente" });
    var outcome = await store.PublishInTransactionAsync(
        new Atlas.Catalog.Domain.ProductId(id), 2, "dev-seed-approval", marketId,
        $"dev-publish-{Guid.NewGuid():N}", ct);
    return outcome.Accepted
        ? Results.Ok(new { productId = id, version = outcome.Version })
        : Results.Conflict(new { reason = outcome.Reason });
});

static async Task<(string, List<(string, string)>, List<string>)> ParseSeedBodyAsync(HttpContext http, CancellationToken ct)
{
    var body = await http.Request.ReadFromJsonAsync<System.Text.Json.JsonElement>(ct);
    var market = body.TryGetProperty("marketId", out var m) ? m.GetString() ?? "BR" : "BR";
    var titles = new List<(string, string)>();
    if (body.TryGetProperty("titles", out var ts) && ts.ValueKind == System.Text.Json.JsonValueKind.Array)
        foreach (var t in ts.EnumerateArray())
            titles.Add((t.GetProperty("locale").GetString()!, t.GetProperty("title").GetString()!));
    if (titles.Count == 0) titles.Add(("pt-BR", "Produto Smoke M2"));
    var skus = new List<string>();
    if (body.TryGetProperty("skuCodes", out var ss) && ss.ValueKind == System.Text.Json.JsonValueKind.Array)
        foreach (var s in ss.EnumerateArray()) skus.Add(s.GetString()!);
    if (skus.Count == 0) skus.Add("SMOKE-M2-1");
    return (market, titles, skus);
}

app.Run();

public partial class Program; // para Microsoft.AspNetCore.Mvc.Testing
