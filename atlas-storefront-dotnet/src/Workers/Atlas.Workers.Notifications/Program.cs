using Atlas.Workers.Notifications;
using MongoDB.Driver;

var builder = Host.CreateApplicationBuilder(args);

var mongoConn = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://127.0.0.1:27017/?directConnection=true";
var amqpUrl = builder.Configuration["Rabbit:Url"] ?? "amqp://atlas:atlas_dev_only@127.0.0.1:5672/";
var kafkaBootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "127.0.0.1:29092";

builder.Services.AddSingleton(NotificationDedupStore.Create(
    mongoConn, builder.Configuration["Mongo:NotificationsDatabase"] ?? "atlas_notifications"));
builder.Services.AddSingleton(sp => new RabbitTaskConsumer(
    amqpUrl,
    handler: (msg, ct) =>
    {
        // provedor FAKE determinístico (dev): envio = log. Provedor real com opt-out (REQ-CST-001) na SPEC-006+.
        Console.WriteLine($"[send] canal={msg.Channel} para={msg.CustomerId} tpl={msg.Template} pedido={msg.Args.GetValueOrDefault("order_id")}");
        return Task.CompletedTask;
    },
    maxAttempts: builder.Configuration.GetValue("Notifications:MaxAttempts", 3)));
builder.Services.AddHostedService(sp => new NotificationsWorker(
    sp.GetRequiredService<NotificationDedupStore>(),
    sp.GetRequiredService<RabbitTaskConsumer>(),
    kafkaBootstrap));

var host = builder.Build();
host.Run();

public partial class Program;
