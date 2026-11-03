namespace Atlas.Inventory.Domain;

/// <summary>Identidades fortes do contexto Inventory.</summary>
public readonly record struct WarehouseId(Guid Value)
{
    public static WarehouseId New() => new(Guid.NewGuid());
    public bool IsValid => Value != Guid.Empty;
}

/// <summary>Chave composta da posição de estoque (warehouse + SKU) — blueprint #02 §2.</summary>
public readonly record struct StockKey(Guid WarehouseId, string SkuId)
{
    public static StockKey Create(Guid warehouseId, string skuId) =>
        string.IsNullOrWhiteSpace(skuId)
            ? throw new ArgumentException("skuId obrigatório", nameof(skuId))
            : new StockKey(warehouseId, skuId);

    public override string ToString() => $"{WarehouseId:N}:{SkuId}";
}

public enum ReservationStatus
{
    Active = 0,
    Confirmed = 1,
    Released = 2,
    Expired = 3,
    Consumed = 4,
}

/// <summary>
/// Agregado StockPosition (INV-STOCK-001): saldo por warehouse+SKU com versão para
/// concorrência otimista. A atomicidade da reserva (CAS condicional) é aplicada pelo
/// repositório/store via filtro "version esperada E available suficiente" — o domínio
/// expõe a regra pura (<see cref="CanReserve"/>/<see cref="ApplyReserve"/>) que o store
/// executa condicionalmente. Redis/PostgreSQL/Kafka nunca decidem disponibilidade.
/// </summary>
public sealed class StockPosition
{
    public StockKey Key { get; }
    public int OnHand { get; private set; }
    public int Reserved { get; private set; }
    public int SafetyStock { get; private set; }
    public long Version { get; private set; } = 1;

    private StockPosition(StockKey key, int onHand, int reserved, int safetyStock, long version)
    {
        Key = key;
        OnHand = onHand;
        Reserved = reserved;
        SafetyStock = safetyStock;
        Version = version;
    }

    public static StockPosition Open(StockKey key, int safetyStock = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(safetyStock);
        return new StockPosition(key, onHand: 0, reserved: 0, safetyStock, version: 1);
    }

    /// <summary>Reconstrução a partir do store (write model Mongo) — sem revalidação de história.</summary>
    public static StockPosition Hydrate(StockKey key, int onHand, int reserved, int safetyStock, long version) =>
        new(key, onHand, reserved, safetyStock, version);

    public int Available => OnHand - Reserved - SafetyStock;

    public bool CanReserve(int quantity) =>
        quantity > 0 && Available >= quantity;

    /// <summary>Aplica a reserva CONDITIONADA a <paramref name="expectedVersion"/> (regra CAS do store).</summary>
    /// <returns>false quando a versão não confere (conflito de concorrência — reler e tentar de novo) ou saldo insuficiente.</returns>
    public bool TryApplyReserve(int quantity, long expectedVersion)
    {
        if (Version != expectedVersion) return false;
        if (!CanReserve(quantity)) return false;
        Reserved += quantity;
        Version++;
        return true;
    }

    public void Receive(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        OnHand += quantity;
        Version++;
    }

    /// <summary>Ajuste (delta negativo permitido até o disponível — INV-STOCK-002 com motivo/ator no ledger).</summary>
    public void Adjust(int delta)
    {
        if (delta == 0) throw new ArgumentException("Ajuste de zero não gera movimento");
        var newOnHand = OnHand + delta;
        if (newOnHand < 0) throw new InvalidOperationException("Ajuste deixaria on_hand negativo");
        if (delta < 0 && Math.Abs(delta) > Available)
            throw new InvalidOperationException("Ajuste negativo maior que o disponível (reservas ativas protegem o saldo)");
        OnHand = newOnHand;
        Version++;
    }

    public void Release(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (Reserved < quantity) throw new InvalidOperationException("Liberação maior que o reservado");
        Reserved -= quantity;
        Version++;
    }
}

/// <summary>
/// Movimento auditável append-only (Stock Ledger — INV-STOCK-002): motivo e ator obrigatórios.
/// </summary>
public sealed record StockMovement(
    Guid MovementId,
    StockKey PositionKey,
    int Delta,
    string Reason,
    string ActorSub,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc)
{
    public static StockMovement Create(StockKey positionKey, int delta, string reason, string actorSub, string correlationId, DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Motivo obrigatório (INV-STOCK-002)", nameof(reason));
        if (string.IsNullOrWhiteSpace(actorSub)) throw new ArgumentException("Ator obrigatório", nameof(actorSub));
        return new(Guid.NewGuid(), positionKey, delta, reason, actorSub, correlationId, occurredAtUtc);
    }
}

