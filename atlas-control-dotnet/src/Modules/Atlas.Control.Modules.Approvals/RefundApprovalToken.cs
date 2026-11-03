namespace Atlas.Control.Modules.Approvals;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Espelho do RefundApprovalToken do Payments owner (mesma convenção HMAC dev).</summary>
public static class RefundApprovalToken
{
    public sealed record Payload(
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("order_id")] string OrderId,
        [property: JsonPropertyName("amount_minor")] long AmountMinor,
        [property: JsonPropertyName("exp")] long ExpUnixSeconds);

    public static string Issue(Payload payload, string secret)
    {
        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))).TrimEnd('=');
        var sig = Convert.ToBase64String(new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(body))).TrimEnd('=');
        return $"{body}.{sig}";
    }
}
