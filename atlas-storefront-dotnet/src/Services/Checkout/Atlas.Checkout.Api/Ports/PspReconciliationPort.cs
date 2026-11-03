using System.Net;
using System.Text.Json;
using Atlas.Checkout.Application;

namespace Atlas.Checkout.Api.Ports;

/// <summary>Consulta o resultado REAL da autorização no PSP fake (GET by-key).</summary>
public sealed class PspReconciliationPort(IHttpClientFactory factory, string baseUrl) : IPspReconciliationPort
{
    public async Task<(ReconciliationOutcome Outcome, string? ProviderReference)> QueryAuthorizationAsync(
        string idempotencyKey, CancellationToken ct)
    {
        var client = factory.CreateClient("psp");
        client.BaseAddress = new Uri(baseUrl);
        try
        {
            using var response = await client.GetAsync($"/v1/authorizations/by-key/{Uri.EscapeDataString(idempotencyKey)}", ct);
            Console.WriteLine($"[psp-recon] GET by-key={idempotencyKey} → {(int)response.StatusCode}");
            if (response.StatusCode == HttpStatusCode.NotFound)
                // PSP fake determinístico (ADR-0017): chave inexistente = autorização NUNCA
                // processada (ex. crash antes de chegar ao PSP) → compensação é segura
                return (ReconciliationOutcome.NotAuthorized, null);
            if (response.StatusCode != HttpStatusCode.OK) return (ReconciliationOutcome.Indeterminate, null);
            var raw = await response.Content.ReadAsStringAsync(ct);
            Console.WriteLine($"[psp-recon] body={raw}");
            var body = JsonDocument.Parse(raw).RootElement;
            if (body.GetProperty("status").GetString() != "AUTHORIZED") return (ReconciliationOutcome.NotAuthorized, null);
            return (ReconciliationOutcome.Paid, body.GetProperty("provider_reference").GetString());
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return (ReconciliationOutcome.Indeterminate, null);
        }
    }
}
