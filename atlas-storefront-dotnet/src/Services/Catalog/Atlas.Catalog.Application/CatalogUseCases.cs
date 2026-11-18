namespace Atlas.Catalog.Application;

using Atlas.Catalog.Domain;

public sealed record UpsertDraftCommand(
    string RequestId,
    string ActorSub,
    string MarketId,
    IReadOnlyList<(string Locale, string Title)> Titles,
    IReadOnlyList<string> SkuCodes);

public enum UpsertDraftStatus { Created, Updated, Rejected }

public sealed record UpsertDraftResult(UpsertDraftStatus Status, ProductId ProductId, long Version, string? Reason = null);

/// <summary>Port de persistência do Catalog (adapter Mongo em Infrastructure; DIP).</summary>
public interface ICatalogStore
{
    Task<UpsertDraftResult> UpsertDraftInTransactionAsync(
        UpsertDraftCommand command, CancellationToken ct);
    Task<PublishOutcome> SubmitForApprovalAsync(ProductId productId, long expectedVersion, CancellationToken ct);
    Task<PublishOutcome> PublishInTransactionAsync(
        ProductId productId, long expectedVersion, string approvedBy, string marketId, string correlationId, CancellationToken ct);
    Task<ProductDraft?> GetAsync(ProductId productId, CancellationToken ct);
    Task<(IReadOnlyList<CatalogDraftSummary> Items, long Total)> ListDraftsAsync(
        string? status, string? market, string? search, int page, int pageSize, CancellationToken ct);
    Task<PublishOutcome> UpdateDraftAsync(
        ProductId productId, long expectedVersion, IReadOnlyList<(string Locale, string Title)> titles,
        IReadOnlyDictionary<string, string> attributes, CancellationToken ct);
    Task<(bool Accepted, string? Reason)> DeleteDraftAsync(ProductId productId, CancellationToken ct);
    Task<PublishOutcome> UnpublishInTransactionAsync(
        ProductId productId, long expectedVersion, string actorSub, string reason, string correlationId, CancellationToken ct);
    Task<PublishOutcome> AttachImageAsync(
        ProductId productId, long expectedVersion, string objectKey, string contentType, long sizeBytes,
        string actorSub, string correlationId, CancellationToken ct);
    Task<PublishOutcome> DetachImageAsync(
        ProductId productId, long expectedVersion, string objectKey, string actorSub, string correlationId, CancellationToken ct);
}

/// <summary>Linha da grade de produtos (operações do Control — ERP master data).</summary>
public sealed record CatalogDraftSummary(
    Guid Id, string Status, long Version, string MarketId,
    IReadOnlyList<(string Locale, string Title)> Titles, int SkuCount, string CreatedBy,
    IReadOnlyList<(string Key, string ContentType, long SizeBytes)>? Images = null);

/// <summary>Port de mídia (S3/MinIO): bytes nunca tocam o backend.</summary>
public sealed record PresignedUpload(string UploadUrl, string ObjectKey, DateTimeOffset ExpiresAtUtc);
public sealed record ObjectMetadata(string ContentType, long SizeBytes);

public interface IMediaStorage
{
    Task<PresignedUpload> PresignPutAsync(ProductId productId, string contentType, long contentLength, CancellationToken ct);
    Task<ObjectMetadata?> HeadObjectAsync(string key, CancellationToken ct);
    Task EnsureBucketAsync(CancellationToken ct);
}

public sealed class RequestImageUploadHandler(IMediaStorage media, ICatalogStore store)
{
    public async Task<(bool Accepted, string? UploadUrl, string? ObjectKey, DateTimeOffset? ExpiresAt, string? Reason)> HandleAsync(
        ProductId productId, string actorSub, string contentType, long contentLength, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorSub))
            return (false, null, null, null, "actor_sub obrigatório");
        if (await store.GetAsync(productId, ct) is null)
            return (false, null, null, null, "produto inexistente");
        PresignedUpload presigned;
        try
        {
            presigned = await media.PresignPutAsync(productId, contentType, contentLength, ct);
        }
        catch (ArgumentException ex)
        {
            return (false, null, null, null, ex.Message);
        }
        return (true, presigned.UploadUrl, presigned.ObjectKey, presigned.ExpiresAtUtc, null);
    }
}

public sealed class AttachImageHandler(IMediaStorage media, ICatalogStore store)
{
    public async Task<PublishOutcome> HandleAsync(
        ProductId productId, long expectedVersion, string actorSub, string objectKey, string correlationId, CancellationToken ct)
    {
        var expectedPrefix = $"products/{productId}/";
        if (!objectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
            return new PublishOutcome(false, 0, null, $"chave fora do produto (prefixo {expectedPrefix})");
        var meta = await media.HeadObjectAsync(objectKey, ct);
        if (meta is null)
            return new PublishOutcome(false, 0, null, "objeto não enviado (PUT no upload_url primeiro)");
        if (!Atlas.Catalog.Domain.ProductImage.AllowedContentTypes.Contains(meta.ContentType))
            return new PublishOutcome(false, 0, null, $"tipo real não permitido: '{meta.ContentType}'");
        if (meta.SizeBytes <= 0 || meta.SizeBytes > Atlas.Catalog.Domain.ProductImage.MaxSizeBytes)
            return new PublishOutcome(false, 0, null, $"tamanho real inválido: {meta.SizeBytes}");
        return await store.AttachImageAsync(productId, expectedVersion, objectKey, meta.ContentType, meta.SizeBytes, actorSub, correlationId, ct);
    }
}

public sealed record PublishOutcome(bool Accepted, long Version, string? EventId, string? Reason);

public sealed record UpdateDraftCommand(
    ProductId ProductId,
    long ExpectedVersion,
    string ActorSub,
    IReadOnlyList<(string Locale, string Title)> Titles,
    IReadOnlyDictionary<string, string> Attributes);

public sealed class UpdateDraftHandler(ICatalogStore store)
{
    public Task<PublishOutcome> HandleAsync(UpdateDraftCommand command, CancellationToken ct) =>
        store.UpdateDraftAsync(command.ProductId, command.ExpectedVersion, command.Titles, command.Attributes, ct);
}

public sealed class UpsertDraftHandler(ICatalogStore store)
{
    public Task<UpsertDraftResult> HandleAsync(UpsertDraftCommand command, CancellationToken ct) =>
        store.UpsertDraftInTransactionAsync(command, ct);
}

public sealed class PublishProductHandler(ICatalogStore store)
{
    public Task<PublishOutcome> HandleAsync(
        ProductId productId, long expectedVersion, string approvedBy, string marketId,
        string correlationId, CancellationToken ct) =>
        store.PublishInTransactionAsync(productId, expectedVersion, approvedBy, marketId, correlationId, ct);
}
