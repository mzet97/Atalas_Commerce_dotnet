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

/// <summary>Port do Offers (adapter Mongo em Infrastructure).</summary>
public interface IOffersStore
{
    Task<PublishOfferOutcome> PublishInTransactionAsync(PublishOfferCommand command, CancellationToken ct);
}

public sealed class PublishOfferHandler(IOffersStore store)
{
    public Task<PublishOfferOutcome> HandleAsync(PublishOfferCommand command, CancellationToken ct) =>
        store.PublishInTransactionAsync(command, ct);
}
