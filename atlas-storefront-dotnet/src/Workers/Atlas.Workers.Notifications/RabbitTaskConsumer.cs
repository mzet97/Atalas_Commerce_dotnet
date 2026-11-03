namespace Atlas.Workers.Notifications;

using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

/// <summary>
/// Produtor/consumidor da fila de TAREFAS de notificação (SPEC-006 req.3):
/// fila QUORUM durável, manual ack, retry com backoff (fila retry TTL→principal,
/// contador x-attempt) e DLQ notifications.dlq para veneno (gate: DLQ/retry provados).
/// </summary>
/// <summary>Tarefa de envio (uma notificação dedup por NotificationKey).</summary>
public sealed record TaskMessage(
    string NotificationKey,
    string Channel,
    string Locale,
    string CustomerId,
    string Template,
    IReadOnlyDictionary<string, string> Args)
{
    // veneno determinístico p/ teste de DLQ/retry: template "poison" nunca entrega
    public bool IsPoison => Template == "poison";
}

public sealed class RabbitTaskConsumer(
    string amqpUrl,
    Func<TaskMessage, CancellationToken, Task> handler,
    int maxAttempts = 3) : IDisposable
{
    public const string Exchange = "notifications.tasks.x";
    public const string Queue = "notifications.tasks.q";
    public const string RetryQueue = "notifications.tasks.retry";
    public const string DeadLetterQueue = "notifications.dlq";

    private readonly IConnection _connection = new ConnectionFactory
    {
        Uri = new Uri(amqpUrl),
    }.CreateConnectionAsync().GetAwaiter().GetResult();
    private IChannel? _channel;

    public async Task DeclareTopologyAsync(CancellationToken ct)
    {
        var channel = await ChannelAsync(ct);
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = Exchange,
                ["x-dead-letter-routing-key"] = DeadLetterQueue,
            }, cancellationToken: ct);
        await channel.QueueBindAsync(Queue, Exchange, Queue, cancellationToken: ct);
        await channel.QueueDeclareAsync(RetryQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = Exchange,
                ["x-dead-letter-routing-key"] = Queue,
                ["x-message-ttl"] = 3000,
            }, cancellationToken: ct);
        await channel.QueueBindAsync(RetryQueue, Exchange, RetryQueue, cancellationToken: ct);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: ct);
        await channel.QueueBindAsync(DeadLetterQueue, Exchange, DeadLetterQueue, cancellationToken: ct);
    }

    public async Task PublishTaskAsync(TaskMessage msg, CancellationToken ct)
    {
        var channel = await ChannelAsync(ct);
        await channel.BasicPublishAsync(Exchange, Queue, mandatory: false,
            new BasicProperties
            {
                Persistent = true,
                Headers = new Dictionary<string, object?> { ["x-attempt"] = 0 },
            }, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg)), ct);
    }

    /// <summary>Drena até N mensagens: sucesso→ack; veneno com tentativas&lt;max→retry(TTL 3s); veneno final→DLQ.</summary>
    public async Task<int> DrainAsync(int maxMessages, CancellationToken ct)
    {
        var channel = await ChannelAsync(ct);
        var processed = 0;
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var msg = JsonSerializer.Deserialize<TaskMessage>(Encoding.UTF8.GetString(ea.Body.ToArray()))
                          ?? throw new FormatException("payload ilegível");
                var attempt = ReadAttempt(ea);

                if (msg.IsPoison && attempt + 1 < maxAttempts)
                {
                    await channel.BasicPublishAsync(Exchange, RetryQueue, mandatory: false,
                        new BasicProperties
                        {
                            Persistent = true,
                            Headers = new Dictionary<string, object?> { ["x-attempt"] = attempt + 1 },
                        }, ea.Body.ToArray(), ct);
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                else if (msg.IsPoison)
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false); // → DLQ
                }
                else
                {
                    await handler(msg, ct);
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
            }
            catch
            {
                await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false); // → DLQ
            }
            processed++;
            if (processed >= maxMessages) drained.TrySetResult();
        };
        await channel.BasicConsumeAsync(Queue, autoAck: false, consumer, cancellationToken: ct);
        await drained.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
        return processed;
    }

    public async Task<ulong> DlqCountAsync(CancellationToken ct)
    {
        var channel = await ChannelAsync(ct);
        var result = await channel.QueueDeclarePassiveAsync(DeadLetterQueue, cancellationToken: ct);
        return result.MessageCount;
    }

    private async Task<IChannel> ChannelAsync(CancellationToken ct) =>
        _channel ??= await _connection.CreateChannelAsync(cancellationToken: ct);

    private static int ReadAttempt(BasicDeliverEventArgs ea) =>
        ea.BasicProperties.Headers?.TryGetValue("x-attempt", out var v) == true
            ? Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture)
            : 0;

    public void Dispose()
    {
        _channel?.Dispose();
        _connection.Dispose();
    }
}
