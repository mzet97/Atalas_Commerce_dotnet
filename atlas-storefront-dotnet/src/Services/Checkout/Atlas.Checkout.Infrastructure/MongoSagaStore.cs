namespace Atlas.Checkout.Infrastructure;

using Atlas.Checkout.Application;
using Atlas.Checkout.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

/// <summary>Store da saga de checkout (ADR-0006): persistência do Process Manager em Mongo.</summary>
public sealed class MongoSagaStore : ISagaStore
{
    private static readonly bool _guid = RegisterGuid();
    private static bool RegisterGuid()
    {
        try { BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); }
        catch (BsonSerializationException) { }
        return true;
    }

    private readonly IMongoDatabase _db;

    public MongoSagaStore(IMongoDatabase db) => _db = db;

    public static MongoSagaStore Create(string connectionString, string databaseName)
    {
        var store = new MongoSagaStore(new MongoClient(connectionString).GetDatabase(databaseName));
        store.EnsureIndexes();
        return store;
    }

    private void EnsureIndexes()
    {
        Sagas().Indexes.CreateOne(new CreateIndexModel<SagaDoc>(
            Builders<SagaDoc>.IndexKeys.Ascending(s => s.IdempotencyKey),
            new CreateIndexOptions { Unique = true }));
    }

    private IMongoCollection<SagaDoc> Sagas() => _db.GetCollection<SagaDoc>("checkout.sagas");

    public async Task SaveAsync(CheckoutSaga saga, CancellationToken ct)
    {
        var doc = SagaDoc.From(saga);
        await Sagas().ReplaceOneAsync(
            s => s.IdempotencyKey == doc.IdempotencyKey,
            doc,
            new ReplaceOptions { IsUpsert = true }, cancellationToken: ct);
    }

    public async Task<CheckoutSaga?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        var doc = await Sagas().Find(s => s.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<IReadOnlyList<CheckoutSaga>> FindByStateAsync(CheckoutSagaState state, CancellationToken ct)
    {
        var docs = await Sagas().Find(s => s.State == state).ToListAsync(ct);
        return [.. docs.Select(d => d.ToDomain()!)];
    }

    public sealed class SagaDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string IdempotencyKey { get; set; } = default!;
        public string CustomerId { get; set; } = default!;
        public long TotalMinor { get; set; }
        public string Currency { get; set; } = default!;
        public CheckoutSagaState State { get; set; }
        public string? ReservationId { get; set; }
        public string? OrderId { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? ProviderReference { get; set; }
        public string? FailureReason { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }

        public static SagaDoc From(CheckoutSaga s) => new()
        {
            Id = s.Id.Value, IdempotencyKey = s.IdempotencyKey, CustomerId = s.CustomerId,
            TotalMinor = s.TotalMinor, Currency = s.Currency, State = s.State,
            ReservationId = s.ReservationId, OrderId = s.OrderId,
            PaymentIntentId = s.PaymentIntentId, ProviderReference = s.ProviderReference,
            FailureReason = s.FailureReason, CreatedAtUtc = s.CreatedAtUtc,
        };

        public CheckoutSaga ToDomain()
        {
            var saga = CheckoutSaga.Hydrate(new CheckoutId(Id), IdempotencyKey, CustomerId, TotalMinor, Currency, CreatedAtUtc);
            // reidrata passos persistidos (ordem do fluxo)
            if (ReservationId is not null) saga.MarkStockReserved(ReservationId, CreatedAtUtc);
            if (OrderId is not null) saga.MarkOrderPlaced(OrderId, CreatedAtUtc);
            if (PaymentIntentId is not null && ProviderReference is null) saga.MarkPaymentAuthorized(PaymentIntentId, CreatedAtUtc);
            if (PaymentIntentId is not null && ProviderReference is null) saga.MarkPaymentAuthorized(PaymentIntentId, CreatedAtUtc);
            switch (State)
            {
                case CheckoutSagaState.Confirmed: saga.MarkConfirmed(CreatedAtUtc); break;
                case CheckoutSagaState.PaymentDeclined: saga.MarkPaymentDeclined(FailureReason ?? "declined", CreatedAtUtc); break;
                case CheckoutSagaState.PendingReconciliation: saga.MarkPendingReconciliation(FailureReason ?? "unknown", CreatedAtUtc); break;
                case CheckoutSagaState.Compensating: saga.BeginCompensation(FailureReason ?? "comp", CreatedAtUtc); break;
                case CheckoutSagaState.Compensated: saga.BeginCompensation(FailureReason ?? "comp", CreatedAtUtc); saga.MarkCompensated(CreatedAtUtc); break;
            }
            if (ProviderReference is not null)
                saga.RestoreReconciliationReference(PaymentIntentId ?? "pi-reconciled", ProviderReference,
                    State == CheckoutSagaState.Confirmed);
            return saga;
        }
    }
}
