namespace Atlas.ShoppingAssistant.Api.Tools;

using System.ComponentModel;
using System.Text.Json;

/// <summary>
/// ALLOWLIST DE TOOLS (SPEC-007 §9): SOMENTE leitura — SearchCatalog, GetProductDetails,
/// GetInventoryHint, GetCustomerCart e ProposeCartChange (retorna proposta INERTE).
/// NÃO EXISTEM tools de mutação/pagamento/IAM — guardrail em CÓDIGO, não em prompt.
/// Outputs de tools são UNTRUSTED (podem conter injection de catálogo/reviews).
/// </summary>
public sealed class CatalogTools(HttpClient http, string marketplaceUrl)
{
    private readonly string _base = marketplaceUrl.TrimEnd('/');

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct)
    {
        var res = await http.GetAsync(_base + path, ct);
        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    [Description("Busca produtos publicados por termo e mercado. Retorna títulos e mercados (grounded).")]
    public async Task<string> SearchCatalog(
        [Description("Termo de busca")] string query,
        [Description("Mercado (BR, US ou UE) — opcional")] string? market,
        CancellationToken ct)
    {
        var q = Uri.EscapeDataString(query ?? "");
        using var data = await GetJsonAsync($"/api/catalog/search?q={q}{(string.IsNullOrWhiteSpace(market) ? "" : $"&market={Uri.EscapeDataString(market)}")}", ct);
        var items = data.RootElement.GetProperty("items");
        var out_ = new System.Text.StringBuilder();
        foreach (var i in items.EnumerateArray().Take(5))
            out_.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- {i.GetProperty("title").GetString()} (mercado {i.GetProperty("marketId").GetString()}, id {i.GetProperty("productId").GetString()})");
        return out_.Length == 0 ? "nenhum produto encontrado" : out_.ToString();
    }

    [Description("Detalhes de um produto: título, mercado, SKUs e oferta ativa com preço na moeda do mercado (grounded com quoted_at).")]
    public async Task<string> GetProductDetails([Description("ProductId")] string productId, CancellationToken ct)
    {
        var d = await GetJsonAsync($"/api/catalog/products/{productId}", ct);
        var offers = d.RootElement.TryGetProperty("offers", out var o) && o.GetArrayLength() > 0
            ? $"oferta: {o[0].GetProperty("priceMinor").GetInt64()} minor {o[0].GetProperty("currency").GetString()} (quoted_at=agora, TTL 60s)"
            : "sem oferta ativa";
        return $"{d.RootElement.GetProperty("title").GetString()} | mercado {d.RootElement.GetProperty("marketId").GetString()} | skus {string.Join(",", d.RootElement.GetProperty("skus").EnumerateArray().Select(s => s.GetString()))} | {offers}";
    }

    [Description("Prévia de disponibilidade de um SKU (hint NUNCA confirma reserva — o checkout reserva no owner).")]
    public async Task<string> GetInventoryHint([Description("Código do SKU")] string skuId, CancellationToken ct)
    {
        var d = await GetJsonAsync($"/api/catalog/search?q={Uri.EscapeDataString(skuId)}", ct);
        return d.RootElement.GetProperty("total").GetInt64() > 0
            ? $"hint: SKU {skuId} tem oferta publicada (disponibilidade real só no checkout)"
            : $"hint: sku {skuId} sem oferta publicada";
    }

    [Description("Propõe adicionar um item ao carrinho. NÃO aplica nada — devolve proposta inerte para confirmação do usuário.")]
    public Task<string> ProposeCartChange(
        [Description("SKU")] string skuId,
        [Description("Título do produto")] string title,
        [Description("Quantidade")] int quantity,
        [Description("Preço unitário em minor units (vem da oferta, nunca inventado)")] long unitPriceMinor,
        [Description("Moeda da oferta")] string currency,
        CancellationToken ct)
        => Task.FromResult($"PROPOSTA INERTE: {{skuId:{skuId}, qtd:{quantity}, preço:{unitPriceMinor} {currency}}} — aguarda consentimento do usuário.");
}
