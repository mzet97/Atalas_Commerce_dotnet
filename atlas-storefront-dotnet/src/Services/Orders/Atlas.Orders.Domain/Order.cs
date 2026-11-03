namespace Atlas.Orders.Domain;

/// <summary>Dinheiro em minor units (INV-MONEY-001) — cópia local até pacote de contratos publicado.</summary>
public readonly partial record struct Money
{
    public long AmountMinor { get; }
    public string Currency { get; }

    private Money(long amountMinor, string currency)
    {
        AmountMinor = amountMinor;
        Currency = currency;
    }

    public static Money Create(long amountMinor, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 || !currency.All(char.IsLetter))
            throw new ArgumentException($"Moeda inválida: '{currency}'");
        if (amountMinor < 0)
            throw new ArgumentOutOfRangeException(nameof(amountMinor), "Valor monetário não pode ser negativo");
        return new Money(amountMinor, currency.ToUpperInvariant());
    }

    public Money Add(Money other) =>
        other.Currency == Currency
            ? new Money(checked(AmountMinor + other.AmountMinor), Currency)
            : throw new InvalidOperationException($"Moedas incompatíveis: {Currency} × {other.Currency}");
}

public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public bool IsValid => Value != Guid.Empty;
}

/// <summary>Snapshot imutável de linha do pedido (INV-ORDER-001 — nunca recalcula de offers correntes).</summary>
public sealed record OrderLineSnapshot(
    string SkuId,
    string OfferId,
    int Quantity,
    Money UnitPriceMinor,
    Money LineTotal);

/// <summary>Snapshot de endereço (elogio de privacidade: mínimos dados, retenção por política).</summary>
public sealed record ShippingAddressSnapshot(string Country, string PostalCode, string Line1, string City);

public enum OrderState
{
    PendingPayment = 0,
    Confirmed = 1,
    PaymentFailed = 2,
    Cancelled = 3,
    ReconciliationRequired = 4,
}

/// <summary>Motivos de transição (histórico auditável — INV-ORDER-002).</summary>
public sealed record OrderTransition(OrderState From, OrderState To, string Reason, DateTimeOffset OccurredAtUtc);

/// <summary>
/// Agregado Order (INV-ORDER-001/002): snapshot IMUTÁVEL de itens/valores/endereço capturado
/// na colocação; máquina de estados explícita e idempotente — eventos duplicados/reordenados
/// NÃO avançam duas vezes; confirmação exige pagamento autorizado + reservas confirmadas.
/// </summary>
public sealed class Order
{
    private readonly List<OrderTransition> _transitions;

    private Order(OrderId id, string customerId, IReadOnlyList<OrderLineSnapshot> lines,
        ShippingAddressSnapshot address, Money total, DateTimeOffset placedAtUtc)
    {
        Id = id;
        CustomerId = customerId;
        Lines = lines;
        Address = address;
        Total = total;
        PlacedAtUtc = placedAtUtc;
        State = OrderState.PendingPayment;
        _transitions = [new(OrderState.PendingPayment, OrderState.PendingPayment, "placed", placedAtUtc)];
    }

    public OrderId Id { get; }
    public string CustomerId { get; }
    public IReadOnlyList<OrderLineSnapshot> Lines { get; }
    public ShippingAddressSnapshot Address { get; }
    public Money Total { get; }
    public DateTimeOffset PlacedAtUtc { get; }
    public OrderState State { get; private set; }
    public IReadOnlyList<OrderTransition> Transitions => _transitions;
    public string? PaymentReference { get; private set; }
    public IReadOnlyList<string> ReservationIds { get; private set; } = [];

    public static Order Place(
        string customerId,
        IReadOnlyList<OrderLineSnapshot> lines,
        ShippingAddressSnapshot address,
        DateTimeOffset placedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(customerId)) throw new ArgumentException("customer_id obrigatório");
        if (lines.Count == 0) throw new ArgumentException("Pedido sem linhas");
        if (lines.Any(l => l.Quantity <= 0)) throw new ArgumentException("Quantidade deve ser > 0");

