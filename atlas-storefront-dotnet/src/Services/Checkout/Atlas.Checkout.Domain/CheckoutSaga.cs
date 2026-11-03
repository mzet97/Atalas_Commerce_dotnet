namespace Atlas.Checkout.Domain;

public readonly record struct CheckoutId(Guid Value)
{
    public static CheckoutId New() => new(Guid.NewGuid());
}

public enum CheckoutSagaState
{
    Submitted = 0,               // validado, iniciando
    StockReserved = 1,           // reserva autoritativa OK
    OrderPlaced = 2,             // Order PENDING_PAYMENT criado com snapshot
    PaymentAuthorized = 3,
    Confirmed = 4,               // pedido confirmado + captura
    PaymentDeclined = 5,         // terminal: reserva liberada
    PendingReconciliation = 6,   // PSP UNKNOWN: reconciliador resolve
    Compensating = 7,            // desfazendo (libera reserva etc.)
    Compensated = 8,             // terminal
}

/// <summary>
/// Saga de checkout persistida (ADR-0006): cada transição é explicitamente permitida,
/// idempotente (reexecução do mesmo passo não avança duas vezes) e reversível via
/// compensações. A regra de negócio pesada fica nos owners — a saga coordena.
/// </summary>
public sealed partial class CheckoutSaga
{
    private readonly List<string> _stepsApplied = [];

    public CheckoutId Id { get; }
    public string IdempotencyKey { get; }
    public string CustomerId { get; }
    public long TotalMinor { get; }
    public string Currency { get; }
    public CheckoutSagaState State { get; private set; } = CheckoutSagaState.Submitted;
    public string? ReservationId { get; private set; }
    public string? OrderId { get; private set; }
    public string? PaymentIntentId { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public IReadOnlyList<string> StepsApplied => _stepsApplied;

    private CheckoutSaga(CheckoutId id, string idempotencyKey, string customerId, long totalMinor, string currency, DateTimeOffset createdAtUtc)
    {
        Id = id;
        IdempotencyKey = idempotencyKey;
        CustomerId = customerId;
        TotalMinor = totalMinor;
        Currency = currency;
        CreatedAtUtc = createdAtUtc;
    }

    public static CheckoutSaga Start(string idempotencyKey, string customerId, long totalMinor, string currency, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("idempotency_key obrigatória");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalMinor);
        return new(CheckoutId.New(), idempotencyKey, customerId, totalMinor, currency, nowUtc);
    }

    public bool MarkStockReserved(string reservationId, DateTimeOffset nowUtc)
    {
        if (State != CheckoutSagaState.Submitted) return State == CheckoutSagaState.StockReserved; // idempotente
        if (string.IsNullOrWhiteSpace(reservationId)) return false;
        ReservationId = reservationId;
        State = CheckoutSagaState.StockReserved;
        _stepsApplied.Add($"stock-reserved:{reservationId}:{nowUtc:O}");
        return true;
    }

    public bool MarkOrderPlaced(string orderId, DateTimeOffset nowUtc)
    {
        if (State != CheckoutSagaState.StockReserved) return State == CheckoutSagaState.OrderPlaced;
        OrderId = orderId;
        State = CheckoutSagaState.OrderPlaced;
        _stepsApplied.Add($"order-placed:{orderId}:{nowUtc:O}");
        return true;
    }

    public bool MarkPaymentAuthorized(string paymentIntentId, DateTimeOffset nowUtc)
    {
        if (State != CheckoutSagaState.OrderPlaced) return State == CheckoutSagaState.PaymentAuthorized;
        PaymentIntentId = paymentIntentId;
        State = CheckoutSagaState.PaymentAuthorized;
        _stepsApplied.Add($"payment-authorized:{paymentIntentId}:{nowUtc:O}");
        return true;
    }

    public bool MarkConfirmed(DateTimeOffset nowUtc)
    {
        if (State != CheckoutSagaState.PaymentAuthorized) return State == CheckoutSagaState.Confirmed;
        State = CheckoutSagaState.Confirmed;
        _stepsApplied.Add($"confirmed:{nowUtc:O}");
        return true;
    }