/// <summary>
/// Agregado Reservation (blueprint #02 §2; INV-STOCK-003): idempotente por chave de checkout;
/// reservas CONFIRMADAS não expiram por TTL (expiração é transição explícita de domínio).
/// A unicidade da idempotency key é índice único no store; o domínio documenta a regra.
/// </summary>
public sealed partial class Reservation
{
    public Guid Id { get; }
    public string IdempotencyKey { get; }        // = checkout_id
    public IReadOnlyList<ReservationLine> Lines { get; }
    public ReservationStatus Status { get; private set; } = ReservationStatus.Active;
    public DateTimeOffset ExpiresAtUtc { get; }
    public string? OrderId { get; private set; }
    public long Version { get; private set; } = 1;

    private Reservation(Guid id, string idempotencyKey, IReadOnlyList<ReservationLine> lines, DateTimeOffset expiresAtUtc)
    {
        Id = id;
        IdempotencyKey = idempotencyKey;
        Lines = lines;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static Reservation Create(string idempotencyKey, IReadOnlyList<ReservationLine> lines, DateTimeOffset nowUtc, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("idempotency_key obrigatória", nameof(idempotencyKey));
        if (lines.Count == 0) throw new ArgumentException("Reserva sem linhas", nameof(lines));
        if (lines.Any(l => l.Quantity <= 0)) throw new ArgumentException("Quantidade deve ser > 0");
        return new(Guid.NewGuid(), idempotencyKey, lines, nowUtc.Add(ttl));
    }

    /// <summary>Confirmação (pós-pagamento) — fixa OrderId e impede expiração futura (ADR-0016).</summary>
    public void Confirm(string orderId, DateTimeOffset nowUtc)
    {
        EnsureStatus(ReservationStatus.Active, "confirmar");
        if (string.IsNullOrWhiteSpace(orderId)) throw new ArgumentException("orderId obrigatório", nameof(orderId));
        if (nowUtc >= ExpiresAtUtc)
            throw new InvalidOperationException("Reserva expirada: confirmar exige revalidação/nova reserva (INV-QUOTE-001 análogo)");
        Status = ReservationStatus.Confirmed;
        OrderId = orderId;
        Version++;
    }

    public void Release(string reason)
    {
        if (Status is not (ReservationStatus.Active or ReservationStatus.Confirmed))
            throw new InvalidOperationException($"Transição inválida: {Status} → Released");
        Status = ReservationStatus.Released;
        Version++;
    }

    /// <summary>Expiração por job de domínio (TTL acelera, nunca substitui — INV-STOCK-002).</summary>
    public void MarkExpired(DateTimeOffset nowUtc)
    {
        EnsureStatus(ReservationStatus.Active, "expirar");
        if (nowUtc < ExpiresAtUtc) throw new InvalidOperationException("Reserva ainda vigente");
        Status = ReservationStatus.Expired;
        Version++;
    }

    public void MarkConsumed()
    {
        EnsureStatus(ReservationStatus.Confirmed, "consumir");
        Status = ReservationStatus.Consumed;
        Version++;
    }

    private void EnsureStatus(ReservationStatus expected, string action)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Transição inválida: {Status} → {action} (esperado {expected})");
    }
}

public sealed record ReservationLine(StockKey Key, int Quantity);

/// <summary>Eventos de domínio do contexto (envelope aplicado em Application).</summary>
public sealed record StockReserved(string ReservationId, string IdempotencyKey, long AggregateVersion, DateTimeOffset OccurredAtUtc);
public sealed record ReservationConfirmed(string ReservationId, string OrderId, long AggregateVersion, DateTimeOffset OccurredAtUtc);
public sealed record ReservationReleased(string ReservationId, string Reason, long AggregateVersion, DateTimeOffset OccurredAtUtc);
public sealed record ReservationExpired(string ReservationId, long AggregateVersion, DateTimeOffset OccurredAtUtc);
public sealed record StockAdjusted(string PositionKey, int Delta, string Reason, long AggregateVersion, DateTimeOffset OccurredAtUtc);

public sealed partial class Reservation
{
    /// <summary>Reconstrução a partir do store (write model Mongo) — sem revalidar história.</summary>
    public static Reservation Hydrate(
        Guid id, string idempotencyKey, IReadOnlyList<ReservationLine> lines,
        DateTimeOffset expiresAtUtc, ReservationStatus status, string? orderId, long version) => new(id, idempotencyKey, lines, expiresAtUtc)
        {
            Status = status,
            OrderId = orderId,
            Version = version,
        };
}
