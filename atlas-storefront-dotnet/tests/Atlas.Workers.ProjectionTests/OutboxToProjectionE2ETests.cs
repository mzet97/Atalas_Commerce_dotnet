using System.Text.Json;
using Atlas.Catalog.Application;
using Atlas.Catalog.Infrastructure;
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
/// E2E REAL (TEST-002-004/006): Mongo outbox → dispatcher → Kafka (Podman, 127.0.0.1:29092)
/// → consumer → projector → PostgreSQL (Podman, atlas_read). Prova: evento no tópico com
/// envelope completo; reentrega do MESMO event_id não duplica projeção (INV-EVT-001);
/// evento fora de ordem (version menor) não retroage a projeção.
/// </summary>
public sealed class OutboxToProjectionE2ETests : IAsyncLifetime
{
    private const string MongoConn = "mongodb://127.0.0.1:27017/?directConnection=true";
    private const string PgConn = "Host=127.0.0.1;Username=atlas;Password=atlas_dev_only;Database=atlas_read";
    private const string Bootstrap = "127.0.0.1:29092";

    private readonly string _dbName = $"atlas_e2e_{Guid.NewGuid():N}";
    private readonly string _topic = $"atlas.catalog.v1.e2e.{Guid.NewGuid():N}";
    private readonly string _productsTable = $"catalog_product_search_it_{Guid.NewGuid():N}";
    private readonly string _processedTable = $"processed_events_it_{Guid.NewGuid():N}";
    private readonly ITestOutputHelper _output;
    private MongoCatalogStore _store = default!;
    private IMongoDatabase _db = default!;

    public OutboxToProjectionE2ETests(ITestOutputHelper output) => _output = output;

    public Task InitializeAsync()
    {
        _store = MongoCatalogStore.Create(MongoConn, _dbName);
        _db = new MongoClient(MongoConn).GetDatabase(_dbName);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Client.DropDatabaseAsync(_dbName);
        await using var conn = new NpgsqlConnection(PgConn);
        await conn.OpenAsync();
        await new NpgsqlCommand($"DROP TABLE IF EXISTS {_productsTable}; DROP TABLE IF EXISTS {_processedTable};", conn).ExecuteNonQueryAsync();
    }

