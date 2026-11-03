namespace Atlas.Catalog.Domain;

/// <summary>Identidade forte de Product (UUID; chaves naturais nunca substituem o ID — glossário).</summary>
public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());
    public bool IsValid => Value != Guid.Empty;
    public override string ToString() => Value.ToString("N");
}

/// <summary>Identidade forte de SKU (variação estocável; imutável após criação — REQ-CAT-005).</summary>
public readonly record struct SkuId(Guid Value)
{
    public static SkuId New() => new(Guid.NewGuid());
    public bool IsValid => Value != Guid.Empty;
    public override string ToString() => Value.ToString("N");
}

/// <summary>Locale suportado no MVP (i18n — discovery 08; extensível por configuração no M2+).</summary>
public readonly record struct Locale(string Value)
{
    private static readonly HashSet<string> Supported = ["pt-BR", "en-US", "es-ES"];
    public bool IsSupported => Supported.Contains(Value);
    public static bool TryParse(string? raw, out Locale locale)
    {
        locale = new Locale(raw ?? string.Empty);
        return locale.IsSupported;
    }
}

/// <summary>Texto localizado por locale (fallback explícito é decisão da aplicação — CAT-01).</summary>
public sealed record LocalizedText(Locale Locale, string Text)
{
    public static LocalizedText Create(string locale, string text)
    {
        if (!Locale.TryParse(locale, out var parsed))
            throw new ArgumentException($"Locale não suportado: '{locale}'", nameof(locale));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Texto localizado não pode ser vazio", nameof(text));
        return new LocalizedText(parsed, text);
    }
}

/// <summary>Lifecycle do Product (UC-005: publicar exige aprovação prévia — enforced no owner no M2).</summary>
public enum ProductStatus
{
    Draft = 0,
    PendingApproval = 1,
    Published = 2,
    Unpublished = 3,
}

/// <summary>
/// Rascunho do agregado Product (M1: esqueleto de domínio com invariantes locais verificáveis;
/// persistência/outbox/comando gRPC chegam na SPEC-002 — sem stubs vazios antecipados).
/// </summary>
public sealed partial class ProductDraft
{
    public ProductId Id { get; }
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    private readonly Dictionary<string, LocalizedText> _titles = [];
    private readonly Dictionary<string, string> _attributes = [];

    private ProductDraft(ProductId id) => Id = id;

    public static ProductDraft Create(ProductId? id = null)
    {
        var pid = id ?? ProductId.New();
        if (!pid.IsValid) throw new ArgumentException("ProductId inválido", nameof(id));
        return new ProductDraft(pid);
    }

    public IReadOnlyCollection<LocalizedText> Titles => [.. _titles.Values];

    public ProductDraft WithTitle(string locale, string text)
    {
        var lt = LocalizedText.Create(locale, text);
        _titles[lt.Locale.Value] = lt;
        return this;
    }

    public ProductDraft WithAttribute(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Chave de atributo obrigatória");
        _attributes[key] = value;
        return this;
    }

    public IReadOnlyDictionary<string, string> Attributes => _attributes;

    public LocalizedText? TitleFor(Locale locale) =>
        _titles.TryGetValue(locale.Value, out var t) ? t : null;

    /// <summary>Submete para aprovação: exige ao menos um título (regra mínima do UC-005).</summary>
    public void SubmitForApproval()
    {
        if (Status != ProductStatus.Draft)
            throw new InvalidOperationException($"Transição inválida: {Status} → PendingApproval");
        if (_titles.Count == 0)
            throw new InvalidOperationException("Produto sem título localizado não pode ser submetido");
        Status = ProductStatus.PendingApproval;
    }
}
