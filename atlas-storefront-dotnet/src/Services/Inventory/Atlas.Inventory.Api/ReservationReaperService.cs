using MongoInventoryStore = Atlas.Inventory.Infrastructure.MongoInventoryStore;

namespace Atlas.Inventory.Api;

/// <summary>
/// Backstop do ledger: reservas Active com prazo vencido voltam ao disponível
/// (release transacional + outbox Reservation.ReleasedV1). Sem isso, crashes entre
/// reserve e outcome prendem saldo até intervenção manual (M9-hold).
/// Reservas de pedidos confirmados têm status Confirmed — nunca são tocadas aqui.
/// </summary>
public sealed partial class ReservationReaperService(
    MongoInventoryStore store,
    IConfiguration config,
    ILogger<ReservationReaperService> log) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "reaper: {Count} reservas expiradas liberadas")]
    private static partial void LogFreed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "reaper: falha na varredura (próximo ciclo retenta)")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromSeconds(Math.Max(30,
            config.GetValue("Reaper:PeriodSeconds", 120)));
        // Primeira varredura após o boot, sem atrasar o /health.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var freed = await store.ReapExpiredReservationsAsync(
                    DateTimeOffset.UtcNow, take: 100, stoppingToken).ConfigureAwait(false);
                if (freed > 0) LogFreed(log, freed);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                LogSweepFailed(log, ex);
            }
            await Task.Delay(period, stoppingToken).ConfigureAwait(false);
        }
    }
}