    private async Task CreatePgTablesAsync()
    {
        await using var conn = new NpgsqlConnection(PgConn);
        await conn.OpenAsync();
        await new NpgsqlCommand($"""
            CREATE TABLE {_productsTable}(
                product_id uuid PRIMARY KEY, title text, market_id text,
                status text, source_version bigint, projected_at timestamptz,
                skus text[] DEFAULT ARRAY[]::text[]);
            CREATE TABLE {_processedTable}(
                consumer_id text, event_id uuid, applied_at timestamptz DEFAULT now(),
                PRIMARY KEY(consumer_id, event_id));
            """, conn).ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Outbox_kafka_projection_idempotente_ponta_a_ponta()
    {
        await CreatePgTablesAsync();

        // tópico criado explicitamente (sem depender de auto-create/propagação)
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = Bootstrap }).Build();
        try { await admin.CreateTopicsAsync([new TopicSpecification { Name = _topic, NumPartitions = 1, ReplicationFactor = 1 }]); }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists)) { }

        // 1) owner persiste aggregate + outbox na MESMA transação
        var created = await _store.UpsertDraftInTransactionAsync(
            new UpsertDraftCommand("e2e-1", "editor", "BR",
                [("pt-BR", "Tênis E2E")], ["ATL-E2E-1"]), CancellationToken.None);
        var submitted = await _store.SubmitForApprovalAsync(created.ProductId, created.Version, CancellationToken.None);
        var published = await _store.PublishInTransactionAsync(created.ProductId, submitted.Version, "approver", "BR", "corr-e2e", CancellationToken.None);
        Assert.True(published.Accepted, published.Reason);

        var outboxCol = _db.GetCollection<OutboxEntry>("catalog.outbox");
        var pending = await outboxCol.Find(e => !e.Published).SingleAsync();
        Assert.Equal("Product.PublishedV1", pending.EventType);
        // roteia o evento ao tópico efêmero do teste (produção usa o tópico canônico do doc)
        await outboxCol.UpdateOneAsync(e => e.Id == pending.Id,
            Builders<OutboxEntry>.Update.Set(e => e.Topic, _topic));

        // 2) dispatcher → Kafka
        var producerCfg = new ProducerConfig { BootstrapServers = Bootstrap, LingerMs = 5 };
        using var producer = new ProducerBuilder<string, string>(producerCfg).Build();
        var dispatcher = new OutboxDispatcher(_db, producer);
        var dispatched = await dispatcher.DispatchPendingBatchAsync("catalog.outbox", "catalog", CancellationToken.None);
        Assert.Equal(1, dispatched);
        _output.WriteLine($"[1] dispatcher publicou {dispatched} evento(s) em {_topic}");

        // 3) consumer lê o tópico (from beginning)
        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = Bootstrap,
            GroupId = $"it-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();
        consumer.Subscribe(_topic);

        var envelopes = new List<DomainEventEnvelope>();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (envelopes.Count < 1 && DateTime.UtcNow < deadline)
        {
            var msg = consumer.Consume(TimeSpan.FromSeconds(2));
            if (msg is null) continue;
            var raw = JsonDocument.Parse(msg.Message.Value);
            envelopes.Add(new DomainEventEnvelope(
                raw.RootElement.GetProperty("event_id").GetGuid(),
                raw.RootElement.GetProperty("event_type").GetString()!,
                raw.RootElement.GetProperty("aggregate_id").GetString()!,
                raw.RootElement.GetProperty("aggregate_version").GetInt64(),
                raw.RootElement.GetProperty("correlation_id").GetString()!,
                raw.RootElement.GetProperty("payload").Clone()));
        }
        Assert.Single(envelopes);
        Assert.Equal(Guid.Parse(pending.Id), envelopes[0].EventId);
        _output.WriteLine($"[2] consumido: {envelopes[0].EventType} ver={envelopes[0].AggregateVersion}");

        // 4) projector aplica → 1 linha
        var projector = new CatalogProjector(PgConn, "catalog-search-it", _productsTable, _processedTable);
        var applied = await projector.ApplyBatchAsync(envelopes, CancellationToken.None);
        Assert.Equal(1, applied);

        await using var conn = new NpgsqlConnection(PgConn);
        await conn.OpenAsync();
        var count1 = (long)(await new NpgsqlCommand($"SELECT count(*) FROM {_productsTable}", conn).ExecuteScalarAsync())!;
        Assert.Equal(1, count1);
        var version1 = (long)(await new NpgsqlCommand($"SELECT source_version FROM {_productsTable} LIMIT 1", conn).ExecuteScalarAsync())!;
        _output.WriteLine($"[3] projeção aplicada: rows={count1} source_version={version1}");

        // 5) REENTREGA do MESMO evento (at-least-once) → dedup, projeção não muda
        var appliedAgain = await projector.ApplyBatchAsync(envelopes, CancellationToken.None);
        Assert.Equal(0, appliedAgain);
        var count2 = (long)(await new NpgsqlCommand($"SELECT count(*) FROM {_productsTable}", conn).ExecuteScalarAsync())!;
        Assert.Equal(1, count2);
        _output.WriteLine("[4] reentrega do mesmo event_id: dedup — projeção permanece 1 linha");

        // 6) evento FORA DE ORDEM (version menor) não retroage
        var stale = envelopes[0] with { AggregateVersion = 1 };
        await projector.ApplyBatchAsync([stale], CancellationToken.None);
        var version2 = (long)(await new NpgsqlCommand($"SELECT source_version FROM {_productsTable} LIMIT 1", conn).ExecuteScalarAsync())!;
        Assert.Equal(version1, version2);
        _output.WriteLine($"[5] evento stale (ver=1) ignorado: source_version permanece {version2}");

        consumer.Close();
    }
}
