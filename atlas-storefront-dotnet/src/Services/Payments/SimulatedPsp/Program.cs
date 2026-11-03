using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenLocalhost(5399, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");

var secret = app.Configuration["Psp:WebhookSecret"] ?? "dev-psp-webhook-secret";
// Autorizações "em memória" do PSP fake (dev/CI — ADR-0017: nunca parecer gateway real)
var authorizedStore = new Dictionary<string, (string OrderId, long Amount, string Currency)>();

// Cenários determinísticos por header x-psp-scenario:
// APPROVED | DECLINED | TIMEOUT | UNKNOWN | WEBHOOK_DUPLICATE | WEBHOOK_OUT_OF_ORDER | REFUND
app.MapPost("/v1/authorizations", async (HttpContext http, JsonElement body) =>
{
    var scenario = http.Request.Headers["x-psp-scenario"].ToString() is { Length: > 0 } s ? s : "APPROVED";
    var orderId = body.GetProperty("order_id").GetString()!;
    var amount = body.GetProperty("amount_minor").GetInt64();
    var currency = body.GetProperty("currency").GetString()!;
    var intentKey = body.GetProperty("idempotency_key").GetString()!;
    var providerRef = $"pr_{intentKey[..Math.Min(12, intentKey.Length)]}";

    switch (scenario)
    {
        case "DECLINED":
            return Results.Json(new { status = "DECLINED", reason = "cartão recusado (simulado)" }, statusCode: 402);
        case "TIMEOUT":
            return Results.StatusCode(503); // sem resposta e SEM efeito (falha antes de processar)
        case "UNKNOWN":
            // Pagamento PODE ter sido processado e a resposta se perdeu — registra e responde 503.
            // O reconciliador consulta /v1/authorizations/{key} para descobrir o resultado real.
            authorizedStore[providerRef] = (orderId, amount, currency);
            return Results.StatusCode(503);
        default:
            authorizedStore[providerRef] = (orderId, amount, currency);
            return Results.Json(new { status = "AUTHORIZED", provider_reference = providerRef, simulated = true });
    }
});

app.MapGet("/v1/authorizations/by-key/{idempotencyKey}", (string idempotencyKey) =>
{
    Console.WriteLine($"[psp] by-key consulta: {idempotencyKey} | store count: {authorizedStore.Count} | keys: {string.Join(",", authorizedStore.Keys)}");
    var entry = authorizedStore.FirstOrDefault(kv => kv.Key.EndsWith(idempotencyKey[..Math.Min(12, idempotencyKey.Length)], StringComparison.Ordinal) || kv.Key.Contains(idempotencyKey[..Math.Min(12, idempotencyKey.Length)]));
    return entry.Key is not null
        ? Results.Json(new { status = "AUTHORIZED", provider_reference = entry.Key, order_id = entry.Value.OrderId, simulated = true })
        : Results.Json(new { status = "NOT_FOUND" }, statusCode: 404);
});

app.MapPost("/v1/captures", (JsonElement body) =>
{
    var providerRef = body.GetProperty("provider_reference").GetString()!;
    return authorizedStore.ContainsKey(providerRef)
        ? Results.Json(new { status = "CAPTURED", provider_reference = providerRef, simulated = true })
        : Results.Json(new { error = "autorização inexistente" }, statusCode: 404);
});

app.MapPost("/v1/refunds", (JsonElement body) =>
    Results.Json(new
    {
        status = "REFUNDED",
        provider_reference = body.GetProperty("provider_reference").GetString()!,
        simulated = true,
    }));

// Gera o payload+assinatura do webhook (o Payments Service valida HMAC e deduplica por id)
app.MapGet("/v1/webhook-payload", (string providerRef, string eventType) =>
{
    var orderId = authorizedStore.TryGetValue(providerRef, out var entry) ? entry.OrderId : "";
    var body = JsonSerializer.Serialize(new
    {
        id = $"wh_{providerRef}_{eventType}",
        type = eventType,
        provider_reference = providerRef,
        order_id = orderId,
        simulated = true,
    });
    var sig = Convert.ToHexString(new HMACSHA256(Encoding.UTF8.GetBytes(secret))
        .ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    return Results.Json(new { body, signature = sig });
});

var scenarios = new[] { "APPROVED", "DECLINED", "TIMEOUT", "UNKNOWN", "WEBHOOK_DUPLICATE", "WEBHOOK_OUT_OF_ORDER", "REFUND" };
app.MapGet("/", () => Results.Ok(new
{
    service = "SimulatedPSP",
    warning = "SIMULADO — NUNCA usar como gateway real (ADR-0017)",
    scenarios,
}));

app.Run();

public partial class Program;
