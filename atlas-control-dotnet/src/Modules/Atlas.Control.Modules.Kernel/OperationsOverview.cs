namespace Atlas.Control.Modules.Kernel;

using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Visão operacional read-only (SPEC-004 req.6): idade do outbox por owner, sagas de
/// checkout por estado (conciliação) e placeholders de DLQ (M6). Leitura direta de
/// OBSERVABILIDADE — comandos de negócio continuam SEMPRE via owners (ADR-0001).
/// </summary>
public sealed class OperationsOverview(IMongoClient client)
{
    public async Task<object> SnapshotAsync(CancellationToken ct)
    {
        var outboxes = new Dictionary<string, object>();
        foreach (var (db, collection, label) in new[]
        {
            ("atlas_catalog", "catalog.outbox", "catalog"),
            ("atlas_offers", "offers.outbox", "offers"),
            ("atlas_inventory", "inventory.outbox", "inventory"),
            ("atlas_payments", "payments.outbox", "payments"),
        })
        {
            var col = client.GetDatabase(db).GetCollection<BsonDocument>(collection);
            var pending = await col.CountDocumentsAsync(
                Builders<BsonDocument>.Filter.Ne("Published", true), cancellationToken: ct);
            var oldest = await col.Find(Builders<BsonDocument>.Filter.Ne("Published", true))
                .SortBy(d => d["OccurredAtUtc"]).Limit(1).FirstOrDefaultAsync(ct);
            long? ageSeconds = null;
            if (oldest is not null && oldest.TryGetValue("OccurredAtUtc", out var occ) &&
                BsonTypeMapper.MapToDotNetValue(occ) is DateTimeOffset occurredAt)
                ageSeconds = (long)(DateTimeOffset.UtcNow - occurredAt).TotalSeconds;
            outboxes[label] = new { pending = (long)pending, oldestPendingAgeSeconds = ageSeconds };
        }

        // sagas de checkout por estado (conciliação — quantos UNKNOWN ainda não resolvidos)
        var sagas = client.GetDatabase("atlas_checkout").GetCollection<BsonDocument>("checkout.sagas");
        var byState = new Dictionary<string, long>();
        var grouped = await sagas.Aggregate()
            .Group(new BsonDocument { ["_id"] = "$State", ["count"] = new BsonDocument("$sum", 1) })
            .ToListAsync(ct);
        foreach (var doc in grouped)
        {
            var state = doc["_id"].IsString ? doc["_id"].AsString : doc["_id"].IsBsonNull ? "?" : doc["_id"].ToInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
            byState[state] = doc["count"].ToInt64();
        }

        return new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            outbox = outboxes,
            checkoutSagas = byState,
            pendingReconciliation = byState.TryGetValue("PendingReconciliation", out var p) ? p : 0,
            dlqDepth = "N/A até M6 (notifications.dlq)",
            note = "leitura de observabilidade; decisões críticas SEMPRE revalidam no owner (INV-CQRS-001)",
        };
    }
}
