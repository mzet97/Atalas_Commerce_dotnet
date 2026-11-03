using Microsoft.AspNetCore.Http.HttpResults;
using __SERVICE__.Application;
using __SERVICE__.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry(); // traces nativos ASP.NET Core 10; OTLP via OTEL_EXPORTER_OTLP_ENDPOINT
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IDraftStore, __SERVICE__.Infrastructure.InMemoryDraftStore>();
builder.Services.AddSingleton<SetDraftSummaryHandler>();

var app = builder.Build();
app.MapOpenApi();
app.MapHealthChecks("/health");

// Fatia vertical mínima de exemplo — substituir pelos endpoints reais do serviço.
app.MapPost("/drafts", async (SetDraftSummaryHandler handler, CancellationToken ct) =>
{
    var draft = Draft.Create();
    await handler.HandleAsync(new SetDraftSummaryCommand(draft.Id, "rascunho inicial"), ct);
    return Results.Created($"/drafts/{draft.Id}", new { draft.Id });
});

app.Run();

public partial class Program; // para Microsoft.AspNetCore.Mvc.Testing