        var currency = lines[0].UnitPriceMinor.Currency;
        if (lines.Any(l => l.UnitPriceMinor.Currency != currency))
            throw new InvalidOperationException("Pedido com moedas mistas");

        var expectedTotal = lines.Aggregate(Money.Create(0, currency), (acc, l) => acc.Add(l.LineTotal));
        var statedTotal = lines.Aggregate(Money.Create(0, currency), (acc, l) => acc.Add(
            Money.Create(l.UnitPriceMinor.AmountMinor * l.Quantity, currency)));
        if (expectedTotal.AmountMinor != statedTotal.AmountMinor)
            throw new InvalidOperationException("Total do snapshot não confere com a soma das linhas");

        return new(OrderId.New(), customerId, lines, address, expectedTotal, placedAtUtc);
    }

    /// <summary>Confirma (INV-ORDER-002): exige pagamento autorizado/capturado e reservas; idempotente.</summary>
    public bool TryConfirm(string paymentReference, IReadOnlyList<string> reservationIds, DateTimeOffset nowUtc)
    {
        if (State == OrderState.Confirmed && PaymentReference == paymentReference)
            return true; // idempotente: mesmo pagamento, mesmo resultado
        if (State != OrderState.PendingPayment) return false;
        if (string.IsNullOrWhiteSpace(paymentReference)) return false;
        if (reservationIds.Count == 0) return false;

        State = OrderState.Confirmed;
        PaymentReference = paymentReference;
        ReservationIds = reservationIds;
        _transitions.Add(new(OrderState.PendingPayment, OrderState.Confirmed, $"payment={paymentReference}", nowUtc));
        return true;
    }

    public bool TryMarkPaymentFailed(string reason, DateTimeOffset nowUtc)
    {
        if (State != OrderState.PendingPayment) return false;
        State = OrderState.PaymentFailed;
        _transitions.Add(new(OrderState.PendingPayment, OrderState.PaymentFailed, reason, nowUtc));
        return true;
    }

    public bool TryCancel(string reason, DateTimeOffset nowUtc)
    {
        if (State is not (OrderState.PendingPayment or OrderState.Confirmed)) return false;
        State = OrderState.Cancelled;
        _transitions.Add(new(State, OrderState.Cancelled, reason, nowUtc));
        return true;
    }

    /// <summary>PSP desconhecido (timeout): estado de reconciliação, nunca sucesso presumido (INV-PAYMENT-001).</summary>
    public bool TryMarkReconciliationRequired(string reason, DateTimeOffset nowUtc)
    {
        if (State != OrderState.PendingPayment) return false;
        State = OrderState.ReconciliationRequired;
        _transitions.Add(new(OrderState.PendingPayment, OrderState.ReconciliationRequired, reason, nowUtc));
        return true;
    }

    public bool TryResolveReconciliation(bool paid, string paymentReference, DateTimeOffset nowUtc)
    {
        if (State != OrderState.ReconciliationRequired) return false;
        if (paid && !string.IsNullOrWhiteSpace(paymentReference))
        {
            State = OrderState.Confirmed;
            PaymentReference = paymentReference;
        }
        else
        {
            State = OrderState.Cancelled;
        }
        _transitions.Add(new(OrderState.ReconciliationRequired, State, "reconciliation-resolved", nowUtc));
        return true;
    }
}

/// <summary>Eventos de domínio (envelope INV-EVT-001 aplicado na Application).</summary>
public sealed record OrderPlacedV1(string OrderId, string CustomerId, long TotalMinor, string Currency, long AggregateVersion, DateTimeOffset OccurredAtUtc);
public sealed record OrderConfirmedV1(string OrderId, string PaymentReference, long AggregateVersion, DateTimeOffset OccurredAtUtc);
public sealed record OrderCancelledV1(string OrderId, string Reason, long AggregateVersion, DateTimeOffset OccurredAtUtc);
