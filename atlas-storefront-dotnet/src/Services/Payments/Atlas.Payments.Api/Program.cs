global using Atlas.Payments.Api;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Payments.Infrastructure;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenLocalhost(5398, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});
builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("Atlas.Payments").AddOtlpExporter());
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var mongoConn = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://127.0.0.1:27017/?directConnection=true";
var mongoDb = builder.Configuration["Mongo:PaymentsDatabase"] ?? "atlas_payments";
builder.Services.AddSingleton(_ => MongoPaymentsStore.Create(mongoConn, mongoDb));

builder.Services.AddHttpClient("psp", c => c.BaseAddress = new Uri(
    builder.Configuration["Psp:Url"] ?? "http://127.0.0.1:5399"));

var app = builder.Build();
app.MapOpenApi();
app.MapHealthChecks("/health");

var secret = app.Configuration["Psp:WebhookSecret"] ?? "dev-psp-webhook-secret";

// Webhook do PSP (INV-PAYMENT-001): autentica por HMAC-SHA256 (hex) do corpo bruto;
// dedup + transição CAS + outbox numa ÚNICA transação — repetido nunca reaplica efeito.
app.MapPost("/webhooks/psp", async (HttpRequest http, MongoPaymentsStore store, CancellationToken ct) =>
{
    var body = await new StreamReader(http.Body).ReadToEndAsync(ct);
    var signature = http.Headers["x-psp-signature"].ToString();

    var expected = Convert.ToHexString(
        new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    if (signature.Length == 0 || !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature), Encoding.UTF8.GetBytes(expected)))
        return Results.Unauthorized();

    PspWebhookPayload? payload;
    try
    {
        payload = JsonSerializer.Deserialize<PspWebhookPayload>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "payload inválido" });
    }
    if (payload is null || string.IsNullOrWhiteSpace(payload.Id) || string.IsNullOrWhiteSpace(payload.OrderId))
        return Results.BadRequest(new { error = "webhook sem id ou order_id" });
    if (!PspWebhookEvents.IsSupported(payload.Type))
        return Results.UnprocessableEntity(new { error = $"tipo não suportado: {payload.Type}" });

    var result = await store.ApplyWebhookAsync(
        payload.OrderId, payload.Id, payload.Type, payload.ProviderReference,
        correlationId: $"webhook:{payload.Id}", ct);

    return result switch
    {
        MongoPaymentsStore.WebhookApplyResult.Processed => Results.Ok(new { status = "processed", type = payload.Type }),
        MongoPaymentsStore.WebhookApplyResult.Duplicate => Results.Ok(new { status = "duplicate", type = payload.Type }),
        MongoPaymentsStore.WebhookApplyResult.IntentNotFound => Results.NotFound(new { status = "intent_not_found" }),
        _ => Results.Conflict(new { status = "state_mismatch", type = payload.Type }),
    };
});

// Diagnóstico read-only: intent por pedido (verificação do e2e/auditoria).
app.MapGet("/intents/by-order/{orderId}", async (string orderId, MongoPaymentsStore store, CancellationToken ct) =>
{
    var intent = await store.FindByOrderIdAsync(orderId, ct);
    if (intent is null) return Results.NotFound();
    var webhookCount = await store.WebhookCountAsync(orderId, ct);
    return Results.Ok(new
    {
        intentId = intent.Id.Value,
        state = intent.State.ToString(),
        orderId = intent.OrderId,
        pspIdempotencyKey = intent.PspIdempotencyKey,
        providerReference = intent.ProviderReference,
        declineReason = intent.DeclineReason,
        webhooksProcessed = webhookCount, // dedup é na coleção payments.processed_webhooks (índice único)
    });
});

// ---- SPEC-006 req.2 (REQ-RET-001): refund COERENTE — four-eyes token + idempotência + PSP real fake ----
var refundSecret = app.Configuration["Approval:TokenSecret"] ?? "atlas-dev-approval-secret";
var processedRefunds = app.Configuration["Refund:IdempotencyStore"] is null
    ? new System.Collections.Concurrent.ConcurrentDictionary<string, byte>()
    : throw new NotSupportedException("store persistente na SPEC-006+ (dev: in-memory)");

app.MapPost("/admin/refunds", async (
    HttpRequest http, MongoPaymentsStore store, IHttpClientFactory factory, CancellationToken ct) =>
{
    var body = await System.Text.Json.JsonSerializer.DeserializeAsync<System.Text.Json.JsonElement?>(
        http.Body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
    var orderId = body is not null && body.Value.TryGetProperty("orderId", out var oid) ? oid.GetString() : null;
    var amountMinor = body is not null && body.Value.TryGetProperty("amountMinor", out var amt) && amt.TryGetInt64(out var am) ? am : 0L;
    var token = body is not null && body.Value.TryGetProperty("approvalToken", out var tk) ? tk.GetString() : null;
    var idempotencyKey = body is not null && body.Value.TryGetProperty("idempotencyKey", out var ik) ? ik.GetString() : null;
    if (string.IsNullOrEmpty(orderId) || amountMinor <= 0 || string.IsNullOrEmpty(idempotencyKey))
        return Results.BadRequest(new { error = "orderId/amountMinor/idempotencyKey obrigatórios" });

    // 1) idempotência: mesma key → MESMO resultado (T02 do refund)
    if (!processedRefunds.TryAdd(idempotencyKey!, 0))
        return Results.Ok(new { status = "already-processed", orderId, amountMinor });

    // 2) four-eyes: token HMAC validado no OWNER (INV-SEC-001) — emitido pelo Control pós-RMA
    var approval = RefundApprovalToken.Validate(token, refundSecret, orderId!, amountMinor, DateTimeOffset.UtcNow, out var failure);
    if (approval is null)
        return Results.Json(new { error = $"approval_token rejeitado: {failure}", audited = true }, statusCode: 403);

    // 3) coerência: só refund de intent CAPTURED (nunca sem pagamento capturado)
    var intent = await store.FindByOrderIdAsync(orderId!, ct);
    if (intent is null) return Results.NotFound(new { error = "intent inexistente" });
    if (!intent.TryStartRefund($"rma:{idempotencyKey}", DateTimeOffset.UtcNow))
        return Results.Conflict(new { error = $"intent não capturado (estado {intent.State}) — refund recusado", audited = true });

    // 4) PSP fake: POST /v1/refunds (idempotente pelo provider_reference)
    var client = factory.CreateClient("psp");
    var psp = await client.PostAsJsonAsync("/v1/refunds", new
    {
        provider_reference = intent.ProviderReference ?? intent.PspIdempotencyKey,
        amount_minor = amountMinor,
        idempotency_key = idempotencyKey,
    }, ct);
    if (!psp.IsSuccessStatusCode)
    {
        intent.TryStartRefund($"retry:{idempotencyKey}", DateTimeOffset.UtcNow); // mantém RefundPending p/ conciliação
        await store.SaveAsync(intent, ct);
        return Results.Json(new { status = "refund-pending", reason = "PSP indisponível" }, statusCode: 202);
    }

    intent.CompleteRefund(DateTimeOffset.UtcNow);
    await store.SaveAsync(intent, ct);
    return Results.Ok(new { status = "refunded", orderId, amountMinor, intentState = intent.State.ToString() });
});

app.MapGet("/", () => Results.Ok(new { service = "Atlas.Payments.Api", milestone = "M6" }));

app.Run();

public partial class Program;
