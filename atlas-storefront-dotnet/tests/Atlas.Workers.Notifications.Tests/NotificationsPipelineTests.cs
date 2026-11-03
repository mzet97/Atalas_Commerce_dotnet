using System.Text;
using System.Text.Json;
using Atlas.Workers.Notifications;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Workers.Notifications.Tests;

/// <summary>
/// Integração REAL (SPEC-006 req.3 + gate): RabbitMQ 4 com quorum queues + replica set Mongo.
/// Cobre os três aceites do gate M6:
///   (1) notificação NUNCA duplicada sob concorrência (dedup por índice único);
///   (2) retry com backoff funciona (poison volta pela fila de retry TTL);
///   (3) veneno final cai na DLQ observável.
/// </summary>
public sealed class NotificationsPipelineTests : IDisposable
{
    private const string Amqp = "amqp://atlas:atlas_dev_only@127.0.0.1:5672/";
    private const string Mongo = "mongodb://127.0.0.1:27017/?directConnection=true";
    private readonly string _dbName = $"atlas_notif_{Guid.NewGuid():N}";

    private NotificationDedupStore NewStore() =>
        NotificationDedupStore.Create(Mongo, _dbName);

    [Fact]
    public async Task Dedup_concorrente_exatamente_um_envio()
    {
        var store = NewStore();
        var key = $"order-confirmed:{Guid.NewGuid()}";

        // 8 workers disputam a mesma notification_key simultaneamente
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            store.TryClaimAsync(key, "email", Guid.NewGuid().ToString(), CancellationToken.None)));

        Assert.Equal(1, claims.Count(c => c));                    // exatamente UM ganha
        Assert.Equal(1, await store.CountSentAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Poison_vai_para_DLQ_após_retry_e_boa_tarefa_entrega()
    {
        var sent = new List<string>();
        using var rabbit = new RabbitTaskConsumer(Amqp, (msg, ct) =>
        {
            if (!msg.IsPoison) sent.Add(msg.NotificationKey);
            return Task.CompletedTask;
        }, maxAttempts: 3);
        await rabbit.DeclareTopologyAsync(CancellationToken.None);

        var poisonKey = $"poison:{Guid.NewGuid():N}";
        var goodKey = $"order-confirmed:{Guid.NewGuid():N}";
        await rabbit.PublishTaskAsync(new TaskMessage(poisonKey, "email", "pt-BR", "c1", "poison", new Dictionary<string, string>()), CancellationToken.None);
        await rabbit.PublishTaskAsync(new TaskMessage(goodKey, "email", "pt-BR", "c1", "order-confirmed", new Dictionary<string, string> { ["order_id"] = "o-1" }), CancellationToken.None);

        // drena 4 mensagens: poison(1ª) → retry TTL; good → ack; poison volta(2ª) → retry; poison final → DLQ
        await rabbit.DrainAsync(maxMessages: 4, CancellationToken.None);
        await Task.Delay(4200); // TTL da fila de retry (3s) + margem

        Assert.Equal(1UL, await rabbit.DlqCountAsync(CancellationToken.None));  // veneno na DLQ
        Assert.Equal([goodKey], sent);                                        // boa entregue 1×
    }

    [Fact]
    public async Task Dedup_persiste_após_recriar_o_store()
    {
        var key = $"order-shipped:{Guid.NewGuid():N}";
        var s1 = NewStore();
        Assert.True(await s1.TryClaimAsync(key, "email", Guid.NewGuid().ToString(), CancellationToken.None));
        // restart do worker = nova instância do store sobre o mesmo db
        var s2 = NewStore();
        Assert.False(await s2.TryClaimAsync(key, "email", Guid.NewGuid().ToString(), CancellationToken.None));
    }

    public void Dispose()
    {
        new MongoClient(Mongo).DropDatabaseAsync(_dbName).GetAwaiter().GetResult();
    }
}
