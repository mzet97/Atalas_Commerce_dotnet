namespace Atlas.Fulfillment.Infrastructure;

using Atlas.Fulfillment.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

public sealed class MongoFulfillmentStore
{
    public static MongoFulfillmentStore Create(string conn, string db)
    {
        // OrderId É o _id (BsonId): unicidade já garantida pelo Mongo — criar índice
        // "unique" sobre _id gera Command failed (especificação inválida p/ _id)
        return new MongoFulfillmentStore(new MongoClient(conn).GetDatabase(db));
    }

    private readonly IMongoDatabase _db;
    private MongoFulfillmentStore(IMongoDatabase db) => _db = db;
    private IMongoCollection<FulfillmentDoc> Orders() => _db.GetCollection<FulfillmentDoc>("fulfillment.orders");
    private IMongoCollection<OutboxDoc> Outbox() => _db.GetCollection<OutboxDoc>("fulfillment.outbox");

    /// <summary>
    /// Avanço com evento: CAS do estado + outbox Fulfillment.StepV1 na MESMA transação
    /// (tópico atlas.orders.v1 — o tracking do comprador acompanha Picking→Delivered).
    /// Sem dual write: sem outbox, sem avanço visível fora do owner.
    /// </summary>
    public async Task<bool> SaveWithStepEventAsync(FulfillmentOrder order, string correlationId, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var result = await Orders().ReplaceOneAsync(s,
                d => d.OrderId == order.OrderId && d.Version == order.Version - 1,
                FulfillmentDoc.From(order), cancellationToken: token);
            if (result.ModifiedCount != 1) return false;
            await Outbox().InsertOneAsync(s, OutboxDoc.StepEvent(
                order.OrderId, order.State.ToString(), order.Version, order.TrackingCode, correlationId), cancellationToken: token);
            return true;
        }, cancellationToken: ct);
    }

    /// <summary>Upsert por OrderId: criação idempotente por Order.ConfirmedV1 reentregue (req.4).</summary>
    public async Task<(FulfillmentOrder Order, bool Created)> GetOrCreateAsync(FulfillmentOrder order, CancellationToken ct)
    {
        var existing = await Orders().Find(d => d.OrderId == order.OrderId).FirstOrDefaultAsync(ct);
        if (existing is not null) return (existing.ToDomain(), false);
        try
        {
            await Orders().InsertOneAsync(FulfillmentDoc.From(order), new InsertOneOptions(), ct);
            return (order, true);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            var again = await Orders().Find(d => d.OrderId == order.OrderId).FirstAsync(ct);
            return (again.ToDomain(), false);
        }
    }

    public async Task<FulfillmentOrder?> FindAsync(Guid orderId, CancellationToken ct)
    {
        var doc = await Orders().Find(d => d.OrderId == orderId).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<bool> SaveAsync(FulfillmentOrder order, CancellationToken ct)
    {
        var result = await Orders().ReplaceOneAsync(
            d => d.OrderId == order.OrderId && d.Version == order.Version - 1,
            FulfillmentDoc.From(order), cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public sealed class FulfillmentDoc
    {
        [BsonId] [BsonRepresentation(BsonType.String)] public Guid OrderId { get; set; }
        public int State { get; set; }
        public string Carrier { get; set; } = default!;
        public string TrackingCode { get; set; } = default!;
        public long Version { get; set; }
        public List<LineDoc> Lines { get; set; } = [];
        public List<string> Steps { get; set; } = [];

        public static FulfillmentDoc From(FulfillmentOrder o) => new()
        {
            OrderId = o.OrderId, State = (int)o.State, Carrier = o.Carrier,
            TrackingCode = o.TrackingCode, Version = o.Version,
            Lines = o.Lines.Select(l => new LineDoc { SkuId = l.SkuId, Quantity = l.Quantity }).ToList(),
            Steps = [.. o.Steps],
        };
        public FulfillmentOrder ToDomain() => FulfillmentOrder.Hydrate(OrderId, (FulfillmentOrder.FulfillmentState)State, Carrier, TrackingCode, Version, Steps);
    }

    public sealed class LineDoc { public string SkuId { get; set; } = default!; public int Quantity { get; set; } }
    /// <summary>Envelope de outbox no formato do OutboxDispatcher (mesmo dos outros owners).</summary>
    public sealed class OutboxDoc
    {
        [BsonId] [BsonRepresentation(BsonType.String)] public Guid Id { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = default!;
        public string AggregateId { get; set; } = default!;
        public long AggregateVersion { get; set; }
        public string Topic { get; set; } = "atlas.orders.v1";
        public bool Published { get; set; }
        public string CorrelationId { get; set; } = default!;
        public BsonDocument Payload { get; set; } = default!;
        public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public static OutboxDoc StepEvent(Guid orderId, string step, long version, string tracking, string correlationId) => new()
        {
            EventType = "Fulfillment.StepV1",
            AggregateId = orderId.ToString(),
            AggregateVersion = version,
            CorrelationId = correlationId,
            Payload = new BsonDocument
            {
                ["order_id"] = orderId.ToString(),
                ["step"] = step,
                ["tracking_code"] = tracking,
            },
        };
    }
}

public static class FulfillmentOrderHydration
{
    public static FulfillmentOrder Hydrate(Guid orderId, FulfillmentOrder.FulfillmentState state, string carrier, string tracking, long version, IReadOnlyList<string> steps) =>
        FulfillmentOrder.Hydrate(orderId, state, carrier, tracking, version, steps);
}
