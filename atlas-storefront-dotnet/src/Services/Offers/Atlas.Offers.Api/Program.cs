using Atlas.Offers.Api;
using Atlas.Offers.Application;
using Atlas.Offers.Infrastructure;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenLocalhost(5293, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});
builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("Atlas.Offers").AddOtlpExporter());
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var mongoConn = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://127.0.0.1:27017/?directConnection=true";
var mongoDb = builder.Configuration["Mongo:OffersDatabase"] ?? "atlas_offers";
builder.Services.AddSingleton(_ => MongoOffersStore.Create(mongoConn, mongoDb));
builder.Services.AddSingleton<Atlas.Offers.Application.IOffersStore>(sp => sp.GetRequiredService<MongoOffersStore>());
builder.Services.AddSingleton<PublishOfferHandler>();

var app = builder.Build();
app.MapOpenApi();
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new { service = "Atlas.Offers.Api", milestone = "M5" }));

// DEV/OPS ONLY: cria ATIVA uma oferta direto no owner (o caminho de produção é Pricing Manager
// → workflow de preço — SPEC-004+). Escopo único ativo por (sku, seller, market, channel).
app.MapPost("/admin/offers", async (PublishOfferHandler handler, DevPublishOfferRequest req, CancellationToken ct) =>
{
    var now = DateTimeOffset.UtcNow;
    var outcome = await handler.HandleAsync(new PublishOfferCommand(
        RequestId: req.RequestId ?? $"dev-{Guid.NewGuid():N}",
        ActorSub: req.ActorSub ?? "dev-seed",
        SkuId: req.SkuId,
        SellerId: req.SellerId ?? "atlas-1p",
        MarketId: req.MarketId,
        Channel: req.Channel ?? "web",
        Currency: req.Currency,
        PriceMinor: req.PriceMinor,
        ListPriceMinor: req.ListPriceMinor ?? req.PriceMinor,
        ValidFromUtc: now.AddMinutes(-1),
        ValidToUtc: now.AddDays(req.ValidDays ?? 365),
        CorrelationId: $"dev-{Guid.NewGuid():N}"), ct);
    return outcome.Accepted
        ? Results.Ok(new { offerId = outcome.OfferId.Value, version = outcome.Version, skuId = req.SkuId, marketId = req.MarketId })
        : Results.Conflict(new { reason = outcome.Reason });
});

app.Run();

public partial class Program;
