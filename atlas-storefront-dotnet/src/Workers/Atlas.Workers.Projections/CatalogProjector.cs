using System.Text.Json;
using Npgsql;

namespace Atlas.Workers.Projections;

public sealed record DomainEventEnvelope(
    Guid EventId, string EventType, string AggregateId, long AggregateVersion,
    string CorrelationId, JsonElement Payload);

/// <summary>
/// Projection worker catálogo → PostgreSQL (ADR-0002/0011): consome o tópico do contexto e
/// aplica upsert + processed_events NA MESMA transação PG (INV-EVT-001). Evento repetido
/// (at-least-once) NÃO duplica projeção; out-of-order por version é ignorado (staging/upsert
/// condicional por source_version).
/// </summary>
public sealed class CatalogProjector(
    string connectionString, string consumerId,
    string productsTable = "catalog_product_search", string processedTable = "processed_events")
{
    /// <summary>Aplica um lote de envelopes (consumidos do Kafka) às projeções. Idempotente.</summary>
    public async Task<int> ApplyBatchAsync(IReadOnlyList<DomainEventEnvelope> batch, CancellationToken ct)
    {
        var applied = 0;
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        foreach (var evt in batch)
        {
            await using var tx = await conn.BeginTransactionAsync(ct);
            // inbox: dedup transacional (event_id + consumer)
            await using (var dedup = new NpgsqlCommand(
                $"INSERT INTO {processedTable}(consumer_id, event_id) VALUES (@c, @e) ON CONFLICT DO NOTHING",
                conn, tx))
            {
                dedup.Parameters.AddWithValue("c", consumerId);
                dedup.Parameters.AddWithValue("e", evt.EventId);
                var inserted = await dedup.ExecuteNonQueryAsync(ct);
                if (inserted == 0)
                {
                    await tx.RollbackAsync(ct); // já processado: pula (idempotência INV-EVT-001)
                    continue;
                }
            }

            if (evt.EventType == "Product.PublishedV1")
            {
                var productId = evt.Payload.GetProperty("product_id").GetString();
                var marketId = evt.Payload.GetProperty("market_id").GetString();
                // skus do evento (M5): link produto→SKU para a PDP buscar ofertas na view
                var skus = evt.Payload.TryGetProperty("sku_codes", out var skusEl) && skusEl.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? skusEl.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray()
                    : [];
                // upsert condicional por source_version: evento antigo nunca sobrescreve novo
                await using var upsert = new NpgsqlCommand($"""
                    INSERT INTO {productsTable}(product_id, title, market_id, status, source_version, projected_at, skus)
                    VALUES (@id, @title, @market, 'Published', @ver, now(), @skus)
                    ON CONFLICT (product_id) DO UPDATE SET
                        title = EXCLUDED.title, skus = EXCLUDED.skus, source_version = EXCLUDED.source_version, projected_at = now()
                    WHERE {productsTable}.source_version < @ver
                    """, conn, tx);
                upsert.Parameters.AddWithValue("id", Guid.Parse(productId!));
                var title = evt.Payload.TryGetProperty("title", out var titleEl) && titleEl.GetString() is { Length: > 0 } t2
                    ? t2 : $"produto {productId} ({marketId})";
                upsert.Parameters.AddWithValue("title", title);
                upsert.Parameters.AddWithValue("market", marketId ?? "");
                upsert.Parameters.AddWithValue("ver", evt.AggregateVersion);
                upsert.Parameters.AddWithValue("skus", skus);
                await upsert.ExecuteNonQueryAsync(ct);
                applied++;
            }
            await tx.CommitAsync(ct);
        }
        return applied;
    }
}

/// <summary>
/// Projector de available_offer_view (Offers → PG): upsert por offer_id com stale guard
/// por aggregate_version (TEST-002-004 — evento repetido/out-of-order não retroage).
/// availability_hint NÃO confirma saldo (inv-CQRS-001); comando revalida no owner.
/// </summary>
public sealed class OffersProjector(string connectionString, string consumerId,
    string offersTable = "available_offer_view", string processedTable = "processed_events")
{
    public async Task<int> ApplyBatchAsync(IReadOnlyList<DomainEventEnvelope> batch, CancellationToken ct)
    {
        var applied = 0;
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        foreach (var evt in batch)
        {
            await using var tx = await conn.BeginTransactionAsync(ct);
            await using (var dedup = new NpgsqlCommand(
                $"INSERT INTO {processedTable}(consumer_id, event_id) VALUES (@c, @e) ON CONFLICT DO NOTHING", conn, tx))
            {
                dedup.Parameters.AddWithValue("c", consumerId);
                dedup.Parameters.AddWithValue("e", evt.EventId);
                if (await dedup.ExecuteNonQueryAsync(ct) == 0) { await tx.RollbackAsync(ct); continue; }
            }

            if (evt.EventType == "Offer.PublishedV1")
            {
                var offerId = evt.Payload.GetProperty("offer_id").GetString();
                var scope = evt.Payload.GetProperty("scope_key").GetString()!;
                var parts = scope.Split('|'); // sku|seller|market|channel
                await using var upsert = new NpgsqlCommand($"""
                    INSERT INTO {offersTable}(offer_id, sku_id, seller_id, market_id, channel, currency, price_minor, status, source_version, projected_at)
                    VALUES (@id, @sku, @seller, @market, @channel, @cur, @price, 'Active', @ver, now())
                    ON CONFLICT (offer_id) DO UPDATE SET
                        price_minor = {offersTable}.price_minor, status = {offersTable}.status
                    WHERE {offersTable}.source_version < @ver
                    """, conn, tx);
                upsert.Parameters.AddWithValue("id", Guid.Parse(offerId!));
                upsert.Parameters.AddWithValue("sku", parts[0]);
                upsert.Parameters.AddWithValue("seller", parts[1]);
                upsert.Parameters.AddWithValue("market", parts[2]);
                upsert.Parameters.AddWithValue("channel", parts[3]);
                upsert.Parameters.AddWithValue("cur", evt.Payload.GetProperty("currency").GetString()!);
                upsert.Parameters.AddWithValue("price", evt.Payload.GetProperty("price_minor").GetInt64());
                upsert.Parameters.AddWithValue("ver", evt.AggregateVersion);
                await upsert.ExecuteNonQueryAsync(ct);
                applied++;
            }
            await tx.CommitAsync(ct);
        }
        return applied;
    }
}

