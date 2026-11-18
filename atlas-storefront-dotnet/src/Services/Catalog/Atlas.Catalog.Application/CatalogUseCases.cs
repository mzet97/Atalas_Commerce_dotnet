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
}

/// <summary>Linha da grade de produtos (operações do Control — ERP master data).</summary>
public sealed record CatalogDraftSummary(
    Guid Id, string Status, long Version, string MarketId,
    IReadOnlyList<(string Locale, string Title)> Titles, int SkuCount, string CreatedBy);

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
