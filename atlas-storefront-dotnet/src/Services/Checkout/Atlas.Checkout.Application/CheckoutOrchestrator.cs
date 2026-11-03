namespace Atlas.Checkout.Application;

using Atlas.Checkout.Domain;
using Atlas.Orders.Domain;
using Atlas.Payments.Domain;

/// <summary>Ports da saga — cada elo é um owner/port substituível nos testes (DIP; ADR-0006).</summary>
public interface IInventoryReservePort
{
    /// <summary>Reserva autoritativa (CAS no owner). Retorna reservation_id ou null se sem saldo.</summary>
    Task<string?> ReserveAsync(string checkoutId, IReadOnlyList<(string SkuId, int Quantity)> lines, CancellationToken ct);
    Task<bool> ReleaseAsync(string reservationId, string reason, CancellationToken ct);
}

public interface IPspPort
{
    /// <summary>Autoriza no PSP. Outcome: Authorized | Declined | Unknown (timeout).</summary>
    Task<(PspOutcome Outcome, string? ProviderReference, string? Reason)> AuthorizeAsync(
        string orderReference, long amountMinor, string currency, CancellationToken ct);
}

public enum PspOutcome { Authorized, Declined, Unknown }

public interface IOrdersStore
{
    Task SaveAsync(Order order, CancellationToken ct);
    Task<bool> MarkReconciliationRequiredAsync(Guid orderId, string reason, CancellationToken ct);
    Task<bool> ConfirmOrderAsync(Guid orderId, string paymentReference, CancellationToken ct);
    /// <summary>Recusa definitiva do PSP: order vai a PaymentFailed (TEST-003-007 — sem order órfã).</summary>
    Task<bool> MarkPaymentFailedAsync(Guid orderId, string reason, CancellationToken ct);
}

public interface IPaymentsStore
{
    Task SaveAsync(PaymentIntent intent, CancellationToken ct);
    Task<PaymentIntent?> FindByPspKeyAsync(string pspIdempotencyKey, CancellationToken ct);
}

/// <summary>Port de cotação: o checkout valida quote vigente ANTES de reservar (COM-08/INV-QUOTE-001).</summary>
public interface IQuoteStore
{
    Task<PriceQuote?> FindByIdAsync(Guid quoteId, CancellationToken ct);
}

public interface ISagaStore
{
    Task SaveAsync(CheckoutSaga saga, CancellationToken ct);
    Task<CheckoutSaga?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
    Task<IReadOnlyList<CheckoutSaga>> FindByStateAsync(CheckoutSagaState state, CancellationToken ct);
}

public sealed record CheckoutLineDto(string SkuId, int Quantity, long UnitPriceMinor);

public sealed record OrderShippingAddressDto(string Country, string PostalCode, string Line1, string City);

public sealed record SubmitCheckoutCommand(
    string IdempotencyKey,
    string CustomerId,
    Guid QuoteId,
    IReadOnlyList<CheckoutLineDto> Lines,
    long TotalMinor,
    string Currency,
    OrderShippingAddressDto Shipping);

public enum CheckoutResultStatus { Confirmed, Pending, Rejected }

public sealed record CheckoutResult(
    CheckoutResultStatus Status, string CheckoutId, string? OrderId, string? Reason);

