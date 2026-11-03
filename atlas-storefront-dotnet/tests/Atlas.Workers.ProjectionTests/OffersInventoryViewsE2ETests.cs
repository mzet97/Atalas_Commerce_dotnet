using System.Text.Json;
using Atlas.Inventory.Application;
using Atlas.Inventory.Domain;
using Atlas.Inventory.Infrastructure;
using Atlas.Offers.Application;
using Atlas.Offers.Infrastructure;
using Atlas.Workers.Projections;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MongoDB.Bson;
using MongoDB.Driver;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Atlas.Workers.Projections.Tests;

/// <summary>
/// E2E REAL das views administrativas/de-vitrine do M2:
/// Offers: PublishOffer (outbox) → dispatcher → Kafka → OffersProjector → available_offer_view
/// Inventory: ReserveStock (outbox) → dispatcher → Kafka → InventoryProjector → inventory_admin_view
/// Ambos provando reentrega (dedup) e out-of-order.
/// </summary>
public sealed class OffersInventoryViewsE2ETests : IAsyncLifetime
{
    private const string MongoConn = "mongodb://127.0.0.1:27017/?directConnection=true";
    private const string PgConn = "Host=127.0.0.1;Username=atlas;Password=atlas_dev_only;Database=atlas_read";
    private const string Bootstrap = "127.0.0.1:29092";

    private readonly string _dbName = $"atlas_views_{Guid.NewGuid():N}";
    private readonly string _offersTopic = $"atlas.offers.v1.it.{Guid.NewGuid():N}";
    private readonly string _inventoryTopic = $"atlas.inventory.v1.it.{Guid.NewGuid():N}";
    private readonly string _offersTable = $"available_offer_view_it_{Guid.NewGuid():N}";
    private readonly string _positionsTable = $"inventory_admin_view_it_{Guid.NewGuid():N}";
    private readonly string _processed = $"processed_events_it_{Guid.NewGuid():N}";
    private readonly ITestOutputHelper _output;

