namespace Atlas.Payments.Infrastructure;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

/// <summary>
/// Refund idempotente em STORE PERSISTENTE (SPEC-006, fecha a limitação in-memory do M6):
/// índice único por idempotency_key — mesma key → mesma linha (11000 = duplicado).
/// </summary>
public sealed class MongoRefundStore
{
    public static MongoRefundStore Create(string connectionString, string databaseName)
    {
        var store = new MongoRefundStore(new MongoClient(connectionString).GetDatabase(databaseName));
        store.Refunds().Indexes.CreateOne(new CreateIndexModel<RefundDoc>(
            Builders<RefundDoc>.IndexKeys.Ascending(d => d.IdempotencyKey),
            new CreateIndexOptions { Unique = true }));
        return store;
    }

    private readonly IMongoDatabase _db;
    private MongoRefundStore(IMongoDatabase db) => _db = db;

    private IMongoCollection<RefundDoc> Refunds() => _db.GetCollection<RefundDoc>("payments.refunds");

    /// <summary>True = pedido NOVO; False = key já usada (T02: mesma key → mesmo resultado).</summary>
    public async Task<bool> TryRegisterAsync(string idempotencyKey, string orderId, long amountMinor, string approvalSub, CancellationToken ct)
    {
        try
        {
            await Refunds().InsertOneAsync(new RefundDoc
            {
                IdempotencyKey = idempotencyKey,
                OrderId = orderId,
                AmountMinor = amountMinor,
                ApprovalSub = approvalSub,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            }, new InsertOneOptions(), ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            return false;
        }
    }

    public async Task<long> CountByOrderAsync(string orderId, CancellationToken ct) =>
        await Refunds().CountDocumentsAsync(
            Builders<RefundDoc>.Filter.Eq(d => d.OrderId, orderId), cancellationToken: ct);

    public sealed class RefundDoc
    {
        [BsonId] [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string IdempotencyKey { get; set; } = default!;
        public string OrderId { get; set; } = default!;
        public long AmountMinor { get; set; }
        public string ApprovalSub { get; set; } = default!;
        public DateTimeOffset CreatedAtUtc { get; set; }
    }
}
