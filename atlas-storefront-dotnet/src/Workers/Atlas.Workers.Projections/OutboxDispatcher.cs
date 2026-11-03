using Confluent.Kafka;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Atlas.Workers.Projections;

/// <summary>
/// Documento de outbox genérico para leitura dos workers (mesma estrutura gravada pelos owners).
/// </summary>
public sealed class OutboxEntry
{
    public string Id { get; set; } = default!; // Guid serializado como string nos owners
    public string EventType { get; set; } = default!;
    public string AggregateId { get; set; } = default!;
    public long AggregateVersion { get; set; }
    public string Topic { get; set; } = default!;
    public bool Published { get; set; }
    public string CorrelationId { get; set; } = default!;
    public BsonDocument Payload { get; set; } = default!;
    public DateTimeOffset OccurredAtUtc { get; set; }
}

/// <summary>
/// Bridge outbox→Kafka (ADR-0003): lote idempotente de não-publicados, produce com
/// key = aggregate_id (ordem por agregado), marca Published=true após ack do broker.
/// Kafka indisponível → outbox acumula e retoma (mestre §4.1; OPS-07).
/// </summary>
public sealed class OutboxDispatcher(IMongoDatabase db, IProducer<string, string> producer)
{
    private readonly IMongoCollection<OutboxEntry> _generic = db.GetCollection<OutboxEntry>("__outbox_probe__");

    /// <summary>Despacha um lote da collection de outbox indicada. Retorna nº publicados.</summary>
    public async Task<int> DispatchPendingBatchAsync(
        string collectionName, string serviceName, CancellationToken ct)
    {
        var outbox = db.GetCollection<OutboxEntry>(collectionName);
        var pending = await outbox.Find(e => !e.Published)
            .SortBy(e => e.OccurredAtUtc).Limit(100).ToListAsync(ct);

        var published = 0;
        foreach (var entry in pending)
        {
            var envelope = new
            {
                event_id = entry.Id,
                event_type = entry.EventType,
                aggregate_id = entry.AggregateId,
                aggregate_version = entry.AggregateVersion,
                occurred_at = entry.OccurredAtUtc,
                schema_version = 1,
                correlation_id = entry.CorrelationId,
                payload = BsonTypeMapper.MapToDotNetValue(entry.Payload),
            };
            var message = new Message<string, string>
            {
                Key = entry.AggregateId,
                Value = System.Text.Json.JsonSerializer.Serialize(envelope),
            };
            var delivery = await producer.ProduceAsync(entry.Topic, message, ct);
            if (delivery.Status == PersistenceStatus.Persisted)
            {
                await outbox.UpdateOneAsync(
                    e => e.Id == entry.Id && !e.Published,
                    Builders<OutboxEntry>.Update.Set(e => e.Published, true), cancellationToken: ct);
                published++;
            }
        }
        return published;
    }
}
