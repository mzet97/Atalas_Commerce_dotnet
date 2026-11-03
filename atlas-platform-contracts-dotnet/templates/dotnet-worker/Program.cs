using __WORKER__.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOpenTelemetry(); // metrics/traces do worker; OTLP via env
builder.Services.AddHostedService<RecurringWorker>();

var host = builder.Build();
host.Run();