/// <summary>
/// Projector de inventory_admin_view (Inventory → PG): eventos de DELTA aplicados APÓS
/// dedup transacional (reentrega pula → delta não re-aplica); grade administrativa com
/// source_version; comandos revalidam no owner (INV-CQRS-001).
/// </summary>
public sealed class InventoryProjector(string connectionString, string consumerId,
    string positionsTable = "inventory_admin_view", string processedTable = "processed_events")
{
    public async Task<int> ApplyBatchAsync(IReadOnlyList<DomainEventEnvelope> batch, CancellationToken ct)
    {
        var applied = 0;
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        foreach (var evt in batch)
        {
            await using var tx = await conn.BeginTransactionAsync(ct);
            await using (var dedup = new NpgsqlCommand(
                $"INSERT INTO {processedTable}(consumer_id, event_id) VALUES (@c, @e) ON CONFLICT DO NOTHING", conn, tx))
            {
                dedup.Parameters.AddWithValue("c", consumerId);
                dedup.Parameters.AddWithValue("e", evt.EventId);
                if (await dedup.ExecuteNonQueryAsync(ct) == 0) { await tx.RollbackAsync(ct); continue; }
            }

            if (evt.EventType == "Stock.AdjustedV1")
            {
                // SPEC-004: ajuste admin com a VERSION da posição no evento —
                // source_version projetada = head do owner (lag comparável)
                var payloadAdj = evt.Payload;
                var keyAdj = payloadAdj.GetProperty("position_key").GetString()!;
                var deltaAdj = payloadAdj.GetProperty("delta").GetInt32();
                var verAdj = payloadAdj.GetProperty("version").GetInt64();
                var skuAdj = keyAdj.Split(':', 2)[1];
                await using var adjUpsert = new NpgsqlCommand($"""
                    INSERT INTO {positionsTable}(position_key, sku_id, on_hand, reserved, source_version, projected_at)
                    VALUES (@key, @sku, @delta, 0, @ver, now())
                    ON CONFLICT (position_key) DO UPDATE SET
                        on_hand = {positionsTable}.on_hand + @delta,
                        source_version = GREATEST({positionsTable}.source_version, @ver),
                        projected_at = now()
                    """, conn, tx);
                adjUpsert.Parameters.AddWithValue("key", keyAdj);
                adjUpsert.Parameters.AddWithValue("sku", skuAdj);
                adjUpsert.Parameters.AddWithValue("delta", deltaAdj);
                adjUpsert.Parameters.AddWithValue("ver", verAdj);
                await adjUpsert.ExecuteNonQueryAsync(ct);
                applied++;
            }

            if (evt.EventType == "Stock.ReservedV1")
            {
                foreach (var line in evt.Payload.GetProperty("lines").EnumerateArray())
                {
                    var key = line.GetProperty("key").GetString()!;
                    var qty = line.GetProperty("quantity").GetInt32();
                    var sku = key.Split(':', 2)[1];
                    await using var upsert = new NpgsqlCommand($"""
                        INSERT INTO {positionsTable}(position_key, sku_id, on_hand, reserved, source_version, projected_at)
                        VALUES (@key, @sku, 0, @qty, 1, now())
                        ON CONFLICT (position_key) DO UPDATE SET
                            reserved = {positionsTable}.reserved + @qty,
                            source_version = {positionsTable}.source_version + 1,
                            projected_at = now()
                        """, conn, tx);
                    upsert.Parameters.AddWithValue("key", key);
                    upsert.Parameters.AddWithValue("sku", sku);
                    upsert.Parameters.AddWithValue("qty", qty);
                    await upsert.ExecuteNonQueryAsync(ct);
                    applied++;
                }
            }
            await tx.CommitAsync(ct);
        }
        return applied;
    }
}

