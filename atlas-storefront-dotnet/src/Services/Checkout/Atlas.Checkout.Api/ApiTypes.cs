namespace Atlas.Checkout.Api;

using Atlas.Checkout.Application;
using Atlas.Checkout.Domain;
using Atlas.Orders.Domain;
using Atlas.Payments.Domain;
using MongoCheckoutStore = Atlas.Checkout.Infrastructure.MongoCheckoutStore;
using MongoOrdersStore = Atlas.Orders.Infrastructure.MongoOrdersStore;
using MongoPaymentsStore = Atlas.Payments.Infrastructure.MongoPaymentsStore;
using MongoSagaStore = Atlas.Checkout.Infrastructure.MongoSagaStore;

public sealed record SubmitCheckoutRequest(
    string IdempotencyKey,
    string CustomerId,
    Guid QuoteId,
    List<CheckoutLineDto> Lines,
    long TotalMinor,
    string Currency,
    OrderShippingAddressDto Shipping);

public sealed record CheckoutQuoteRequest(
    string CartId,
    string Currency,
    int TtlSeconds,
    List<QuoteLineRequest> Lines);

public sealed record QuoteLineRequest(string SkuId, int Quantity, long UnitPriceMinor);

public sealed class SagaStoreAdapter(MongoSagaStore inner) : ISagaStore
{
    public Task SaveAsync(CheckoutSaga saga, CancellationToken ct) => inner.SaveAsync(saga, ct);
    public Task<CheckoutSaga?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => inner.FindByIdempotencyKeyAsync(idempotencyKey, ct);
    public Task<IReadOnlyList<CheckoutSaga>> FindByStateAsync(CheckoutSagaState state, CancellationToken ct) => inner.FindByStateAsync(state, ct);
}

public sealed class OrdersStoreAdapter(MongoOrdersStore inner) : IOrdersStore
{
    public Task SaveAsync(Order order, CancellationToken ct) => inner.InsertAsync(order, ct);
    public Task<bool> MarkReconciliationRequiredAsync(Guid orderId, string reason, CancellationToken ct) =>
        inner.ApplyTransitionAsync(orderId, OrderState.PendingPayment, OrderState.ReconciliationRequired,
            null, "Order.ReconciliationRequiredV1", reason, Guid.NewGuid().ToString(), ct);
    public async Task<bool> ConfirmOrderAsync(Guid orderId, string paymentReference, CancellationToken ct) =>
        await inner.ApplyTransitionAsync(orderId, OrderState.PendingPayment, OrderState.Confirmed,
            paymentReference, "Order.ConfirmedV1", "reconciled", Guid.NewGuid().ToString(), ct)
        || await inner.ApplyTransitionAsync(orderId, OrderState.ReconciliationRequired, OrderState.Confirmed,
            paymentReference, "Order.ConfirmedV1", "reconciled", Guid.NewGuid().ToString(), ct);
    public Task<bool> MarkPaymentFailedAsync(Guid orderId, string reason, CancellationToken ct) =>
        inner.ApplyTransitionAsync(orderId, OrderState.PendingPayment, OrderState.PaymentFailed,
            null, "Order.PaymentFailedV1", reason, Guid.NewGuid().ToString(), ct);
}

public sealed class PaymentsStoreAdapter(MongoPaymentsStore inner) : IPaymentsStore
{
    public Task SaveAsync(PaymentIntent intent, CancellationToken ct) => inner.SaveAsync(intent, ct);
    public Task<PaymentIntent?> FindByPspKeyAsync(string pspIdempotencyKey, CancellationToken ct) => inner.FindByPspKeyAsync(pspIdempotencyKey, ct);
}

public sealed class QuoteStoreAdapter(MongoCheckoutStore inner) : IQuoteStore
{
    public Task<PriceQuote?> FindByIdAsync(Guid quoteId, CancellationToken ct) => inner.FindByIdAsync(quoteId, ct);
}

public sealed class OrdersReconciliationAdapter(MongoOrdersStore inner) : IOrderReconciliationStore
{
    public async Task<bool> ConfirmOrderAsync(Guid orderId, string paymentReference, CancellationToken ct) =>
        await inner.ApplyTransitionAsync(orderId, OrderState.PendingPayment, OrderState.Confirmed,
            paymentReference, "Order.ConfirmedV1", "reconciled", Guid.NewGuid().ToString(), ct)
        || await inner.ApplyTransitionAsync(orderId, OrderState.ReconciliationRequired, OrderState.Confirmed,
            paymentReference, "Order.ConfirmedV1", "reconciled", Guid.NewGuid().ToString(), ct);

