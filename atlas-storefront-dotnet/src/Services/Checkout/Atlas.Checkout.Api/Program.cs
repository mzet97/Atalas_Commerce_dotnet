global using Atlas.Checkout.Api;
using Atlas.Checkout.Api.Ports;
using Atlas.Checkout.Application;
using Atlas.Checkout.Domain;
using Atlas.Checkout.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("Atlas.Checkout").AddOtlpExporter());
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var mongoConn = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://127.0.0.1:27017/?directConnection=true";
var mongoCheckoutDb = builder.Configuration["Mongo:CheckoutDatabase"] ?? "atlas_checkout";
var mongoOrdersDb = builder.Configuration["Mongo:OrdersDatabase"] ?? "atlas_orders";
var mongoPaymentsDb = builder.Configuration["Mongo:PaymentsDatabase"] ?? "atlas_payments";

builder.Services.AddSingleton(_ => Atlas.Checkout.Infrastructure.MongoSagaStore.Create(mongoConn, mongoCheckoutDb));
builder.Services.AddSingleton<ISagaStore>(sp => sp.GetRequiredService<Atlas.Checkout.Infrastructure.MongoSagaStore>());
builder.Services.AddSingleton(_ => Atlas.Checkout.Infrastructure.MongoCheckoutStore.Create(mongoConn, mongoCheckoutDb));
builder.Services.AddSingleton<IQuoteStore>(sp => sp.GetRequiredService<Atlas.Checkout.Infrastructure.MongoCheckoutStore>());
builder.Services.AddSingleton(_ => Atlas.Orders.Infrastructure.MongoOrdersStore.Create(mongoConn, mongoOrdersDb));
builder.Services.AddSingleton<IOrdersStore>(sp => new OrdersStoreAdapter(sp.GetRequiredService<Atlas.Orders.Infrastructure.MongoOrdersStore>()));
builder.Services.AddSingleton(_ => Atlas.Payments.Infrastructure.MongoPaymentsStore.Create(mongoConn, mongoPaymentsDb));
builder.Services.AddSingleton<IPaymentsStore>(sp => new PaymentsStoreAdapter(sp.GetRequiredService<Atlas.Payments.Infrastructure.MongoPaymentsStore>()));

builder.Services.AddHttpClient("psp");
builder.Services.AddSingleton<IInventoryReservePort>(sp =>
    new GrpcInventoryReservePort(sp.GetRequiredService<IConfiguration>()["InventoryOwner:Url"] ?? "http://127.0.0.1:5292"));
