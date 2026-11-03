namespace Atlas.Inventory.Application;

using Atlas.Inventory.Domain;

/// <summary>Comando de reserva (idempotente por chave de checkout — INV-STOCK-003).</summary>
public sealed record ReserveStockCommand(
    string IdempotencyKey,
    IReadOnlyList<ReserveLineCommand> Lines,
    TimeSpan Ttl,
    string CorrelationId,
    string ActorSub);

public sealed record ReserveLineCommand(Guid WarehouseId, string SkuId, int Quantity);

public enum ReserveStockStatus { Granted, DuplicateKey, OutOfStock }

/// <summary>Resultado inequívoco (nada de exceção como fluxo de negócio).</summary>
public sealed record ReserveStockResult(
    ReserveStockStatus Status,
    Reservation? Reservation,
    IReadOnlyList<string> DeniedSkus)
{
    public static ReserveStockResult Granted(Reservation r) => new(ReserveStockStatus.Granted, r, []);
    public static ReserveStockResult Duplicate(Reservation r) => new(ReserveStockStatus.DuplicateKey, r, []);
    public static ReserveStockResult OutOfStock(IReadOnlyList<string> skus) => new(ReserveStockStatus.OutOfStock, null, skus);
}

/// <summary>
/// Port de persistência: a atomicidade (posição condicional + reserva + ledger + outbox)
/// é UMA transação de banco no adapter — o handler não conhece o mecanismo (DIP).
/// </summary>
public interface IInventoryStore
{
    Task<Reservation?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
    Task<ReserveStockResult> ReserveInTransactionAsync(
        Reservation reservation,
        IReadOnlyList<StockKey> conditionalKeys,
        string correlationId,
        string actorSub,
        CancellationToken ct);
    Task<bool> ConfirmReservationAsync(Guid reservationId, string orderId, CancellationToken ct);
    Task<bool> ReleaseReservationAsync(Guid reservationId, string reason, string correlationId, string actorSub, CancellationToken ct);
}

public sealed class ReserveStockHandler(IInventoryStore store)
{
    public async Task<ReserveStockResult> HandleAsync(ReserveStockCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            throw new ArgumentException("idempotency_key obrigatória");

        var existing = await store.FindByIdempotencyKeyAsync(command.IdempotencyKey, ct);
        if (existing is not null)
            return ReserveStockResult.Duplicate(existing); // mesma chave → mesma reserva (INV-STOCK-003)

        var reservation = Reservation.Create(
            command.IdempotencyKey,
            [.. command.Lines.Select(l => new ReservationLine(StockKey.Create(l.WarehouseId, l.SkuId), l.Quantity))],
            nowUtc: DateTimeOffset.UtcNow,
            ttl: command.Ttl);

        return await store.ReserveInTransactionAsync(
            reservation,
            [.. reservation.Lines.Select(l => l.Key)],
            command.CorrelationId,
            command.ActorSub,
            ct);
    }
}