/// <summary>
/// Orquestrador da saga (ADR-0006): reserva autoritativa → Order PENDING_PAYMENT →
/// PaymentIntent (idempotente no PSP) → resultado INEQUÍVOCO:
/// Confirmed | Pending (UNKNOWN, reconciliação) | Rejected (sem saldo/recusado).
/// Timeout NUNCA é sucesso presumido (INV-PAYMENT-001); mesmo idempotency key → mesmo resultado (UC003-T02).
/// </summary>
public sealed class CheckoutOrchestrator(
    ISagaStore sagaStore,
    IOrdersStore ordersStore,
    IPaymentsStore paymentsStore,
    IQuoteStore quoteStore,
    IInventoryReservePort inventoryPort,
    IPspPort pspPort)
{
    public async Task<CheckoutResult> SubmitAsync(SubmitCheckoutCommand command, CancellationToken ct)
    {
        // UC003-T02: mesma idempotency key retorna o MESMO resultado
        var existing = await sagaStore.FindByIdempotencyKeyAsync(command.IdempotencyKey, ct);
        if (existing is not null)
            return MapExisting(existing);

        // INV-QUOTE-001/COM-08: quote vigente obrigatória — o checkout NUNCA confirma valores
        // que não conferem com a cotação congelada
        var quote = await quoteStore.FindByIdAsync(command.QuoteId, ct);
        if (quote is null)
            return new CheckoutResult(CheckoutResultStatus.Rejected, "no-saga", null, "quote não encontrada: re-cotação obrigatória");
        if (!quote.IsValidAt(DateTimeOffset.UtcNow))
            return new CheckoutResult(CheckoutResultStatus.Rejected, "no-saga", null, "quote expirada: re-cotação obrigatória");
        if (quote.TotalMinor != command.TotalMinor)
            return new CheckoutResult(CheckoutResultStatus.Rejected, "no-saga", null, "total diverge da cotação: re-cotação obrigatória");

        var saga = CheckoutSaga.Start(
            command.IdempotencyKey, command.CustomerId, command.TotalMinor, command.Currency, DateTimeOffset.UtcNow);
        await sagaStore.SaveAsync(saga, ct);

        // 1) reserva autoritativa no Inventory (CAS no owner)
        var reservationId = await inventoryPort.ReserveAsync(
            saga.Id.Value.ToString(),
            [.. command.Lines.Select(l => (l.SkuId, l.Quantity))], ct);
        if (reservationId is null)
        {
            saga.BeginCompensation("out_of_stock", DateTimeOffset.UtcNow);
            saga.MarkCompensated(DateTimeOffset.UtcNow);
            await sagaStore.SaveAsync(saga, ct);
            return new CheckoutResult(CheckoutResultStatus.Rejected, saga.Id.Value.ToString(), null, "OUT_OF_STOCK");
        }
        saga.MarkStockReserved(reservationId, DateTimeOffset.UtcNow);
        await sagaStore.SaveAsync(saga, ct);

        // 2) Order PENDING_PAYMENT com snapshot IMUTÁVEL (INV-ORDER-001)
        var orderLines = command.Lines.Select(l => new OrderLineSnapshot(
            l.SkuId, $"offer-{l.SkuId}", l.Quantity,
            Money.Create(l.UnitPriceMinor, command.Currency),
            Money.Create(l.UnitPriceMinor * l.Quantity, command.Currency))).ToArray();
        var order = Order.Place(command.CustomerId, orderLines,
            new ShippingAddressSnapshot(
                command.Shipping.Country, command.Shipping.PostalCode,
                command.Shipping.Line1, command.Shipping.City),
            DateTimeOffset.UtcNow);
        await ordersStore.SaveAsync(order, ct);
        saga.MarkOrderPlaced(order.Id.Value.ToString(), DateTimeOffset.UtcNow);
        await sagaStore.SaveAsync(saga, ct);

        // 3) PaymentIntent idempotente no PSP
        var intent = await paymentsStore.FindByPspKeyAsync(command.IdempotencyKey, ct)
                     ?? PaymentIntent.Create(order.Id.Value.ToString(), command.TotalMinor, command.Currency, command.IdempotencyKey);
        await paymentsStore.SaveAsync(intent, ct);

        // 4) autorização no PSP (cenário determinístico)
        var (outcome, providerRef, reason) = await pspPort.AuthorizeAsync(
            order.Id.Value.ToString(), command.TotalMinor, command.Currency, ct);

        switch (outcome)
        {
            case PspOutcome.Authorized:
                intent.TryAuthorize(providerRef!, webhookId: null, DateTimeOffset.UtcNow);
                intent.TryCapture(DateTimeOffset.UtcNow);
                await paymentsStore.SaveAsync(intent, ct);
                saga.MarkPaymentAuthorized(intent.Id.Value.ToString(), DateTimeOffset.UtcNow);
                var confirmed = order.TryConfirm(intent.Id.Value.ToString(), [reservationId], DateTimeOffset.UtcNow);
                await ordersStore.SaveAsync(order, ct);
                saga.MarkConfirmed(DateTimeOffset.UtcNow);
                await sagaStore.SaveAsync(saga, ct);
                return confirmed
                    ? new CheckoutResult(CheckoutResultStatus.Confirmed, saga.Id.Value.ToString(), order.Id.Value.ToString(), null)
                    : new CheckoutResult(CheckoutResultStatus.Rejected, saga.Id.Value.ToString(), order.Id.Value.ToString(), "confirmação inconsistente");

            case PspOutcome.Declined:
                intent.MarkDeclined(reason ?? "recusado", DateTimeOffset.UtcNow);
                await paymentsStore.SaveAsync(intent, ct);
                await ordersStore.MarkPaymentFailedAsync(order.Id.Value, reason ?? "declined", ct); // TEST-003-007
                saga.MarkPaymentDeclined(reason ?? "declined", DateTimeOffset.UtcNow);
                saga.BeginCompensation("payment-declined", DateTimeOffset.UtcNow);
                await inventoryPort.ReleaseAsync(reservationId, "payment-declined", ct);
                saga.MarkCompensated(DateTimeOffset.UtcNow);
                await sagaStore.SaveAsync(saga, ct);
                return new CheckoutResult(CheckoutResultStatus.Rejected, saga.Id.Value.ToString(), order.Id.Value.ToString(), reason);

            case PspOutcome.Unknown:
            default:
                intent.MarkUnknown(reason ?? "timeout", DateTimeOffset.UtcNow);
                await paymentsStore.SaveAsync(intent, ct);
                order.TryMarkReconciliationRequired("psp-unknown", DateTimeOffset.UtcNow);
                await ordersStore.SaveAsync(order, ct);
                await ordersStore.MarkReconciliationRequiredAsync(order.Id.Value, "psp-unknown", ct);
                saga.MarkPendingReconciliation("psp-unknown", DateTimeOffset.UtcNow);
                await sagaStore.SaveAsync(saga, ct);
                // compensação NÃO automática em UNKNOWN: reconciliador decide (pago → confirma; não pago → compensa)
                return new CheckoutResult(CheckoutResultStatus.Pending, saga.Id.Value.ToString(), order.Id.Value.ToString(), "PSP indisponível: reconciliação");
        }
    }

    private static CheckoutResult MapExisting(CheckoutSaga saga) => saga.State switch
    {
        CheckoutSagaState.Confirmed => new CheckoutResult(CheckoutResultStatus.Confirmed, saga.Id.Value.ToString(), saga.OrderId, null),
        CheckoutSagaState.PendingReconciliation or CheckoutSagaState.Compensating =>
            new CheckoutResult(CheckoutResultStatus.Pending, saga.Id.Value.ToString(), saga.OrderId, saga.FailureReason),
        CheckoutSagaState.Compensated or CheckoutSagaState.PaymentDeclined =>
            new CheckoutResult(CheckoutResultStatus.Rejected, saga.Id.Value.ToString(), saga.OrderId, saga.FailureReason),
        _ => new CheckoutResult(CheckoutResultStatus.Pending, saga.Id.Value.ToString(), saga.OrderId, "em processamento"),
    };
}
