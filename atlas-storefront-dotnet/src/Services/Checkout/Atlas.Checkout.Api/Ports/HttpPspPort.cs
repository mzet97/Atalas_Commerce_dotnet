using System.Net;
using System.Text.Json;
using Atlas.Checkout.Application;

namespace Atlas.Checkout.Api.Ports;

/// <summary>Port real do PSP fake via HTTP (cenário por header — ADR-0017).</summary>
public sealed class HttpPspPort(IHttpClientFactory factory, string baseUrl, IConfiguration config) : IPspPort
{
    private string Secret => config["Psp:WebhookSecret"] ?? "dev-psp-webhook-secret";

    public async Task<(PspOutcome Outcome, string? ProviderReference, string? Reason)> AuthorizeAsync(
        string orderReference, long amountMinor, string currency, CancellationToken ct)
    {
        var client = factory.CreateClient("psp");
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Add("x-psp-scenario", config["Psp:Scenario"] ?? "APPROVED");
        try
        {
            using var response = await client.PostAsJsonAsync("/v1/authorizations", new
            {
                order_id = orderReference,
                amount_minor = amountMinor,
                currency,
                idempotency_key = orderReference,
            }, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (raw.Length == 0)
                return (PspOutcome.Unknown, null, $"psp http {(int)response.StatusCode}");
            var body = JsonDocument.Parse(raw).RootElement;
            if ((int)response.StatusCode == 402)
                return (PspOutcome.Declined, null, body.GetProperty("reason").GetString());
            return (PspOutcome.Authorized, body.GetProperty("provider_reference").GetString(), null);
        }
        catch (HttpRequestException)
        {
            return (PspOutcome.Unknown, null, "psp indisponível");
        }
    }
}
