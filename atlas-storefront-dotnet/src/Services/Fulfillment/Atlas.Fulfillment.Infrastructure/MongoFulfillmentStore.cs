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
    private IMongoCollection<OutboxDoc> Outbox() => _db.GetCollection<FulfillmentDoc>("fulfillment.outbox") is null ? null! : _db.GetCollection<OutboxDoc>("fulfillment.outbox");

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
    public sealed class OutboxDoc { public Guid EventId { get; set; } = Guid.NewGuid(); }
}

public static class FulfillmentOrderHydration
{
    public static FulfillmentOrder Hydrate(Guid orderId, FulfillmentOrder.FulfillmentState state, string carrier, string tracking, long version, IReadOnlyList<string> steps) =>
        FulfillmentOrder.Hydrate(orderId, state, carrier, tracking, version, steps);
}
