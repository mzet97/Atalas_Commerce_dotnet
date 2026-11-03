namespace Atlas.ShoppingAssistant.Domain;

/// <summary>
/// Proposta de carrinho INERTE (SPEC-007 §9/INV-AI-001): a IA NUNCA muta o carrinho —
/// produz proposta que só vira mutação após consent artifact emitido em UI confiável.
/// </summary>
public sealed record CartProposal(
    string SkuId, string Title, int Quantity, long UnitPriceMinor, string Currency, string Reason);

/// <summary>
/// Consent artifact (ADR-0009): HMAC sobre {subject|skuId|quantity|nonce|exp}.
/// Token para subject/payload diverso = REJEITADO (INV-AI-001: consentimento inviolável).
/// </summary>
public sealed class ConsentArtifact
{
    public static string Issue(string subject, string skuId, int quantity, TimeSpan ttl, string secret, DateTimeOffset nowUtc, out string nonce)
    {
        nonce = Guid.NewGuid().ToString("N");
        var payload = $"{subject}|{skuId}|{quantity}|{nonce}|{nowUtc.Add(ttl).ToUnixTimeSeconds()}";
        var sig = Convert.ToHexString(
            new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret))
                .ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return $"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload)).TrimEnd('=')}.{sig}";
    }

    public static (string Subject, string SkuId, int Quantity)? Validate(
        string? token, string secret, string expectedSubject, string expectedSkuId, int expectedQuantity, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 2) return null;
        string payload;
        try { payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Pad(parts[0]))); }
        catch (FormatException) { return null; }
        var seg = payload.Split('|');
        if (seg.Length != 5) return null;
        var expected = Convert.ToHexString(
            new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret))
                .ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(parts[1]), System.Text.Encoding.UTF8.GetBytes(expected)))
            return null;
        if (long.Parse(seg[4], System.Globalization.CultureInfo.InvariantCulture) < nowUtc.ToUnixTimeSeconds()) return null;                          // expirado
        if (!string.Equals(seg[0], expectedSubject, StringComparison.Ordinal)) return null;        // outro subject
        if (!string.Equals(seg[1], expectedSkuId, StringComparison.Ordinal)) return null;          // payload diverso
        if (int.Parse(seg[2], System.Globalization.CultureInfo.InvariantCulture) != expectedQuantity) return null;
        return (seg[0], seg[1], int.Parse(seg[2], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string Pad(string s) => s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
}
