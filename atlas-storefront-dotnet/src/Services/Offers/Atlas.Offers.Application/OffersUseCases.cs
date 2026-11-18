namespace Atlas.Offers.Application;

using Atlas.Offers.Domain;

public sealed record PublishOfferCommand(
    string RequestId,
    string ActorSub,
    string SkuId,
    string SellerId,
    string MarketId,
    string Channel,
    string Currency,
    long PriceMinor,
    long ListPriceMinor,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset ValidToUtc,
    string CorrelationId);

public sealed record PublishOfferOutcome(bool Accepted, OfferId OfferId, long Version, string? Reason);

public sealed record UpdateOfferPriceCommand(
    string RequestId, string ActorSub, OfferId OfferId, long ExpectedVersion, long PriceMinor, string CorrelationId);
public sealed record UpdateOfferPriceOutcome(bool Accepted, long Version, string? EventId, string? Reason);

public sealed record DeactivateOfferOutcome(bool Accepted, long Version, string? EventId, string? Reason);

public sealed record OfferSummary(
    Guid Id, string SkuId, string SellerId, string MarketId, string Channel,
    string Currency, long PriceMinor, long ListPriceMinor, long Version, DateTimeOffset ValidToUtc);

/// <summary>Port do Offers (adapter Mongo em Infrastructure).</summary>
public interface IOffersStore
{
    Task<PublishOfferOutcome> PublishInTransactionAsync(PublishOfferCommand command, CancellationToken ct);
    Task<UpdateOfferPriceOutcome> UpdatePriceInTransactionAsync(UpdateOfferPriceCommand command, CancellationToken ct);
    Task<DeactivateOfferOutcome> DeactivateInTransactionAsync(
        string requestId, string actorSub, OfferId offerId, long expectedVersion, string correlationId, CancellationToken ct);
    Task<(IReadOnlyList<OfferSummary> Items, long Total)> ListActiveAsync(
        string? marketId, string? skuId, int page, int pageSize, CancellationToken ct);
}

public sealed class PublishOfferHandler(IOffersStore store)
{
    public Task<PublishOfferOutcome> HandleAsync(PublishOfferCommand command, CancellationToken ct) =>
        store.PublishInTransactionAsync(command, ct);
}

public sealed class UpdateOfferPriceHandler(IOffersStore store)
{
    public Task<UpdateOfferPriceOutcome> HandleAsync(UpdateOfferPriceCommand command, CancellationToken ct) =>
        store.UpdatePriceInTransactionAsync(command, ct);
}

public sealed class DeactivateOfferHandler(IOffersStore store)
{
    public Task<DeactivateOfferOutcome> HandleAsync(
        string requestId, string actorSub, OfferId offerId, long expectedVersion, string correlationId, CancellationToken ct) =>
        store.DeactivateInTransactionAsync(requestId, actorSub, offerId, expectedVersion, correlationId, ct);
}
