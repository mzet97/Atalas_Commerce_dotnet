namespace Atlas.Payments.Api;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// ApprovalToken de REFUND (SPEC-006 req.2, REQ-RET-001 four-eyes): HMAC-SHA256 sobre
/// {sub, order_id, amount_minor, exp} emitido pelo Control APÓS RMA aprovada (≠ requester);
/// validado no OWNER antes do refund (INV-SEC-001). Mesma convenção do M2/M4 (secret compartilhado dev).
/// </summary>
public static class RefundApprovalToken
{
    public sealed record Payload(
        [property: System.Text.Json.Serialization.JsonPropertyName("sub")] string Sub,
        [property: System.Text.Json.Serialization.JsonPropertyName("order_id")] string OrderId,
        [property: System.Text.Json.Serialization.JsonPropertyName("amount_minor")] long AmountMinor,
        [property: System.Text.Json.Serialization.JsonPropertyName("exp")] long ExpUnixSeconds);

    public static string Issue(Payload payload, string secret)
    {
        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(payload))).TrimEnd('=');
        var sig = Convert.ToBase64String(new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(body))).TrimEnd('=');
        return $"{body}.{sig}";
    }

    public static Payload? Validate(string? token, string secret, string orderId, long amountMinor, DateTimeOffset nowUtc, out string? failure)
    {
        failure = null;
        if (string.IsNullOrWhiteSpace(token)) { failure = "token ausente"; return null; }
        var parts = token.Split('.');
        if (parts.Length != 2) { failure = "formato"; return null; }
        var (body, sig) = (parts[0], parts[1]);
        if (!CryptographicOperations.FixedTimeEquals(
                new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(body)),
                Convert.FromBase64String(Pad(sig)))) { failure = "assinatura"; return null; }
        Payload? payload;
        try { payload = System.Text.Json.JsonSerializer.Deserialize<Payload>(Decode(body)); }
        catch (System.Text.Json.JsonException) { payload = null; }
        if (payload is null) { failure = "payload ilegível"; return null; }
        if (payload.ExpUnixSeconds < nowUtc.ToUnixTimeSeconds()) { failure = "expirado"; return null; }
        if (!string.Equals(payload.OrderId, orderId, StringComparison.Ordinal)) { failure = $"order_id ({payload.OrderId} != {orderId})"; return null; }
        if (payload.AmountMinor != amountMinor) { failure = $"amount ({payload.AmountMinor} != {amountMinor})"; return null; }
        return payload;
    }

    private static string Pad(string s) => s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
    private static string Decode(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(Pad(s)));
}
