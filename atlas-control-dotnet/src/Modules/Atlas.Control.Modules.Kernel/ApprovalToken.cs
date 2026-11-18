using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Control.Modules.Kernel;

/// <summary>
/// Emissor HMAC do approval_token — MESMO algoritmo validado pelos owners
/// (Atlas.Catalog: ApprovalToken.TryValidate; formato compartilhado com o publish).
/// Fica no Kernel (compartilhado, sem arestas entre módulos — ADRs 0001/0012).
/// Duplicado de propósito no repo storefront até o pacote de contratos publicado.
/// </summary>
public static class ApprovalTokenIssuer
{
    public sealed record Payload(
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("product_id")] string ProductId,
        [property: JsonPropertyName("expected_version")] long ExpectedVersion,
        [property: JsonPropertyName("exp")] long ExpUnixSeconds);

    public static string Issue(Payload payload, string secret)
    {
        var json = JsonSerializer.Serialize(payload);
        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=');
        var sig = Convert.ToBase64String(Sign(body, secret)).TrimEnd('=');
        return $"{body}.{sig}";
    }

    private static byte[] Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
    }
}