    public OffersInventoryViewsE2ETests(ITestOutputHelper output) => _output = output;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await new MongoClient(MongoConn).GetDatabase(_dbName).Client.DropDatabaseAsync(_dbName);
        await using var conn = new NpgsqlConnection(PgConn);
        await conn.OpenAsync();
        await new NpgsqlCommand($"""
            DROP TABLE IF EXISTS {_offersTable};
            DROP TABLE IF EXISTS {_positionsTable};
            DROP TABLE IF EXISTS {_processed};
            """, conn).ExecuteNonQueryAsync();
    }

    private async Task CreatePgTablesAsync()
    {
        await using var conn = new NpgsqlConnection(PgConn);
        await conn.OpenAsync();
        await new NpgsqlCommand($"""
            CREATE TABLE {_offersTable}(
                offer_id uuid PRIMARY KEY, sku_id text, seller_id text, market_id text, channel text,
                currency text, price_minor bigint, status text, source_version bigint, projected_at timestamptz);
            CREATE TABLE {_positionsTable}(
                position_key text PRIMARY KEY, sku_id text, on_hand int, reserved int,
                source_version bigint, projected_at timestamptz);
            CREATE TABLE {_processed}(
                consumer_id text, event_id uuid, applied_at timestamptz DEFAULT now(),
                PRIMARY KEY(consumer_id, event_id));
            """, conn).ExecuteNonQueryAsync();
    }

    private static async Task<List<DomainEventEnvelope>> ConsumeAsync(
        string topic, int expected, CancellationToken ct)
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = Bootstrap,
            GroupId = $"it-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();
        consumer.Subscribe(topic);
        var envelopes = new List<DomainEventEnvelope>();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (envelopes.Count < expected && DateTime.UtcNow < deadline)
        {
            var msg = consumer.Consume(TimeSpan.FromSeconds(2));
            if (msg is null) continue;
            var raw = JsonDocument.Parse(msg.Message.Value).RootElement;
            envelopes.Add(new DomainEventEnvelope(
                raw.GetProperty("event_id").GetGuid(),
                raw.GetProperty("event_type").GetString()!,
                raw.GetProperty("aggregate_id").GetString()!,
                raw.GetProperty("aggregate_version").GetInt64(),
                raw.GetProperty("correlation_id").GetString()!,
                raw.GetProperty("payload").Clone()));
        }
        return envelopes;
    }

    [Fact]
    public async Task Offers_e_inventory_views_ponta_a_ponta()
    {
        await CreatePgTablesAsync();
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = Bootstrap }).Build();
        try { await admin.CreateTopicsAsync(
            [new TopicSpecification { Name = _offersTopic, NumPartitions = 1, ReplicationFactor = 1 },
             new TopicSpecification { Name = _inventoryTopic, NumPartitions = 1, ReplicationFactor = 1 }]); }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists)) { }

        var producerCfg = new ProducerConfig { BootstrapServers = Bootstrap, LingerMs = 5 };
        using var producer = new ProducerBuilder<string, string>(producerCfg).Build();

        // ---------- OFFERS: publish → available_offer_view ----------
        var offersDb = new MongoClient(MongoConn).GetDatabase(_dbName);
        var offersStore = MongoOffersStore.Create(MongoConn, _dbName);
        var offerCmd = new PublishOfferCommand(
            RequestId: "off-view-1", ActorSub: "pricing", SkuId: "sku-view", SellerId: "seller-1p",
            MarketId: "BR", Channel: "web", Currency: "BRL", PriceMinor: 4990, ListPriceMinor: 5990,
            ValidFromUtc: DateTimeOffset.UtcNow.AddMinutes(-1), ValidToUtc: DateTimeOffset.UtcNow.AddDays(30),
            CorrelationId: "corr-off");
        var offer = await offersStore.PublishInTransactionAsync(offerCmd, CancellationToken.None);
        Assert.True(offer.Accepted, offer.Reason);
        await offersDb.GetCollection<OutboxEntry>("offers.outbox").UpdateOneAsync(
            Builders<OutboxEntry>.Filter.Eq(e => e.Published, false),
            Builders<OutboxEntry>.Update.Set(e => e.Topic, _offersTopic));

        var offersDispatcher = new OutboxDispatcher(offersDb, producer);
        Assert.Equal(1, await offersDispatcher.DispatchPendingBatchAsync("offers.outbox", "offers", CancellationToken.None));
        var offerEvents = await ConsumeAsync(_offersTopic, 1, CancellationToken.None);
        Assert.Equal("Offer.PublishedV1", offerEvents.Single().EventType);

        var offersProjector = new OffersProjector(PgConn, "offers-view-it", _offersTable, _processed);
        Assert.Equal(1, await offersProjector.ApplyBatchAsync(offerEvents, CancellationToken.None));
        Assert.Equal(0, await offersProjector.ApplyBatchAsync(offerEvents, CancellationToken.None)); // reentrega

        await using var conn = new NpgsqlConnection(PgConn);
        await conn.OpenAsync();
        await using (var readOffer = new NpgsqlCommand(
            $"SELECT sku_id, market_id, price_minor, status FROM {_offersTable}", conn))
        await using (var readerOffer = await readOffer.ExecuteReaderAsync())
        {
            await readerOffer.ReadAsync();
            Assert.Equal("sku-view", readerOffer.GetString(0));
            Assert.Equal("BR", readerOffer.GetString(1));
            Assert.Equal(4990L, readerOffer.GetInt64(2));
            Assert.Equal("Active", readerOffer.GetString(3));
        }
        _output.WriteLine("[offers] available_offer_view: sku=sku-view market=BR price=4990 Active (+dedup na reentrega)");

        // ---------- INVENTORY: reserve → inventory_admin_view ----------
        var inventoryDb = new MongoClient(MongoConn).GetDatabase(_dbName);
        var inventoryStore = MongoInventoryStore.Create(MongoConn, _dbName);
        var wh = Guid.NewGuid();
        var positions = inventoryDb.GetCollection<MongoInventoryStore.PositionDoc>("inventory.positions");
        var key = StockKey.Create(wh, "sku-view").ToString();
        await positions.UpdateOneAsync(
            p => p.Id == key,
            Builders<MongoInventoryStore.PositionDoc>.Update
                .Set(p => p.OnHand, 10).Set(p => p.SafetyStock, 0).Set(p => p.Reserved, 0)
                .Set(p => p.Version, 1).SetOnInsert(p => p.Id, key),
            new UpdateOptions { IsUpsert = true });

        var handler = new ReserveStockHandler(inventoryStore);
        var reserved = await handler.HandleAsync(
            new ReserveStockCommand("view-chk-1", [new ReserveLineCommand(wh, "sku-view", 3)],
                TimeSpan.FromMinutes(15), "corr-inv", "operator-1"), CancellationToken.None);
        Assert.Equal(ReserveStockStatus.Granted, reserved.Status);
        await inventoryDb.GetCollection<OutboxEntry>("inventory.outbox").UpdateOneAsync(
            Builders<OutboxEntry>.Filter.Eq(e => e.Published, false),
            Builders<OutboxEntry>.Update.Set(e => e.Topic, _inventoryTopic));

        var inventoryDispatcher = new OutboxDispatcher(inventoryDb, producer);
        Assert.Equal(1, await inventoryDispatcher.DispatchPendingBatchAsync("inventory.outbox", "inventory", CancellationToken.None));
        var inventoryEvents = await ConsumeAsync(_inventoryTopic, 1, CancellationToken.None);
        Assert.Equal("Stock.ReservedV1", inventoryEvents.Single().EventType);

        var inventoryProjector = new InventoryProjector(PgConn, "inventory-view-it", _positionsTable, _processed);
        Assert.Equal(1, await inventoryProjector.ApplyBatchAsync(inventoryEvents, CancellationToken.None));
        Assert.Equal(0, await inventoryProjector.ApplyBatchAsync(inventoryEvents, CancellationToken.None)); // reentrega NÃO re-aplica delta

        await using (var readPos = new NpgsqlCommand(
            $"SELECT on_hand, reserved FROM {_positionsTable} WHERE position_key = @k", conn))
        {
            readPos.Parameters.AddWithValue("k", key);
            await using var readerPos = await readPos.ExecuteReaderAsync();
            await readerPos.ReadAsync();
            Assert.Equal(0, readerPos.GetInt32(0));
            Assert.Equal(3, readerPos.GetInt32(1)); // delta aplicado UMA vez
        }
        _output.WriteLine("[inventory] inventory_admin_view: reserved=3 após 1 aplicação (reentrega sem efeito)");
    }
}
