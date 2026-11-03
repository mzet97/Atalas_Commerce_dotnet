namespace Atlas.Catalog.Domain;

/// <summary>
/// Extensão do agregado ProductDraft para a fatia da SPEC-002: SKUs imutáveis (REQ-CAT-005),
/// versionamento para concorrência otimista e publicação com pré-condições do UC-005.
/// A verificação do approval_token acontece na camada de aplicação/infra ANTES de chamar
/// Publish (ADR-0001: owner revalida); o domínio exige o estado PendingApproval.
/// </summary>
public sealed partial class ProductDraft
{
    private readonly List<SkuVariant> _skus = [];

    public long Version { get; private set; } = 1;
    public IReadOnlyCollection<SkuVariant> Skus => _skus;

    /// <summary>Adiciona SKU — permitido apenas antes da publicação; SKUs são imutáveis (sem update/remove).</summary>
    public ProductDraft AddSku(string skuCode, IReadOnlyDictionary<string, string> variantAttributes)
    {
        if (Status is ProductStatus.Published or ProductStatus.Unpublished)
            throw new InvalidOperationException("SKUs não podem mudar após publicação (REQ-CAT-005)");
        if (string.IsNullOrWhiteSpace(skuCode)) throw new ArgumentException("skuCode obrigatório", nameof(skuCode));
        if (_skus.Any(s => s.Code.Equals(skuCode, StringComparison.Ordinal)))
            throw new InvalidOperationException($"SKU duplicado no produto: {skuCode}");
        _skus.Add(new SkuVariant(SkuId.New(), skuCode, variantAttributes));
        Version++;
        return this;
    }

    /// <summary>Publica o produto (UC-005): exige aprovação prévia (estado), ≥1 título e ≥1 SKU.</summary>
    public ProductPublishedV1 Publish(string approvedBy, DateTimeOffset occurredAtUtc)
    {
        if (Status != ProductStatus.PendingApproval)
            throw new InvalidOperationException($"Publicação exige aprovação prévia (estado atual: {Status})");
        if (_titles.Count == 0) throw new InvalidOperationException("Produto sem título localizado");
        if (_skus.Count == 0) throw new InvalidOperationException("Produto sem SKU estocável");
        if (string.IsNullOrWhiteSpace(approvedBy)) throw new ArgumentException("Aprovador obrigatório", nameof(approvedBy));

        Status = ProductStatus.Published;
        Version++;
        return new ProductPublishedV1(
            ProductId: Id,
            ApprovedBy: approvedBy,
            SkuCodes: [.. _skus.Select(s => s.Code)],
            AggregateVersion: Version,
            OccurredAtUtc: occurredAtUtc);
    }
}

/// <summary>Variação estocável imutável (blueprint #02 §2: SKU diferencia unidades estocáveis).</summary>
public sealed record SkuVariant(SkuId Id, string Code, IReadOnlyDictionary<string, string> VariantAttributes);

/// <summary>Evento de domínio: envelope INV-EVT-001 aplicado na camada Application.</summary>
public sealed record ProductPublishedV1(
    ProductId ProductId,
    string ApprovedBy,
    IReadOnlyList<string> SkuCodes,
    long AggregateVersion,
    DateTimeOffset OccurredAtUtc);

public sealed partial class ProductDraft
{
    /// <summary>Reidratação a partir do write model (Mongo) — não revalida história.</summary>
    public static ProductDraft Hydrate(
        ProductId id,
        ProductStatus status,
        long version,
        IReadOnlyList<(string Locale, string Text)> titles,
        IReadOnlyList<(SkuId SkuId, string Code)> skus,
        IReadOnlyDictionary<string, string> attributes)
    {
        var draft = new ProductDraft(id);
        draft.Status = status;
        draft.Version = version;
        foreach (var (locale, text) in titles)
        {
            var lt = LocalizedText.Create(locale, text);
            draft._titles[lt.Locale.Value] = lt;
        }
        foreach (var (skuId, code) in skus)
            draft._skus.Add(new SkuVariant(skuId, code, new Dictionary<string, string>()));
        foreach (var kv in attributes)
            draft._attributes[kv.Key] = kv.Value;
        return draft;
    }
}
