namespace Atlas.Workers.Notifications;

using System.Text.Json;
using Confluent.Kafka;
using MongoDB.Driver;

/// <summary>
/// Worker de notificações (SPEC-006 req.3): consome eventos de PEDIDO do Kafka
/// (atlas.orders.v1), dedup por notification_key (índice único) e publica TAREFAS
/// no RabbitMQ (quorum). Duplicado nunca re-envia; evento confirmado nunca re-envia
/// mesmo após restart do worker (dedup persistido no replica set).
/// </summary>
public sealed class NotificationsWorker(
    NotificationDedupStore dedup,
    RabbitTaskConsumer rabbit,
    string bootstrap) : BackgroundService
{
    private const string TopicOrders = "atlas.orders.v1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await rabbit.DeclareTopologyAsync(stoppingToken);

        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = "atlas-notifications-v1",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();
        consumer.Subscribe(TopicOrders);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = consumer.Consume(TimeSpan.FromSeconds(1));
                if (message is null) continue;
                var raw = JsonDocument.Parse(message.Message.Value).RootElement;
                var envelope = new EventEnvelope(
                    raw.GetProperty("event_id").GetGuid(),
                    raw.GetProperty("event_type").GetString()!,
                    raw.GetProperty("aggregate_id").GetString()!,
                    raw.GetProperty("aggregate_version").GetInt64(),
                    raw.GetProperty("correlation_id").GetString()!,
                    raw.GetProperty("payload").Clone());

                if (envelope.EventType is "Order.ConfirmedV1" or "Order.ShippedV1")
                {
                    var template = envelope.EventType == "Order.ConfirmedV1" ? "order-confirmed" : "order-shipped";
                    var key = $"{template}:{envelope.EventId}";
                    if (await dedup.TryClaimAsync(key, "email", envelope.EventId.ToString(), stoppingToken))
                    {
                        await rabbit.PublishTaskAsync(new TaskMessage(
                            NotificationKey: key,
                            Channel: "email",
                            Locale: "pt-BR",
                            CustomerId: envelope.Payload.TryGetProperty("customer_id", out var c)
                                ? c.GetString() ?? "guest" : "guest",
                            Template: template,
                            Args: new Dictionary<string, string>
                            {
                                ["order_id"] = envelope.AggregateId,
                            }), stoppingToken);
                        Console.WriteLine($"[notifications] tarefa publicada: {key}");
                    }
                    else
                    {
                        Console.WriteLine($"[notifications] dedup: {key} já enviada");
                    }
                }
                consumer.Commit(message);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[notifications] ciclo com falha (retoma): {ex.GetType().Name}: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        consumer.Close();
    }
}
