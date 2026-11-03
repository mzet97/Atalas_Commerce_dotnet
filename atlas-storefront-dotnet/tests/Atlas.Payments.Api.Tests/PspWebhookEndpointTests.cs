using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Text.Json;
using Atlas.Payments.Domain;
using Atlas.Payments.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Atlas.Payments.Api.Tests;

/// <summary>
/// Webhook do PSP E2E no host real (replica set Mongo): HMAC obrigatório, dedup por
/// webhook id (UC003-T05 — repetido NUNCA reaplica efeito), transição CAS com guard.
/// </summary>
public sealed class PspWebhookEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Secret = "dev-psp-webhook-secret";
    private readonly WebApplicationFactory<Program> _factory;

    public PspWebhookEndpointTests(WebApplicationFactory<Program> factory)
    {
        var dbName = $"atlas_pay_{Guid.NewGuid():N}";
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Mongo:PaymentsDatabase", dbName);
            b.UseSetting("Psp:WebhookSecret", Secret);
        });
        Store = MongoPaymentsStore.Create(
            "mongodb://127.0.0.1:27017/?directConnection=true", dbName);
    }

    private MongoPaymentsStore Store { get; }

    private static string Sign(string body) => Convert.ToHexString(
        new HMACSHA256(Encoding.UTF8.GetBytes(Secret)).ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    private static string Payload(string webhookId, string type, string orderId) => JsonSerializer.Serialize(
        new { id = webhookId, type, provider_reference = "pr_test", order_id = orderId, simulated = true });

    private async Task<HttpResponseMessage> PostWebhookAsync(string body, string? signature = null)
    {
        var client = _factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        if (signature is not null)
            content.Headers.Add("x-psp-signature", signature);
        return await client.PostAsync("/webhooks/psp", content);
    }

    private async Task<PaymentIntent> SeedIntentAsync(string orderId, PaymentIntentState state)
    {
        var intent = PaymentIntent.Create(orderId, 2500, "BRL", $"psp-key-{Guid.NewGuid():N}");
        if (state == PaymentIntentState.Unknown) intent.MarkUnknown("timeout", DateTimeOffset.UtcNow);
        if (state == PaymentIntentState.Authorized) intent.TryAuthorize("pr_sync", null, DateTimeOffset.UtcNow);
        await Store.SaveAsync(intent, CancellationToken.None);
        return intent;
    }

    [Fact]
    public async Task Webhook_authorized_em_intent_Unknown_transitiona_para_Authorized()
    {
        var orderId = Guid.NewGuid().ToString();
        await SeedIntentAsync(orderId, PaymentIntentState.Unknown);

        var response = await PostWebhookAsync(Payload("wh-1", PspWebhookEvents.Authorized, orderId), Sign(Payload("wh-1", PspWebhookEvents.Authorized, orderId)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("processed", await response.Content.ReadAsStringAsync());

        var intent = await Store.FindByOrderIdAsync(orderId, CancellationToken.None);
        Assert.Equal(PaymentIntentState.Authorized, intent!.State);
        Assert.Equal("pr_test", intent.ProviderReference);
    }

    [Fact]
    public async Task T05_mesmo_webhook_id_nunca_reaplica_efeito()
    {
        var orderId = Guid.NewGuid().ToString();
        await SeedIntentAsync(orderId, PaymentIntentState.Unknown);

        var body = Payload("wh-dup", PspWebhookEvents.Authorized, orderId);
        var first = await PostWebhookAsync(body, Sign(body));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("processed", await first.Content.ReadAsStringAsync());

        // PSP reenvia o MESMO webhook (WEBHOOK_DUPLICATE): HTTP 200 idempotente, efeito único
        var second = await PostWebhookAsync(body, Sign(body));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("duplicate", await second.Content.ReadAsStringAsync());

        var intent = await Store.FindByOrderIdAsync(orderId, CancellationToken.None);
        Assert.Equal(PaymentIntentState.Authorized, intent!.State); // transição única
        var webhooks = await Store.WebhookCountAsync(orderId);
        Assert.Equal(1, webhooks); // dedup gravou UMA vez
    }

    [Fact]
    public async Task Webhook_sem_assinatura_válida_rejeita_com_401()
    {
        var orderId = Guid.NewGuid().ToString();
        await SeedIntentAsync(orderId, PaymentIntentState.Unknown);
        var body = Payload("wh-bad", PspWebhookEvents.Authorized, orderId);

        var unsigned = await PostWebhookAsync(body); // sem header
        Assert.Equal(HttpStatusCode.Unauthorized, unsigned.StatusCode);

        var forged = await PostWebhookAsync(body, new string('0', 64)); // assinatura falsa
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);

        var intent = await Store.FindByOrderIdAsync(orderId, CancellationToken.None);
        Assert.Equal(PaymentIntentState.Unknown, intent!.State); // nenhum efeito
    }

    [Fact]
    public async Task Captured_em_Authorized_avança_e_repetido_não_reduplica_captura()
    {
        var orderId = Guid.NewGuid().ToString();
        await SeedIntentAsync(orderId, PaymentIntentState.Authorized);

        var body = Payload("wh-cap", PspWebhookEvents.Captured, orderId);
        var first = await PostWebhookAsync(body, Sign(body));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("processed", await first.Content.ReadAsStringAsync());

        // reentrega com id DIFERENTE mas intent já Captured → idempotente (sem efeito duplo)
        var body2 = Payload("wh-cap-2", PspWebhookEvents.Captured, orderId);
        var second = await PostWebhookAsync(body2, Sign(body2));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("duplicate", await second.Content.ReadAsStringAsync());

        var intent = await Store.FindByOrderIdAsync(orderId, CancellationToken.None);
        Assert.Equal(PaymentIntentState.Captured, intent!.State);
    }

    [Fact]
    public async Task Webhook_para_order_inexistente_rejeita_sem_gravar_dedup()
    {
        var orderId = Guid.NewGuid().ToString();
        var body = Payload("wh-404", PspWebhookEvents.Authorized, orderId);

        var response = await PostWebhookAsync(body, Sign(body));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // retry com intent agora existente PRECISA funcionar (dedup não ficou gravado)
        await SeedIntentAsync(orderId, PaymentIntentState.Unknown);
        var retry = await PostWebhookAsync(body, Sign(body));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Contains("processed", await retry.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Fora_de_ordem_captured_em_Unknown_rejeita_409_sem_efeito()
    {
        // TEST-003-004: webhook ANTIGO (captured) chega antes do authorized — CAS
        // rejeita (nenhuma transição inválida), nada é aplicado nem gravado
        var orderId = Guid.NewGuid().ToString();
        await SeedIntentAsync(orderId, PaymentIntentState.Unknown);
        var body = Payload("wh-ooo", PspWebhookEvents.Captured, orderId);

        var response = await PostWebhookAsync(body, Sign(body));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var intent = await Store.FindByOrderIdAsync(orderId, CancellationToken.None);
        Assert.Equal(PaymentIntentState.Unknown, intent!.State); // inalterado
        Assert.Equal(0, await Store.WebhookCountAsync(orderId));  // dedup não gravado

        // ordem correta depois: authorized → processed
        var body2 = Payload("wh-ooo-2", PspWebhookEvents.Authorized, orderId);
        var ok = await PostWebhookAsync(body2, Sign(body2));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("processed", await ok.Content.ReadAsStringAsync());
    }
}
