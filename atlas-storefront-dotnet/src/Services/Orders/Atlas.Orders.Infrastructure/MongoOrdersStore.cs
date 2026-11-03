namespace Atlas.Orders.Infrastructure;

using Atlas.Orders.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

/// <summary>Store Mongo do Orders owner (ADR-0002): aggregate + outbox NA mesma transação.</summary>
public sealed partial class MongoOrdersStore
{
    private static readonly bool _guid = RegisterGuid();
    private static bool RegisterGuid()
    {
        try { BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); }
        catch (BsonSerializationException) { }
        return true;
    }

    private readonly IMongoDatabase _db;

    public MongoOrdersStore(IMongoDatabase db) => _db = db;

    public static MongoOrdersStore Create(string connectionString, string databaseName) =>
        new(new MongoClient(connectionString).GetDatabase(databaseName));

    private IMongoCollection<OrderDoc> Orders() => _db.GetCollection<OrderDoc>("orders.orders");
    private IMongoCollection<OutboxDoc> Outbox() => _db.GetCollection<OutboxDoc>("orders.outbox");

    /// <summary>Insert-only no write path: Order é imutável após Place; confirmações são update de estado+version.</summary>
    public async Task InsertAsync(Order order, CancellationToken ct)
    {
        try
        {
            await Orders().InsertOneAsync(OrderDoc.From(order), cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            // retry de transação transiente re-executa o callback — insert deve ser idempotente
        }
    }

    /// <summary>Transição de estado + evento outbox na MESMA transação (guard: state anterior + version).</summary>
    public async Task<bool> ApplyTransitionAsync(
        Guid orderId, OrderState expectedState, OrderState newState,
        string? paymentReference, string eventType, string reason, string correlationId, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var filter = Builders<OrderDoc>.Filter.And(
                Builders<OrderDoc>.Filter.Eq(o => o.Id, orderId),
                Builders<OrderDoc>.Filter.Eq(o => o.State, expectedState));
            var update = Builders<OrderDoc>.Update
                .Set(o => o.State, newState)
                .Set(o => o.PaymentReference, paymentReference)
                .Inc(o => o.Version, 1);
            var result = await Orders().UpdateOneAsync(s, filter, update, cancellationToken: token);
            if (result.ModifiedCount != 1) return false; // duplicado/fora de ordem: NÃO avança (INV-ORDER-002)

            await Outbox().InsertOneAsync(s, OutboxDoc.DomainEvent(
                Guid.NewGuid(), eventType, orderId.ToString(), correlationId,
                new BsonDocument { ["order_id"] = orderId.ToString(), ["reason"] = reason ?? "" }), cancellationToken: token);
            return true;
        }, cancellationToken: ct);
    }

    // ---- documentos ----
    public sealed class OrderDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string CustomerId { get; set; } = default!;
        public OrderState State { get; set; }
        public long TotalMinor { get; set; }
        public string Currency { get; set; } = default!;
        public string? PaymentReference { get; set; }
        public long Version { get; set; } = 1;
        public List<LineDoc> Lines { get; set; } = [];

        public sealed record LineDoc(string SkuId, string OfferId, int Quantity, long UnitPriceMinor, long LineTotalMinor);

        public static OrderDoc From(Order o) => new()
        {
            Id = o.Id.Value,
            CustomerId = o.CustomerId,
            State = o.State,
            TotalMinor = o.Total.AmountMinor,
            Currency = o.Total.Currency,
            PaymentReference = o.PaymentReference,
            Version = 1,
            Lines = [.. o.Lines.Select(l => new LineDoc(l.SkuId, l.OfferId, l.Quantity, l.UnitPriceMinor.AmountMinor, l.LineTotal.AmountMinor))],
        };
    }

    public sealed class OutboxDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string EventType { get; set; } = default!;
        public string AggregateId { get; set; } = default!;
        public long AggregateVersion { get; set; } = 1;
        public string Topic { get; set; } = "atlas.orders.v1";
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
