using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Atlas.Workers.Projections;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MongoDB.Driver;

var builder = Host.CreateApplicationBuilder(args);

// Mongo (outbox dos owners)
var mongoConn = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://127.0.0.1:27017/?directConnection=true";
var mongoClient = new MongoClient(mongoConn);

// Kafka (bootstrap por config/env)
var bootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "127.0.0.1:29092";

// PostgreSQL (views)
var pgConn = builder.Configuration["Pg:ConnectionString"] ?? "Host=127.0.0.1;Username=atlas;Password=atlas_dev_only;Database=atlas_read";

// dispatcher único (producer compartilhado)
using var producer = new ProducerBuilder<string, string>(new ProducerConfig
{
    BootstrapServers = bootstrap,
    LingerMs = 5,
}).Build();
// UM dispatcher POR OWNER DB (SPEC-004: bug real — o dispatcher único apontava para
// atlas_catalog e o outbox de inventory/offers NUNCA saía em modo live)
var dispatcherCatalog = new OutboxDispatcher(mongoClient.GetDatabase("atlas_catalog"), producer);
var dispatcherOffers = new OutboxDispatcher(mongoClient.GetDatabase("atlas_offers"), producer);
var dispatcherInventory = new OutboxDispatcher(mongoClient.GetDatabase("atlas_inventory"), producer);

// consumers por contexto (group id por worker; earliest p/ replay)
builder.Services.AddHostedService(sp => new ProjectionWorkerHostedService(
    dispatcherCatalog,
    dispatcherOffers,
    dispatcherInventory,
    new CatalogProjector(pgConn, "catalog-search-worker"),
    new OffersProjector(pgConn, "offers-view-worker"),
    new InventoryProjector(pgConn, "inventory-view-worker"),
    bootstrap,
    mongoClient));

var host = builder.Build();

// schema das views + tópicos canônicos (sem depender de auto-create)
await ProjectionSchema.EnsureAsync(pgConn, "catalog_product_search", "available_offer_view", "inventory_admin_view", "processed_events", CancellationToken.None);
using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build();
try
{
    await admin.CreateTopicsAsync(
    [
        new TopicSpecification { Name = "atlas.catalog.v1", NumPartitions = 1, ReplicationFactor = 1 },
        new TopicSpecification { Name = "atlas.offers.v1", NumPartitions = 1, ReplicationFactor = 1 },
        new TopicSpecification { Name = "atlas.inventory.v1", NumPartitions = 1, ReplicationFactor = 1 },
    ]);
}
catch (CreateTopicsException)
{
    // tópicos canônicos: já existentes é o caminho feliz (operação idempotente)
}

host.Run();

/// <summary>
/// Worker de projeções (M2): loop contínuo — (1) despacha outboxes dos owners ao Kafka;
/// (2) consome os tópicos e aplica as views PG com dedup transacional (INV-EVT-001).
/// Cancelamento cooperativo; delay entre ciclos; sem fire-and-forget.
/// </summary>
public sealed class ProjectionWorkerHostedService(
    OutboxDispatcher dispatcherCatalog,
    OutboxDispatcher dispatcherOffers,
    OutboxDispatcher dispatcherInventory,
    CatalogProjector catalogProjector,
    OffersProjector offersProjector,
    InventoryProjector inventoryProjector,
    string bootstrap,
    IMongoClient mongoClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = "atlas-projections-v1",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();
        consumer.Subscribe(["atlas.catalog.v1", "atlas.offers.v1", "atlas.inventory.v1"]);

        var lastDispatch = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1) dispatch dos outboxes (a cada ciclo)
                if (DateTimeOffset.UtcNow - lastDispatch > TimeSpan.FromSeconds(5))
                {
                    await dispatcherCatalog.DispatchPendingBatchAsync("catalog.outbox", "catalog", stoppingToken);
                    await dispatcherOffers.DispatchPendingBatchAsync("offers.outbox", "offers", stoppingToken);
                    var inv = await dispatcherInventory.DispatchPendingBatchAsync("inventory.outbox", "inventory", stoppingToken);
                    if (inv > 0) Console.WriteLine($"[projections] inventory despachados: {inv}");
                    lastDispatch = DateTimeOffset.UtcNow;
                }

                // 2) consume e aplica (dedup transacional por evento)
                var message = consumer.Consume(TimeSpan.FromSeconds(1));
                if (message is null) continue;
                var raw = JsonDocument.Parse(message.Message.Value).RootElement;
                var envelope = new DomainEventEnvelope(
                    raw.GetProperty("event_id").GetGuid(),
                    raw.GetProperty("event_type").GetString()!,
                    raw.GetProperty("aggregate_id").GetString()!,
                    raw.GetProperty("aggregate_version").GetInt64(),
                    raw.GetProperty("correlation_id").GetString()!,
                    raw.GetProperty("payload").Clone());

                var topic = message.Topic;
                var applied = topic.Contains("catalog") ? await catalogProjector.ApplyBatchAsync([envelope], stoppingToken)
                    : topic.Contains("offers") ? await offersProjector.ApplyBatchAsync([envelope], stoppingToken)
                    : await inventoryProjector.ApplyBatchAsync([envelope], stoppingToken);
                consumer.Commit(message);
                if (applied > 0)
                    Console.WriteLine($"[projections] {topic} {envelope.EventType} aplicado (ver {envelope.AggregateVersion})");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[projections] ciclo com falha (retoma): {ex.GetType().Name}: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        consumer.Close();
    }
}