    public bool MarkPaymentDeclined(string reason, DateTimeOffset nowUtc)
    {
        if (State is not (CheckoutSagaState.StockReserved or CheckoutSagaState.OrderPlaced)) 
            return State == CheckoutSagaState.PaymentDeclined;
        FailureReason = reason;
        State = CheckoutSagaState.PaymentDeclined;
        _stepsApplied.Add($"payment-declined:{reason}:{nowUtc:O}");
        return true;
    }

    public bool MarkPendingReconciliation(string reason, DateTimeOffset nowUtc)
    {
        if (State != CheckoutSagaState.OrderPlaced) return State == CheckoutSagaState.PendingReconciliation;
        FailureReason = reason;
        State = CheckoutSagaState.PendingReconciliation;
        _stepsApplied.Add($"pending-reconciliation:{reason}:{nowUtc:O}");
        return true;
    }

    /// <summary>Inicia compensação (liberação de reserva + cancelamento do pedido conforme estado).</summary>
    public bool BeginCompensation(string reason, DateTimeOffset nowUtc)
    {
        if (State is not (CheckoutSagaState.Submitted or CheckoutSagaState.StockReserved
            or CheckoutSagaState.OrderPlaced or CheckoutSagaState.PaymentAuthorized
            or CheckoutSagaState.PaymentDeclined or CheckoutSagaState.PendingReconciliation))
            return State == CheckoutSagaState.Compensated;
        FailureReason = reason;
        State = CheckoutSagaState.Compensating;
        _stepsApplied.Add($"compensating:{reason}:{nowUtc:O}");
        return true;
    }

    public bool MarkCompensated(DateTimeOffset nowUtc)
    {
        if (State != CheckoutSagaState.Compensating) return State == CheckoutSagaState.Compensated;
        State = CheckoutSagaState.Compensated;
        _stepsApplied.Add($"compensated:{nowUtc:O}");
        return true;
    }
}

public partial class CheckoutSaga
{
    /// <summary>
    /// Reconciliação paga (UC003-T03): PendingReconciliation → Confirmed direto.
    /// OrderPlaced também é fonte válida: saga CRASHADA antes de persistir o resultado
    /// do PSP (recuperação — o PSP confirmou pago, a compra não se perde).
    /// </summary>
    public bool MarkReconciliationPaid(string paymentIntentId, string paymentReference, DateTimeOffset nowUtc)
    {
        if (State is not (CheckoutSagaState.PendingReconciliation or CheckoutSagaState.OrderPlaced))
            return State == CheckoutSagaState.Confirmed;
        PaymentIntentId = paymentIntentId;
        ProviderReference = string.IsNullOrWhiteSpace(paymentReference) ? ProviderReference : paymentReference;
        State = CheckoutSagaState.Confirmed;
        _stepsApplied.Add($"reconciliation-paid:{paymentIntentId}:{paymentReference}:{nowUtc:O}");
        return true;
    }

    /// <summary>Reconciliação não-pago: PendingReconciliation → Compensated direto.</summary>
    public bool MarkReconciliationNotPaid(string reason, DateTimeOffset nowUtc)
    {
        if (State != CheckoutSagaState.PendingReconciliation) return State == CheckoutSagaState.Compensated;
        FailureReason = reason;
        State = CheckoutSagaState.Compensated;
        _stepsApplied.Add($"reconciliation-not-paid:{reason}:{nowUtc:O}");
        return true;
    }
}

public partial class CheckoutSaga
{
    /// <summary>Reidratação a partir do store — preserva o Id e o histórico.</summary>
    public static CheckoutSaga Hydrate(CheckoutId id, string idempotencyKey, string customerId,
        long totalMinor, string currency, DateTimeOffset createdAtUtc)
    {
        return new CheckoutSaga(id, idempotencyKey, customerId, totalMinor, currency, createdAtUtc);
    }

    /// <summary>Restaura a referência de reconciliação do PSP persistida pelo store (uso exclusivo de reidratação).</summary>
    public void RestoreReconciliationReference(string paymentIntentId, string providerReference, bool confirmed)
    {
        PaymentIntentId = paymentIntentId;
        ProviderReference = providerReference;
        if (confirmed) State = CheckoutSagaState.Confirmed;
        _stepsApplied.Add($"reconciliation-restored:{paymentIntentId}:{providerReference}");
    }
}
