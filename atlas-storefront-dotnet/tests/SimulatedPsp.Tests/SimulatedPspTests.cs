using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SimulatedPsp.Tests;

/// <summary>
/// REQ-PAY-001/ADR-0017: PSP fake determinístico — cenários reproduzíveis por header,
/// webhook assinado HMAC validável, e a identidade SIMULADA explícita.
/// </summary>
public sealed class SimulatedPspTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string Secret = "dev-psp-webhook-secret";

    public SimulatedPspTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private async Task<(JsonElement? Body, HttpStatusCode Code)> AuthorizeAsync(string scenario, string key)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-psp-scenario", scenario);
        var response = await client.PostAsJsonAsync("/v1/authorizations", new
        {
            order_id = "order-smoke",
            amount_minor = 5000,
            currency = "BRL",
            idempotency_key = key,
        });
        var raw = await response.Content.ReadAsStringAsync();
        JsonElement? body = raw.Length > 0 ? JsonDocument.Parse(raw).RootElement : null;
        return (body, response.StatusCode);
    }

    [Fact]
    public async Task APPROVED_autoriza_e_captura()
    {
        var (body, code) = await AuthorizeAsync("APPROVED", "k-ok-1");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("AUTHORIZED", body!.Value.GetProperty("status").GetString());
        Assert.True(body.Value.GetProperty("simulated").GetBoolean());
        var pr = body.Value.GetProperty("provider_reference").GetString()!;

        var client = _factory.CreateClient();
        var capture = await client.PostAsJsonAsync("/v1/captures", new { provider_reference = pr });
        Assert.Equal(HttpStatusCode.OK, capture.StatusCode);
    }

    [Fact]
    public async Task DECLINED_402_e_TIMEOUT_503()
    {
        var (declined, codeDeclined) = await AuthorizeAsync("DECLINED", "k-dec");
        Assert.Equal(HttpStatusCode.PaymentRequired, codeDeclined);
        Assert.Equal("DECLINED", declined!.Value.GetProperty("status").GetString());

        var (_, codeTimeout) = await AuthorizeAsync("TIMEOUT", "k-to"); // 503 sem body JSON
        Assert.Equal(HttpStatusCode.ServiceUnavailable, codeTimeout);
    }

    [Fact]
    public async Task Webhook_pode_ser_validado_por_HMAC()
    {
        var (_, _) = await AuthorizeAsync("APPROVED", "k-wh");
        var client = _factory.CreateClient();
        var whRaw = await client.GetStringAsync("/v1/webhook-payload?providerRef=pr_k-wh&eventType=payment.authorized");
        using var wh = JsonDocument.Parse(whRaw);
        var body = wh.RootElement.GetProperty("body").GetString()!;
        var sig = wh.RootElement.GetProperty("signature").GetString()!;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        Assert.Equal(expected, sig); // o Payments Service valida exatamente isto (INV-PAYMENT-001)
    }

    [Fact]
    public async Task Identidade_SIMULADA_exposta_na_raiz()
    {
        var client = _factory.CreateClient();
        var raw = await client.GetStringAsync("/");
        Assert.Contains("SimulatedPSP", raw);
        Assert.Contains("SIMULADO", raw);
    }
}
