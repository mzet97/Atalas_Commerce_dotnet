namespace Atlas.Offers.Api;

public sealed record DevPublishOfferRequest(
    string SkuId, string MarketId, string Currency, long PriceMinor,
    long? ListPriceMinor, string? SellerId, string? Channel,
    int? ValidDays, string? RequestId, string? ActorSub);
