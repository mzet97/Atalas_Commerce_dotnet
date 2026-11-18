namespace Atlas.Catalog.Domain;

/// <summary>Imagem do produto (MinIO/S3): o agregado guarda só a CHAVE + metadados
/// validados no attach (HEAD do owner); bytes nunca passam pelo backend. [0] = capa.</summary>
public sealed record ProductImage(
    string Key,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc)
{
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string> { "image/jpeg", "image/png", "image/webp" };

    public const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MiB
    public const int MaxImages = 10;

    public static ProductImage Create(string key, string contentType, long sizeBytes, DateTimeOffset uploadedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Chave do objeto obrigatória", nameof(key));
        if (!AllowedContentTypes.Contains(contentType))
            throw new ArgumentException($"Tipo não permitido: '{contentType}' (jpeg/png/webp)", nameof(contentType));
        if (sizeBytes <= 0 || sizeBytes > MaxSizeBytes)
            throw new ArgumentException($"Tamanho inválido: {sizeBytes} (teto {MaxSizeBytes})", nameof(sizeBytes));
        return new ProductImage(key, contentType, sizeBytes, uploadedAtUtc);
    }

    /// <summary>Chave presa ao produto: products/{productId}/{arquivo}. Impede anexar objeto alheio.</summary>
    public static string ExpectedPrefix(ProductId id) => $"products/{id}/";

    public static string ExtensionFor(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => throw new ArgumentException($"Tipo não permitido: '{contentType}'", nameof(contentType)),
    };
}

/// <summary>Imagens do draft: anexar/desanexar em qualquer estado (só apresentação;
/// sem efeito comercial — INV-CQRS-001 não se aplica a foto). CAS por Version.</summary>
public sealed partial class ProductDraft
{
    private readonly List<ProductImage> _images = [];

    public IReadOnlyList<ProductImage> Images => _images;

    public ProductImage AttachImage(string key, string contentType, long sizeBytes, DateTimeOffset uploadedAtUtc)
    {
        var prefix = ProductImage.ExpectedPrefix(Id);
        if (!key.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException($"Chave fora do produto: '{key}' (prefixo {prefix})", nameof(key));
        if (_images.Any(i => i.Key == key))
            throw new InvalidOperationException($"Imagem já anexada: '{key}'");
        if (_images.Count >= ProductImage.MaxImages)
            throw new InvalidOperationException($"Teto de {ProductImage.MaxImages} imagens por produto");
        var image = ProductImage.Create(key, contentType, sizeBytes, uploadedAtUtc);
        _images.Add(image);
        Version++;
        return image;
    }

    public void DetachImage(string key)
    {
        var removed = _images.RemoveAll(i => i.Key == key);
        if (removed == 0) throw new InvalidOperationException($"Imagem não anexada: '{key}'");
        Version++;
    }
}

/// <summary>Evento: imagens mudaram num produto PUBLICADO — projeção atualiza capa/galeria.</summary>
public sealed record ProductImagesUpdatedV1(
    ProductId ProductId,
    IReadOnlyList<string> ImageKeys,
    long AggregateVersion,
    DateTimeOffset OccurredAtUtc);
