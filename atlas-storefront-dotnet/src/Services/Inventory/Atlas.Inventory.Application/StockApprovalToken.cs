using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Atlas.Inventory.Application;

/// <summary>
/// Approval artifact four-eyes v2 (SPEC-004, INV-ADMIN-001/INV-SEC-001): HMAC-SHA256 sobre
/// o payload canônico {sub, action, subject, exp, jti}. Emitido pelo Control APÓS decisão de
/// approver distinto com grant ativo; validado pelo OWNER ANTES do comando — a aprovação não
/// é bypass (o owner recusa token expirado, com action/subject divergentes ou sem jti).
/// Mesma convenção do ApprovalToken do Catalog (segredo Approval:TokenSecret dev compartilhado).
/// </summary>
public static class StockApprovalToken
{
    public const string AdjustAction = "inventory.adjust";

    public sealed record Payload(
        [property: System.Text.Json.Serialization.JsonPropertyName("sub")] string Sub,
        [property: System.Text.Json.Serialization.JsonPropertyName("action")] string Action,
        [property: System.Text.Json.Serialization.JsonPropertyName("subject")] string Subject,
        [property: System.Text.Json.Serialization.JsonPropertyName("exp")] long ExpUnixSeconds,
        [property: System.Text.Json.Serialization.JsonPropertyName("jti")] string Jti);

    public static string SubjectFor(string warehouseId, string skuId) => $"{warehouseId}:{skuId}";

    public static string Issue(Payload payload, string secret)
    {
        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(Serialize(payload))).TrimEnd('=');
        var sig = Convert.ToBase64String(Sign(body, secret)).TrimEnd('=');
        return $"{body}.{sig}";
    }

    public static Payload? TryValidate(string? token, string secret, string action, string subject,
        DateTimeOffset nowUtc, out string? failure)
    {
        failure = null;
        if (string.IsNullOrWhiteSpace(token)) { failure = "token ausente"; return null; }
        var parts = token.Split('.');
        if (parts.Length != 2) { failure = $"formato ({parts.Length} partes)"; return null; }
        var (body, sig) = (parts[0], parts[1]);
        if (!CryptographicOperations.FixedTimeEquals(
                Sign(body, secret), Convert.FromBase64String(Pad(sig))))
        { failure = "assinatura"; return null; }

        Payload? payload;
        try { payload = JsonSerializer.Deserialize<Payload>(Decode(body)); }
        catch (JsonException) { payload = null; }
        if (payload is null) { failure = "payload ilegível"; return null; }
        if (payload.ExpUnixSeconds < nowUtc.ToUnixTimeSeconds()) { failure = $"expirado ({payload.ExpUnixSeconds})"; return null; }
        if (!string.Equals(payload.Action, action, StringComparison.Ordinal))
        { failure = $"action ({payload.Action} != {action})"; return null; }
        if (!string.Equals(payload.Subject, subject, StringComparison.Ordinal))
        { failure = $"subject ({payload.Subject} != {subject})"; return null; }
        if (string.IsNullOrWhiteSpace(payload.Sub) || string.IsNullOrWhiteSpace(payload.Jti))
        { failure = "sub/jti vazio"; return null; }
        return payload;
    }

    private static string Serialize(Payload payload) => JsonSerializer.Serialize(payload);
    private static byte[] Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
    }
    private static string Pad(string s) => s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
    private static string Decode(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(Pad(s)));
}
