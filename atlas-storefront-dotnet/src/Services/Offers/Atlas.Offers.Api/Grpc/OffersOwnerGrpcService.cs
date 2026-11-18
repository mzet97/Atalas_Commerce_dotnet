using Atlas.Offers.Application;
using Atlas.Contracts.Offers.V1;
using Grpc.Core;

namespace Atlas.Offers.Api.Grpc;

/// <summary>
/// Owner gRPC do Offers (M2+/fatia preços): recebe comandos do Control já autenticados
/// e REVALIDA no domínio/store antes de persistir — nunca bypass.
/// </summary>
public sealed class OffersOwnerGrpcService(
    PublishOfferHandler publishHandler,
    UpdateOfferPriceHandler updateHandler,
    DeactivateOfferHandler deactivateHandler,
    IOffersStore store) : OffersOwnerService.OffersOwnerServiceBase
{
    public override async Task<PublishOfferResponse> PublishOffer(PublishOfferRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.SkuId) || string.IsNullOrWhiteSpace(request.MarketId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "sku_id e market_id obrigatórios"));
        if (string.IsNullOrWhiteSpace(request.ActorSub))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "actor_sub obrigatório"));
        var outcome = await publishHandler.HandleAsync(new PublishOfferCommand(
            RequestId: string.IsNullOrWhiteSpace(request.RequestId) ? $"off-{Guid.NewGuid():N}" : request.RequestId,
            ActorSub: request.ActorSub,
            SkuId: request.SkuId,
            SellerId: string.IsNullOrWhiteSpace(request.SellerId) ? "atlas-1p" : request.SellerId,
            MarketId: request.MarketId,
            Channel: string.IsNullOrWhiteSpace(request.Channel) ? "web" : request.Channel,
            Currency: request.Currency,
            PriceMinor: request.PriceMinor,
            ListPriceMinor: request.ListPriceMinor <= 0 ? request.PriceMinor : request.ListPriceMinor,
            ValidFromUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            ValidToUtc: DateTimeOffset.UtcNow.AddDays(365),
            CorrelationId: request.CorrelationId), context.CancellationToken);
        return outcome.Accepted
            ? new PublishOfferResponse { Accepted = true, OfferId = outcome.OfferId.Value.ToString(), AggregateVersion = outcome.Version }
            : new PublishOfferResponse { Accepted = false, Reason = outcome.Reason ?? "" };
    }

    public override async Task<UpdateOfferPriceResponse> UpdateOfferPrice(UpdateOfferPriceRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OfferId, out var guid))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"offer_id inválido: '{request.OfferId}'"));
        var outcome = await updateHandler.HandleAsync(new UpdateOfferPriceCommand(
            request.RequestId, request.ActorSub, new Atlas.Offers.Domain.OfferId(guid),
            request.ExpectedVersion, request.PriceMinor, request.CorrelationId), context.CancellationToken);
        return outcome.Accepted
            ? new UpdateOfferPriceResponse { Accepted = true, AggregateVersion = outcome.Version, EventId = outcome.EventId ?? "" }
            : outcome.Reason == "oferta inexistente"
                ? throw new RpcException(new Status(StatusCode.NotFound, outcome.Reason))
                : new UpdateOfferPriceResponse { Accepted = false, AggregateVersion = outcome.Version, Reason = outcome.Reason ?? "" };
    }

    public override async Task<DeactivateOfferResponse> DeactivateOffer(DeactivateOfferRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OfferId, out var guid))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"offer_id inválido: '{request.OfferId}'"));
        var outcome = await deactivateHandler.HandleAsync(
            request.RequestId, request.ActorSub, new Atlas.Offers.Domain.OfferId(guid),
            request.ExpectedVersion, request.CorrelationId, context.CancellationToken);
        return outcome.Accepted
            ? new DeactivateOfferResponse { Accepted = true, AggregateVersion = outcome.Version, EventId = outcome.EventId ?? "" }
            : outcome.Reason == "oferta inexistente"
                ? throw new RpcException(new Status(StatusCode.NotFound, outcome.Reason))
                : new DeactivateOfferResponse { Accepted = false, AggregateVersion = outcome.Version, Reason = outcome.Reason ?? "" };
    }

    public override async Task<ListActiveOffersResponse> ListActiveOffers(ListActiveOffersRequest request, ServerCallContext context)
    {
        var (items, total) = await store.ListActiveAsync(
            string.IsNullOrWhiteSpace(request.MarketId) ? null : request.MarketId,
            string.IsNullOrWhiteSpace(request.SkuId) ? null : request.SkuId,
            request.Page <= 0 ? 1 : request.Page,
            request.PageSize is <= 0 or > 200 ? 50 : request.PageSize,
            context.CancellationToken);
        var response = new ListActiveOffersResponse { Total = total };
        response.Items.AddRange(items.Select(i => new OfferView
        {
            OfferId = i.Id.ToString(),
            SkuId = i.SkuId,
            SellerId = i.SellerId,
            MarketId = i.MarketId,
            Channel = i.Channel,
            Currency = i.Currency,
            PriceMinor = i.PriceMinor,
            ListPriceMinor = i.ListPriceMinor,
            AggregateVersion = i.Version,
            ValidTo = i.ValidToUtc.ToString("o"),
        }));
        return response;
    }
}