    public async Task<bool> CancelOrderAsync(Guid orderId, string reason, CancellationToken ct) =>
        await inner.ApplyTransitionAsync(orderId, OrderState.PendingPayment, OrderState.Cancelled,
            null, "Order.CancelledV1", reason, Guid.NewGuid().ToString(), ct)
        || await inner.ApplyTransitionAsync(orderId, OrderState.ReconciliationRequired, OrderState.Cancelled,
            null, "Order.CancelledV1", reason, Guid.NewGuid().ToString(), ct);

    public Task<bool> MarkPaymentFailedAsync(Guid orderId, string reason, CancellationToken ct) =>
        inner.ApplyTransitionAsync(orderId, OrderState.PendingPayment, OrderState.PaymentFailed,
            null, "Order.PaymentFailedV1", reason, Guid.NewGuid().ToString(), ct);
}

public sealed class PaymentsReconciliationAdapter(MongoPaymentsStore inner) : IPaymentReconciliationStore
{
    public Task<bool> AuthorizeIntentAsync(string pspIdempotencyKey, string providerReference, CancellationToken ct) =>
        inner.ApplyTransitionAsync(pspIdempotencyKey, PaymentIntentState.Unknown, PaymentIntentState.Authorized,
            providerReference, "Payment.AuthorizedV1", "reconciled", ct);

    public async Task<bool> CaptureIntentAsync(string pspIdempotencyKey, CancellationToken ct) =>
        await inner.ApplyTransitionAsync(pspIdempotencyKey, PaymentIntentState.Authorized, PaymentIntentState.Captured,
            null, "Payment.CapturedV1", "reconciled", ct)
        || true; // já Captured (idempotente)

    public Task<bool> VoidIntentAsync(string pspIdempotencyKey, CancellationToken ct) =>
        inner.ApplyTransitionAsync(pspIdempotencyKey, PaymentIntentState.Unknown, PaymentIntentState.AuthorizationVoided,
            null, "Payment.VoidedV1", "reconciled", ct);
}

public sealed class ReconciliationHostedService(
    ISagaStore sagaStore,
    IOrderReconciliationStore ordersReconciliation,
    IPaymentReconciliationStore paymentsReconciliation,
    IInventoryReservePort inventoryPort,
    IPspReconciliationPort pspReconciliation,
    Atlas.Checkout.Application.IWorkerLease lease,
    Microsoft.Extensions.Configuration.IConfiguration config) : Microsoft.Extensions.Hosting.BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconciler = new CheckoutReconciler(
            sagaStore, ordersReconciliation, paymentsReconciliation, inventoryPort, pspReconciliation);
        var stuckAfter = TimeSpan.FromSeconds(
            config.GetValue("Recovery:StuckAfterSeconds", 60));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // SPEC-004+: lease distribuída — multi-instância não duplica reconciliação
                if (!await lease.TryAcquireAsync("checkout-reconciler",
                        Environment.MachineName + "-" + Environment.ProcessId,
                        TimeSpan.FromSeconds(stuckAfter.TotalSeconds), stoppingToken))
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                var pendingCount = (await sagaStore.FindByStateAsync(CheckoutSagaState.PendingReconciliation, stoppingToken)).Count;
                if (pendingCount > 0)
                    Console.WriteLine($"[reconciler] {pendingCount} saga(s) pendentes, consultando PSP...");
                var resolved = await reconciler.ReconcileBatchAsync(stoppingToken);
                if (resolved > 0)
                    Console.WriteLine($"[reconciler] {resolved} saga(s) resolvidas");

                // recuperação de sagas crashadas (TEST-003-008): estados intermediários
                // acima do SLA do fluxo normal são resolvidos (confirmados ou compensados)
                var recovered = await reconciler.RecoverStuckAsync(stuckAfter, DateTimeOffset.UtcNow, stoppingToken);
                if (recovered > 0)
                    Console.WriteLine($"[reconciler] {recovered} saga(s) crashada(s) recuperada(s)");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"[reconciler] ciclo com falha (retoma): {ex.Message}");
            }
            await Task.Delay(5000, stoppingToken);
        }
    }
}
