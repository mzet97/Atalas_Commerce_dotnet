namespace Atlas.Payments.Api;

using System.Text.Json.Serialization;

/// <summary>Payload do webhook do PSP fake — snake_case (convenção ADR-0017).</summary>
public sealed record PspWebhookPayload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("provider_reference")] string ProviderReference,
    [property: JsonPropertyName("order_id")] string? OrderId);

/// <summary>Tipos de evento aceitos pelo webhook (INV-PAYMENT-001).</summary>
public static class PspWebhookEvents
{
    public const string Authorized = "payment.authorized";
    public const string Captured = "payment.captured";

    public static bool IsSupported(string type) => type is Authorized or Captured;
}
