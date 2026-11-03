namespace Atlas.Payments.Infrastructure;

using Atlas.Payments.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

/// <summary>Store Mongo do Payments owner (ADR-0002): intents com webhooks deduplicados (índice único).</summary>
public sealed class MongoPaymentsStore
{
    private static readonly bool _guid = RegisterGuid();
    private static bool RegisterGuid()
    {
        try { BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); }
        catch (BsonSerializationException) { }
        return true;
    }

    private readonly IMongoDatabase _db;

    public MongoPaymentsStore(IMongoDatabase db) => _db = db;

    public static MongoPaymentsStore Create(string connectionString, string databaseName)
    {
        var store = new MongoPaymentsStore(new MongoClient(connectionString).GetDatabase(databaseName));
        store.EnsureIndexes();
        return store;
    }

    private void EnsureIndexes()
    {
        Intents().Indexes.CreateOne(new CreateIndexModel<IntentDoc>(
            Builders<IntentDoc>.IndexKeys.Ascending(i => i.PspIdempotencyKey),
            new CreateIndexOptions { Unique = true }));
        Webhooks().Indexes.CreateOne(new CreateIndexModel<ProcessedWebhookDoc>(
            Builders<ProcessedWebhookDoc>.IndexKeys.Ascending(w => w.WebhookId),
            new CreateIndexOptions { Unique = true }));
    }

    private IMongoCollection<IntentDoc> Intents() => _db.GetCollection<IntentDoc>("payments.intents");
    private IMongoCollection<OutboxDoc> Outbox() => _db.GetCollection<OutboxDoc>("payments.outbox");
    private IMongoCollection<ProcessedWebhookDoc> Webhooks() => _db.GetCollection<ProcessedWebhookDoc>("payments.processed_webhooks");

    /// <summary>Resultado da aplicação de um webhook autenticado (INV-PAYMENT-001).</summary>
    public enum WebhookApplyResult { Processed, Duplicate, IntentNotFound, StateMismatch }

    /// <summary>Aborta a transação do webhook (estado não aplicável — dedup NÃO é gravado).</summary>
    public sealed class WebhookRejectedException(WebhookApplyResult result) : Exception(result.ToString())
    {
        public WebhookApplyResult Result { get; } = result;
    }

    /// <summary>Webhook id já processado — sinaliza ABORT da transação (11000 aborta txn; commit do retorno entrararia em loop de retry).</summary>
    public sealed class WebhookDuplicateException : Exception;

    public async Task SaveAsync(PaymentIntent intent, CancellationToken ct)
    {
        var doc = IntentDoc.From(intent);
        await Intents().ReplaceOneAsync(
            i => i.PspIdempotencyKey == doc.PspIdempotencyKey,
            doc,
            new ReplaceOptions { IsUpsert = true }, cancellationToken: ct);
    }

    public async Task<PaymentIntent?> FindByPspKeyAsync(string pspIdempotencyKey, CancellationToken ct)
    {
        var doc = await Intents().Find(i => i.PspIdempotencyKey == pspIdempotencyKey).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    /// <summary>Lookup por pedido (webhook do PSP chega com order_id — INV-PAYMENT-001).</summary>
    public async Task<PaymentIntent?> FindByOrderIdAsync(string orderId, CancellationToken ct)
    {
        var doc = await Intents().Find(i => i.OrderId == orderId).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    /// <summary>Nº de webhooks processados para um pedido (diagnóstico/verificação).</summary>
    public async Task<int> WebhookCountAsync(string orderId, CancellationToken ct = default) =>
        (int)await Webhooks().CountDocumentsAsync(w => w.OrderId == orderId, cancellationToken: ct);

    /// <summary>Transição + evento outbox na MESMA transação (guard: estado anterior).</summary>
    public async Task<bool> ApplyTransitionAsync(
        string pspIdempotencyKey, PaymentIntentState expectedState, PaymentIntentState newState,
        string? providerReference, string eventType, string correlationId, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var filter = Builders<IntentDoc>.Filter.And(
                Builders<IntentDoc>.Filter.Eq(i => i.PspIdempotencyKey, pspIdempotencyKey),
                Builders<IntentDoc>.Filter.Eq(i => i.State, expectedState));
            var update = Builders<IntentDoc>.Update
                .Set(i => i.State, newState)
                .Set(i => i.ProviderReference, providerReference)
                .Inc(i => i.Version, 1);
            var result = await Intents().UpdateOneAsync(s, filter, update, cancellationToken: token);
            if (result.ModifiedCount != 1) return false;

            await Outbox().InsertOneAsync(s, OutboxDoc.DomainEvent(
                Guid.NewGuid(), eventType, pspIdempotencyKey, correlationId,
                new BsonDocument { ["provider_reference"] = providerReference ?? "" }), cancellationToken: token);
            return true;
        }, cancellationToken: ct);
    }

    /// <summary>
    /// Aplica webhook AUTENTICADO do PSP numa única transação (INV-PAYMENT-001):
    /// dedup por webhook id (índice único) + transição CAS do intent + outbox —
    /// webhook duplicado NUNCA reaplica efeito (UC003-T05).
    /// </summary>
    public async Task<WebhookApplyResult> ApplyWebhookAsync(
        string orderId, string webhookId, string eventType, string providerReference,
        string correlationId, CancellationToken ct)
    {
        // mapa do evento → (estado esperado, estado novo, evento outbox)
        var map = eventType switch
        {
            "payment.authorized" => (PaymentIntentState.Unknown, PaymentIntentState.Authorized, "Payment.AuthorizedV1"),
            "payment.captured" => (PaymentIntentState.Authorized, PaymentIntentState.Captured, "Payment.CapturedV1"),
            _ => ((PaymentIntentState, PaymentIntentState, string)?)null,
        };
        if (map is null) return WebhookApplyResult.StateMismatch; // tipo não suportado (não grava dedup)
        var (expected, newState, outboxEvent) = map.Value;

        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        try
        {
            return await session.WithTransactionAsync(async (s, token) =>
            {
            // 1) dedup: índice único elege o primeiro; repetido → Duplicate (idempotente)
            try
            {
                await Webhooks().InsertOneAsync(s, new ProcessedWebhookDoc
                {
                    WebhookId = webhookId, EventType = eventType,
                    OrderId = orderId, ProcessedAtUtc = DateTimeOffset.UtcNow,
                }, cancellationToken: token);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
            {
                // 11000 ABORTA a transação: abortar via exceção (retornar normalmente
                // falharia no commit e o retry do driver entraria em loop)
                throw new WebhookDuplicateException();
            }

            // 2) transição CAS do intent (guard de estado) + outbox na MESMA transação
            var filter = Builders<IntentDoc>.Filter.And(
                Builders<IntentDoc>.Filter.Eq(i => i.OrderId, orderId),
                Builders<IntentDoc>.Filter.Eq(i => i.State, expected));
            var update = Builders<IntentDoc>.Update
                .Set(i => i.State, newState)
                .Set(i => i.ProviderReference, providerReference)
                .Inc(i => i.Version, 1);
            var result = await Intents().UpdateOneAsync(s, filter, update, cancellationToken: token);
            if (result.ModifiedCount != 1)
            {
                var current = await Intents().Find(s, i => i.OrderId == orderId).FirstOrDefaultAsync(token);
                // já está no estado alvo (webhook legítimo repetido com id diferente) → idempotente
                if (current is not null && current.State == newState) return WebhookApplyResult.Duplicate;
                // rejeita ABORTANDO a transação: dedup não pode ficar gravado sem efeito aplicado
                throw new WebhookRejectedException(
                    current is null ? WebhookApplyResult.IntentNotFound : WebhookApplyResult.StateMismatch);
            }

            await Outbox().InsertOneAsync(s, OutboxDoc.DomainEvent(
                Guid.NewGuid(), outboxEvent, orderId, correlationId,
                new BsonDocument { ["provider_reference"] = providerReference ?? "" }), cancellationToken: token);
            return WebhookApplyResult.Processed;
            }, cancellationToken: ct);
        }
        catch (WebhookRejectedException ex)
        {
            return ex.Result;
        }
        catch (WebhookDuplicateException)
        {
            return WebhookApplyResult.Duplicate;
        }
    }

    public sealed class ProcessedWebhookDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string WebhookId { get; set; } = default!;
        public string EventType { get; set; } = default!;
        public string OrderId { get; set; } = default!;
        public DateTimeOffset ProcessedAtUtc { get; set; }
    }

    public sealed class IntentDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string OrderId { get; set; } = default!;
        public long AmountMinor { get; set; }
        public string Currency { get; set; } = default!;
        public string PspIdempotencyKey { get; set; } = default!;
        public PaymentIntentState State { get; set; }
        public string? ProviderReference { get; set; }
        public string? DeclineReason { get; set; }
        public long Version { get; set; } = 1;
        public List<string> WebhookIds { get; set; } = [];

        public static IntentDoc From(PaymentIntent i) => new()
        {
            Id = i.Id.Value, OrderId = i.OrderId, AmountMinor = i.AmountMinor,
            Currency = i.Currency, PspIdempotencyKey = i.PspIdempotencyKey,
            State = i.State, ProviderReference = i.ProviderReference,
            DeclineReason = i.DeclineReason, Version = 1,
            WebhookIds = [.. i.ProcessedWebhookIds],
        };

        public PaymentIntent ToDomain() => PaymentIntent.Hydrate(
            new PaymentIntentId(Id), OrderId, AmountMinor, Currency, PspIdempotencyKey, State,
            ProviderReference, DeclineReason, WebhookIds, []);
    }

    public sealed class OutboxDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string EventType { get; set; } = default!;
        public string AggregateId { get; set; } = default!;
        public long AggregateVersion { get; set; } = 1;
        public string Topic { get; set; } = "atlas.payments.v1";
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
