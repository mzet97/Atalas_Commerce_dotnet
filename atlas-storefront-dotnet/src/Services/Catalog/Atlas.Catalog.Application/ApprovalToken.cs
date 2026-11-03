using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Atlas.Catalog.Application;

/// <summary>
/// Approval artifact four-eyes (ADR-0001/0007, INV-ADMIN-001): HMAC-SHA256 sobre o payload
/// canônico {sub, product_id, expected_version, exp}. Emitido pelo Control APÓS decisão do
/// approver (≠ solicitante); validado pelo OWNER antes do comando — a aprovação não é bypass.
/// Segredo compartilhado dev (env APPROVAL_TOKEN_SECRET); assimetria (RS256) na fatia K8s/secrets.
/// </summary>
public static class ApprovalToken
{
    public sealed record Payload(
        [property: System.Text.Json.Serialization.JsonPropertyName("sub")] string Sub,
        [property: System.Text.Json.Serialization.JsonPropertyName("product_id")] string ProductId,
        [property: System.Text.Json.Serialization.JsonPropertyName("expected_version")] long ExpectedVersion,
        [property: System.Text.Json.Serialization.JsonPropertyName("exp")] long ExpUnixSeconds);

    public static string Issue(Payload payload, string secret)
    {
        var body = Serialize(payload);
        var sig = Convert.ToBase64String(Sign(body, secret)).TrimEnd('=');
        return $"{body}.{sig}";
    }

    public static Payload? Validate(string? token, string secret, string productId, long expectedVersion, DateTimeOffset nowUtc) =>
        TryValidate(token, secret, productId, expectedVersion, nowUtc, out _);

    public static Payload? TryValidate(string? token, string secret, string productId, long expectedVersion, DateTimeOffset nowUtc, out string? failure)
    {
        failure = null;
        if (string.IsNullOrWhiteSpace(token)) { failure = "token ausente"; return null; }
        var parts = token.Split('.');
        if (parts.Length != 2) { failure = $"formato ({parts.Length} partes)"; return null; }
        var (body, sig) = (parts[0], parts[1]);
        if (!CryptographicOperations.FixedTimeEquals(
                Sign(body, secret), Convert.FromBase64String(Pad(sig))))
        { failure = "assinatura"; return null; }

        var payload = JsonSerializer.Deserialize<Payload>(Decode(body));
        if (payload is null) { failure = "payload ilegível"; return null; }
        if (payload.ExpUnixSeconds < nowUtc.ToUnixTimeSeconds()) { failure = $"expirado ({payload.ExpUnixSeconds})"; return null; }
        if (!string.Equals(payload.ProductId, productId, StringComparison.Ordinal)) { failure = $"product_id ({payload.ProductId} != {productId})"; return null; }
        if (payload.ExpectedVersion != expectedVersion) { failure = $"version ({payload.ExpectedVersion} != {expectedVersion})"; return null; }
        if (string.IsNullOrWhiteSpace(payload.Sub)) { failure = "sub vazio"; return null; }
        return payload;
    }

    private static string Serialize(Payload p) => ToB64(
        JsonSerializer.Serialize(new { sub = p.Sub, product_id = p.ProductId, expected_version = p.ExpectedVersion, exp = p.ExpUnixSeconds }));

    private static byte[] Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
    }

    private static string ToB64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=');
    private static string Pad(string b64) => b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
    private static string Decode(string b64) => Encoding.UTF8.GetString(Convert.FromBase64String(Pad(b64)));
}
