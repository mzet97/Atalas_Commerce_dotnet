namespace Atlas.Inventory.Infrastructure;

using Atlas.Inventory.Application;
using Atlas.Inventory.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

/// <summary>
/// Adapter MongoDB do Inventory (ADR-0002): posição com update CONDICIONAL
/// (filtro _id + version + available suficiente) dentro de transação de replica set,
/// gravando reserva (índice único por idempotency key), movimento de ledger e
/// documento de outbox (INV-EVT-001; envelope espelha Atlas.Contracts.Events).
/// </summary>
public sealed partial class MongoInventoryStore : IInventoryStore
{
    // Driver 3.x exige representação explícita de Guid (padrão do projeto: Standard/UUIDv4)
    private static readonly bool _guidRegistered = RegisterGuid();
    private static bool RegisterGuid()
    {
        try { BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); }
        catch (BsonSerializationException) { /* já registrado */ }
        return true;
    }

    private readonly IMongoDatabase _db;

    public MongoInventoryStore(IMongoDatabase db) => _db = db;

    public static MongoInventoryStore Create(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        var store = new MongoInventoryStore(client.GetDatabase(databaseName));
        store.EnsureIndexes();
        return store;
    }

    private void EnsureIndexes()
    {
        Reservations().Indexes.CreateOne(
            new CreateIndexModel<ReservationDoc>(
                Builders<ReservationDoc>.IndexKeys.Ascending(d => d.IdempotencyKey),
                new CreateIndexOptions { Unique = true }));
    }

    private IMongoCollection<PositionDoc> Positions() => _db.GetCollection<PositionDoc>("inventory.positions");
    private IMongoCollection<ReservationDoc> Reservations() => _db.GetCollection<ReservationDoc>("inventory.reservations");
    private IMongoCollection<MovementDoc> Movements() => _db.GetCollection<MovementDoc>("inventory.movements");
    private IMongoCollection<OutboxDoc> Outbox() => _db.GetCollection<OutboxDoc>("inventory.outbox");

    public async Task<Reservation?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        var doc = await Reservations().Find(r => r.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<ReserveStockResult> ReserveInTransactionAsync(
        Reservation reservation, IReadOnlyList<StockKey> conditionalKeys, string correlationId, string actorSub, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        try
        {
            return await session.WithTransactionAsync(async (s, token) =>
            {
                // 1) Reserva idempotente: READ-FIRST dentro da MESMA transação.
                //    (11000 DENTRO de transação ABORTA a transação — engolir e seguir
                //    envenena a txn e o retry do driver entra em loop; read-first
                //    converge no vencedor: quem perder o insert recebe 11000 → txn
                //    abortada → retry do driver lê o vencedor já commitado → Duplicate)
                var existing = await Reservations()
                    .Find(s, r => r.IdempotencyKey == reservation.IdempotencyKey)
                    .FirstOrDefaultAsync(cancellationToken: token);
                if (existing is not null)
                    return ReserveStockResult.Duplicate(existing.ToDomain());

                await Reservations().InsertOneAsync(s, ReservationDoc.From(reservation), cancellationToken: token);

                // 2) Para cada linha: update condicional (CAS por version) com retry borno
                foreach (var line in reservation.Lines)
                {
                    // Update condicional ATÔMICO no servidor: matched somente com saldo suficiente (INV-STOCK-001)
                    // $expr: OnHand - Reserved - SafetyStock >= qty (aritmética server-side; LINQ3 não traduz)
                    var availableExpr = new BsonDocument("$subtract",
                        new BsonArray { new BsonDocument("$subtract", new BsonArray { "$OnHand", "$Reserved" }), "$SafetyStock" });
                    var filter = new BsonDocumentFilterDefinition<PositionDoc>(new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument("_id", line.Key.ToString()),
                        new BsonDocument("$expr", new BsonDocument("$gte", new BsonArray { availableExpr, line.Quantity })),
                    }));
                    var update = Builders<PositionDoc>.Update
                        .Inc(p => p.Reserved, line.Quantity)
                        .Inc(p => p.Version, 1);
                    var result = await Positions().UpdateOneAsync(s, filter, update, cancellationToken: token);
                    if (result.ModifiedCount != 1)
                    {
                        var snapshot = await Positions().Find(s, p => p.Id == line.Key.ToString()).FirstOrDefaultAsync(cancellationToken: token);
                        if (snapshot is null) throw new InvalidOperationException($"Posição inexistente: {line.Key}");
                        throw new OutOfStockException(line.Key.SkuId);
                    }
                }

                // 3) Ledger (INV-STOCK-002)
                foreach (var line in reservation.Lines)
                {
                    await Movements().InsertOneAsync(s, MovementDoc.FromLedger(
                        line.Key, line.Quantity, $"reserve:{reservation.IdempotencyKey}", actorSub, correlationId), cancellationToken: token);
                }

                // 4) Outbox na MESMA transação (ADR-0003)
                await Outbox().InsertOneAsync(s, cancellationToken: token, document: OutboxDoc.DomainEvent(
                    eventId: Guid.NewGuid(),
                    eventType: "Stock.ReservedV1",
                    aggregateId: reservation.Id.ToString(),
                    correlationId: correlationId,
                    payload: new BsonDocument
                    {
                        ["reservation_id"] = reservation.Id.ToString(),
                        ["idempotency_key"] = reservation.IdempotencyKey,
                        ["lines"] = new BsonArray(reservation.Lines.Select(l => new BsonDocument
                        {
                            ["key"] = l.Key.ToString(), ["quantity"] = l.Quantity,
                        })),
                    }));

                return ReserveStockResult.Granted(reservation);
            }, cancellationToken: ct);
        }
        catch (OutOfStockException ex)
        {
            return ReserveStockResult.OutOfStock([ex.SkuId]); // transação abortada: NADA persistido
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            // Corrida da MESMA idempotency key: unique index elege um vencedor (INV-STOCK-003).
            // O vencedor pode ainda estar em commit (11000 dispara contra a key em voo) —
            // aguarda a visibilidade antes de desistir.
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(100, ct);
                var winner = await FindByIdempotencyKeyAsync(reservation.IdempotencyKey, ct);
                if (winner is not null) return ReserveStockResult.Duplicate(winner);
            }
            throw; // 11000 de outra chave — não deveria ocorrer
        }
    }

    /// <summary>
    /// Ajuste ADMIN de estoque em transação (SPEC-004): CAS por version + ledger + outbox
    /// Stock.AdjustedV1 com a VERSION da posição — a projeção aplica @ver, tornando o lag
    /// CQRS comparável (head do owner × source_version da view).
    /// </summary>
    public async Task<(bool Ok, long NewVersion)?> AdjustStockInTransactionAsync(
        StockKey key, int delta, string reason, string correlationId, string actorSub, CancellationToken ct)
    {
        var id = key.ToString();
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        try
        {
            return await session.WithTransactionAsync(async (s, token) =>
            {
                var doc = await Positions().Find(s, p => p.Id == id).FirstOrDefaultAsync(token);
                if (doc is null) return ((bool, long)?)null;
                var available = doc.OnHand - doc.Reserved - doc.SafetyStock;
                if (delta < 0 && available + delta < 0) return ((bool, long)?)(false, doc.Version); // inviabiliza saldo
                var newVersion = doc.Version + 1;

                var result = await Positions().UpdateOneAsync(s,
                    Builders<PositionDoc>.Filter.And(
                        Builders<PositionDoc>.Filter.Eq(p => p.Id, id),
                        Builders<PositionDoc>.Filter.Eq(p => p.Version, doc.Version)),
                    Builders<PositionDoc>.Update
                        .Inc(p => p.OnHand, delta)
                        .Set(p => p.Version, newVersion),
                    cancellationToken: token);
                if (result.ModifiedCount != 1) return ((bool, long)?)null; // corrida: retry externo

                await Movements().InsertOneAsync(s, MovementDoc.FromLedger(
                    key, delta, $"adjust:{reason}", actorSub, correlationId), cancellationToken: token);
                await Outbox().InsertOneAsync(s, OutboxDoc.DomainEvent(
                    Guid.NewGuid(), "Stock.AdjustedV1", id, correlationId,
                    new BsonDocument
                    {
                        ["position_key"] = id,
                        ["delta"] = delta,
                        ["version"] = newVersion,
                        ["reason"] = reason,
                    }), cancellationToken: token);
                return ((bool, long)?)(true, newVersion);
            }, cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ((bool, long)?)null;
        }
    }

    public async Task<bool> ConfirmReservationAsync(Guid reservationId, string orderId, CancellationToken ct)
    {
        var result = await Reservations().UpdateOneAsync(
            r => r.Id == reservationId && r.Status == ReservationStatus.Active,
            Builders<ReservationDoc>.Update
                .Set(r => r.Status, ReservationStatus.Confirmed)
                .Set(r => r.OrderId, orderId)
                .Inc(r => r.Version, 1), cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> ReleaseReservationAsync(Guid reservationId, string reason, string correlationId, string actorSub, CancellationToken ct)
    {
        var reservation = await Reservations().Find(r => r.Id == reservationId).FirstOrDefaultAsync(ct);
        if (reservation is null) return false;
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var updated = await Reservations().UpdateOneAsync(s,
                r => r.Id == reservationId && (r.Status == ReservationStatus.Active || r.Status == ReservationStatus.Confirmed),
                Builders<ReservationDoc>.Update.Set(r => r.Status, ReservationStatus.Released).Inc(r => r.Version, 1), cancellationToken: token);
            if (updated.ModifiedCount != 1) return false;
            foreach (var line in reservation.Lines)
            {
                var filter = Builders<PositionDoc>.Filter.Eq(p => p.Id, line.Key.ToString());
                var update = Builders<PositionDoc>.Update.Inc(p => p.Reserved, -line.Quantity).Inc(p => p.Version, 1);
                await Positions().UpdateOneAsync(s, filter, update, cancellationToken: token);
                await Movements().InsertOneAsync(s, MovementDoc.FromLedger(ReservationDoc.ParseKey(line.Key), -line.Quantity, $"release:{reason}", actorSub, correlationId), cancellationToken: token);
            }
            await Outbox().InsertOneAsync(s, OutboxDoc.DomainEvent(
                Guid.NewGuid(), "Reservation.ReleasedV1", reservationId.ToString(), correlationId,
                new BsonDocument { ["reservation_id"] = reservationId.ToString(), ["reason"] = reason }), cancellationToken: token);
            return true;
        }, cancellationToken: ct);
    }

    private sealed class OutOfStockException(string skuId) : Exception(skuId)
    {
        public string SkuId { get; } = skuId;
    }

    // ---- documentos ----
    public sealed class PositionDoc
    {
        public string Id { get; set; } = default!; // warehouse:sku
        public int OnHand { get; set; }
        public int Reserved { get; set; }
        public int SafetyStock { get; set; }
        public long Version { get; set; } = 1;
        public int Available => OnHand - Reserved - SafetyStock;
    }

    public sealed class ReservationDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string IdempotencyKey { get; set; } = default!;
        public List<LineDoc> Lines { get; set; } = [];
        public ReservationStatus Status { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public string? OrderId { get; set; }
        public long Version { get; set; } = 1;

        public sealed record LineDoc(string Key, int Quantity);

        public static ReservationDoc From(Reservation r) => new()
        {
            Id = r.Id,
            IdempotencyKey = r.IdempotencyKey,
            Lines = [.. r.Lines.Select(l => new LineDoc(l.Key.ToString(), l.Quantity))],
            Status = r.Status,
            ExpiresAtUtc = r.ExpiresAtUtc,
            Version = r.Version,
        };

        public Reservation ToDomain() => Reservation.Hydrate(
            Id, IdempotencyKey,
            [.. Lines.Select(l => new ReservationLine(ParseKey(l.Key), l.Quantity))],
            ExpiresAtUtc, Status, OrderId, Version);
        public static StockKey ParseKey(string s)
        {
            var parts = s.Split(':', 2);
            return new StockKey(Guid.Parse(parts[0]), parts[1]);
        }
    }

    public sealed class MovementDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string PositionKey { get; set; } = default!;
        public int Delta { get; set; }
        public string Reason { get; set; } = default!;
        public string ActorSub { get; set; } = default!;
        public string CorrelationId { get; set; } = default!;
        public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public static MovementDoc FromLedger(StockKey key, int delta, string reason, string actor, string corr) => new()
        {
            PositionKey = key.ToString(), Delta = delta, Reason = reason, ActorSub = actor, CorrelationId = corr,
        };
    }

    public sealed class OutboxDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string EventType { get; set; } = default!;
        public string AggregateId { get; set; } = default!;
        public long AggregateVersion { get; set; } = 1;
        public string Topic { get; set; } = "atlas.inventory.v1";
        public bool Published { get; set; }
        public string CorrelationId { get; set; } = default!;
        public BsonDocument Payload { get; set; } = default!;
        public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public static OutboxDoc DomainEvent(Guid eventId, string eventType, string aggregateId, string correlationId, BsonDocument payload) => new()
        {
            Id = eventId, EventType = eventType, AggregateId = aggregateId,
            CorrelationId = correlationId, Payload = payload,
        };
    }
}