builder.Services.AddSingleton<IPspPort>(sp => new HttpPspPort(
    sp.GetRequiredService<IHttpClientFactory>(),
    sp.GetRequiredService<IConfiguration>()["Psp:Url"] ?? "http://127.0.0.1:5399",
    sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<CheckoutOrchestrator>();

builder.Services.AddSingleton<IPspReconciliationPort>(sp =>
    new PspReconciliationPort(sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<IConfiguration>()["Psp:Url"] ?? "http://127.0.0.1:5399"));
builder.Services.AddSingleton<IOrderReconciliationStore>(sp => new OrdersReconciliationAdapter(sp.GetRequiredService<Atlas.Orders.Infrastructure.MongoOrdersStore>()));
builder.Services.AddSingleton<IPaymentReconciliationStore>(sp => new PaymentsReconciliationAdapter(sp.GetRequiredService<Atlas.Payments.Infrastructure.MongoPaymentsStore>()));
builder.Services.AddSingleton<Atlas.Checkout.Application.IWorkerLease>(sp =>
    new Atlas.Checkout.Application.MongoWorkerLease(
        new MongoDB.Driver.MongoClient(mongoConn).GetDatabase(mongoCheckoutDb)));
builder.Services.AddHostedService<ReconciliationHostedService>();

var app = builder.Build();
app.MapOpenApi();
app.MapHealthChecks("/health");

app.MapPost("/quotes", async (Atlas.Checkout.Infrastructure.MongoCheckoutStore store, CheckoutQuoteRequest req, CancellationToken ct) =>
{
    if (req.TtlSeconds <= 0)
        return Microsoft.AspNetCore.Http.Results.BadRequest(new { error = "ttl_seconds deve ser > 0" });
    var now = DateTimeOffset.UtcNow;
    var lines = req.Lines.Select(l => new PriceQuote.QuoteLine(l.SkuId, l.Quantity, l.UnitPriceMinor, l.UnitPriceMinor * l.Quantity)).ToArray();
    var quote = PriceQuote.Issue(req.CartId, lines, req.Currency, now, TimeSpan.FromSeconds(req.TtlSeconds));
    await store.SaveQuoteAsync(quote, ct); // persistido ANTES de responder (sem fire-and-forget)
    return Microsoft.AspNetCore.Http.Results.Ok
    (
        new
        {
            quoteId = quote.Id.Value,
            totalMinor = quote.TotalMinor,
            currency = quote.Currency,
            expiresAtUtc = quote.ExpiresAtUtc,
        }
    );
});

app.MapGet("/quotes/{quoteId:guid}", async (Guid quoteId, Atlas.Checkout.Infrastructure.MongoCheckoutStore store, CancellationToken ct) =>
{
    var quote = await store.FindByIdAsync(quoteId, ct);
    return quote is null
        ? Microsoft.AspNetCore.Http.Results.NotFound()
        : Microsoft.AspNetCore.Http.Results.Ok(new
        {
            quoteId = quote.Id.Value,
            totalMinor = quote.TotalMinor,
            currency = quote.Currency,
            expiresAtUtc = quote.ExpiresAtUtc,
            valid = quote.IsValidAt(DateTimeOffset.UtcNow),
        });
});

app.MapPost("/checkout", async (CheckoutOrchestrator orchestrator, SubmitCheckoutRequest request, CancellationToken ct) =>
{
    var command = new SubmitCheckoutCommand(
        IdempotencyKey: request.IdempotencyKey,
        CustomerId: request.CustomerId,
        QuoteId: request.QuoteId,
        Lines: [.. request.Lines.Select(l => new CheckoutLineDto(l.SkuId, l.Quantity, l.UnitPriceMinor))],
        TotalMinor: request.TotalMinor,
        Currency: request.Currency,
        Shipping: new OrderShippingAddressDto(
            request.Shipping.Country, request.Shipping.PostalCode,
            request.Shipping.Line1, request.Shipping.City));

    var result = await orchestrator.SubmitAsync(command, ct);
    return result.Status switch
    {
        CheckoutResultStatus.Confirmed => Microsoft.AspNetCore.Http.Results.Json(result),
        CheckoutResultStatus.Pending => Microsoft.AspNetCore.Http.Results.Json(result, statusCode: 202),
        _ => Microsoft.AspNetCore.Http.Results.Json(result, statusCode: 409),
    };
});

// Diagnóstico read-only (dev/observabilidade): estado da saga por idempotency key.
app.MapGet("/sagas/{idempotencyKey}", async (string idempotencyKey, Atlas.Checkout.Infrastructure.MongoSagaStore store, CancellationToken ct) =>
{
    var saga = await store.FindByIdempotencyKeyAsync(idempotencyKey, ct);
    return saga is null
        ? Microsoft.AspNetCore.Http.Results.NotFound()
        : Microsoft.AspNetCore.Http.Results.Ok(new
        {
            sagaId = saga.Id.Value,
            state = saga.State.ToString(),
            orderId = saga.OrderId,
            reservationId = saga.ReservationId,
            paymentIntentId = saga.PaymentIntentId,
            providerReference = saga.ProviderReference,
        });
});

app.MapGet("/", () => Microsoft.AspNetCore.Http.Results.Ok(new { service = "Atlas.Checkout.Api", milestone = "M3" }));

app.Run();
