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
}

public sealed record PublishOutcome(bool Accepted, long Version, string? EventId, string? Reason);

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