public partial class MongoInventoryStore
{
    /// <summary>Carrega a posição hidratada (ou null).</summary>
    public async Task<StockPosition?> LoadPositionAsync(StockKey key, CancellationToken ct)
    {
        var doc = await Positions().Find(p => p.Id == key.ToString()).FirstOrDefaultAsync(ct);
        return doc is null ? null : StockPosition.Hydrate(key, doc.OnHand, doc.Reserved, doc.SafetyStock, doc.Version);
    }

    /// <summary>Salva com guarda de versão (concorrência otimista). Upsert p/ posição nova.</summary>
    public async Task SavePositionAsync(StockKey key, StockPosition position, CancellationToken ct)
    {
        var filter = Builders<PositionDoc>.Filter.And(
            Builders<PositionDoc>.Filter.Eq(p => p.Id, key.ToString()),
            Builders<PositionDoc>.Filter.Lt(p => p.Version, position.Version));
        var update = Builders<PositionDoc>.Update
            .Set(p => p.OnHand, position.OnHand)
            .Set(p => p.Reserved, position.Reserved)
            .Set(p => p.SafetyStock, position.SafetyStock)
            .Set(p => p.Version, position.Version);
        var result = await Positions().UpdateOneAsync(filter, update,
            new UpdateOptions { IsUpsert = true }, cancellationToken: ct);
        if (result.ModifiedCount != 1 && result.UpsertedId is null)
            throw new InvalidOperationException($"Conflito de concorrência ao salvar {key} (v{position.Version})");
    }
}
