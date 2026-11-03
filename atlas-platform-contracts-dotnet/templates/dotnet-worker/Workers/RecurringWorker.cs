namespace __WORKER__.Workers;

/// <summary>
/// Worker recorrente base: PeriodicTimer + CancellationToken até o fim + graceful shutdown.
/// Substituir a lógica pela responsabilidade real (projection/reconciliation/task — SPECs 002+).
/// </summary>
public sealed class RecurringWorker(ILogger<RecurringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                // TODO(spec do worker): consumo idempotente com inbox/outbox conforme ADR-0003/0004.
                logger.LogInformation("Tick do worker em {Instant:O}", DateTimeOffset.UtcNow);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown gracioso
        }
    }
}
