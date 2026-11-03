namespace Atlas.Offers.Infrastructure;

using Atlas.Offers.Application;
using Atlas.Offers.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

/// <summary>
/// Adapter MongoDB do Offers (ADR-0002/0003). Unicidade de "uma oferta ATIVA por escopo"
/// (sku|seller|market|channel — REQ-OFF-001) garantida por índice ÚNICO PARCIAL
/// (status == Active) dentro da transação de replica set; outbox na mesma escrita.
/// </summary>
public sealed class MongoOffersStore : IOffersStore
{
    private static readonly bool _guid = RegisterGuid();
    private static bool RegisterGuid()
    {
        try { BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); }
        catch (BsonSerializationException) { }
        return true;
    }

    private readonly IMongoDatabase _db;

    public MongoOffersStore(IMongoDatabase db) => _db = db;

    public static MongoOffersStore Create(string connectionString, string databaseName)
    {
        var store = new MongoOffersStore(new MongoClient(connectionString).GetDatabase(databaseName));
        store.EnsureIndexes();
        return store;
    }

    private void EnsureIndexes()
    {
        Offers().Indexes.CreateOne(new CreateIndexModel<OfferDoc>(
            Builders<OfferDoc>.IndexKeys.Ascending(o => o.ScopeKey),
            new CreateIndexOptions<OfferDoc>
            {
                Unique = true,
                // índice único PARCIAL: só documentos ATIVOS participam da unicidade de escopo
                PartialFilterExpression = Builders<OfferDoc>.Filter.Eq(o => o.Status, OfferStatus.Active),
                Name = "uq_active_scope",
            }));
    }

    private IMongoCollection<OfferDoc> Offers() => _db.GetCollection<OfferDoc>("offers.offers");
    private IMongoCollection<OutboxDoc> Outbox() => _db.GetCollection<OutboxDoc>("offers.outbox");

    public async Task<PublishOfferOutcome> PublishInTransactionAsync(PublishOfferCommand command, CancellationToken ct)
    {
        Offer offer;
        try
        {
            offer = Offer.Create(
                command.SkuId, command.SellerId, command.MarketId, command.Channel,
                Money.Create(command.PriceMinor, command.Currency),
                Money.Create(command.ListPriceMinor, command.Currency),
                command.ValidFromUtc, command.ValidToUtc);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return new PublishOfferOutcome(false, default, 0, ex.Message); // regra de domínio = outcome rejeitado
        }

        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        try
        {
            return await session.WithTransactionAsync(async (s, token) =>
            {
                // idempotência por request_id
                var existing = await Offers().Find(s, o => o.RequestId == command.RequestId).FirstOrDefaultAsync(cancellationToken: token);
                if (existing is not null)
                    return new PublishOfferOutcome(true, new OfferId(existing.Id), existing.Version, "idempotente por request_id");

                await Offers().InsertOneAsync(s, OfferDoc.From(offer, command.RequestId), cancellationToken: token);

                var activated = await Offers().UpdateOneAsync(s,
                    o => o.Id == offer.Id.Value && o.ValidFromUtc <= DateTimeOffset.UtcNow && o.ValidToUtc > DateTimeOffset.UtcNow,
                    Builders<OfferDoc>.Update.Set(o => o.Status, OfferStatus.Active).Inc(o => o.Version, 1),
                    cancellationToken: token);
                if (activated.ModifiedCount != 1)
                    return new PublishOfferOutcome(false, offer.Id, 0, "fora da janela de validade (activate negado)");

                await Outbox().InsertOneAsync(s, OutboxDoc.DomainEvent(
                    Guid.NewGuid(), "Offer.PublishedV1", offer.Id.Value.ToString(), command.CorrelationId,
                    new BsonDocument
                    {
                        ["offer_id"] = offer.Id.Value.ToString(),
                        ["scope_key"] = offer.ScopeKey(),
                        ["currency"] = command.Currency,
                        ["price_minor"] = command.PriceMinor,
                    }), cancellationToken: token);

                return new PublishOfferOutcome(true, offer.Id, offer.Version, null);
            }, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            // índice único de escopo ativo: segunda oferta ativa na mesma chave é rejeitada (TEST-002-003)
            return new PublishOfferOutcome(false, offer.Id, 0, "escopo já possui oferta ativa");
        }
    }

    // ---- documentos ----
    public sealed class OfferDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string RequestId { get; set; } = default!;
        public string ScopeKey { get; set; } = default!;
        public string SkuId { get; set; } = default!;
        public string SellerId { get; set; } = default!;
        public string MarketId { get; set; } = default!;
        public string Channel { get; set; } = default!;
        public string Currency { get; set; } = default!;
        public long PriceMinor { get; set; }
        public long ListPriceMinor { get; set; }
        public DateTimeOffset ValidFromUtc { get; set; }
        public DateTimeOffset ValidToUtc { get; set; }
        public OfferStatus Status { get; set; }
        public long Version { get; set; } = 1;

        public static OfferDoc From(Offer o, string requestId) => new()
        {
            Id = o.Id.Value,
            RequestId = requestId,
            ScopeKey = o.ScopeKey(),
            SkuId = o.SkuId,
            SellerId = o.SellerId,
            MarketId = o.MarketId,
            Channel = o.Channel,
            Currency = o.Price.Currency,
            PriceMinor = o.Price.AmountMinor,
            ListPriceMinor = o.ListPrice.AmountMinor,
            ValidFromUtc = o.ValidFromUtc,
            ValidToUtc = o.ValidToUtc,
            Status = o.Status,
            Version = o.Version,
        };
    }

    public sealed class OutboxDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string EventType { get; set; } = default!;
        public string AggregateId { get; set; } = default!;
        public long AggregateVersion { get; set; } = 1;
        public string Topic { get; set; } = "atlas.offers.v1";
        public bool Published { get; set; }
        public string CorrelationId { get; set; } = default!;
        public BsonDocument Payload { get; set; } = default!;
        public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public static OutboxDoc DomainEvent(Guid eventId, string eventType, string aggregateId, string correlationId, BsonDocument payload) => new()
        {
            Id = eventId, EventType = eventType, AggregateId = aggregateId, CorrelationId = correlationId, Payload = payload,
        };
    }
}
