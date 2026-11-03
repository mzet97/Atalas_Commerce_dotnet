namespace Atlas.Checkout.Application;

using Atlas.Checkout.Domain;
using Atlas.Orders.Domain;
using Atlas.Payments.Domain;

/// <summary>Resultado da consulta de reconciliação ao PSP: pago, recusado ou ainda indeterminado.</summary>
public enum ReconciliationOutcome { Paid, NotAuthorized, Indeterminate }

/// <summary>Port de reconciliação — consulta o resultado REAL da autorização no PSP (INV-PAYMENT-001).</summary>
public interface IPspReconciliationPort
{
    Task<(ReconciliationOutcome Outcome, string? ProviderReference)> QueryAuthorizationAsync(
        string idempotencyKey, CancellationToken ct);
}

/// <summary>Ports para resolver a reconciliação nos owners (transições com guard de estado).</summary>
public interface IOrderReconciliationStore
{
    Task<bool> ConfirmOrderAsync(Guid orderId, string paymentReference, CancellationToken ct);
    Task<bool> CancelOrderAsync(Guid orderId, string reason, CancellationToken ct);
}

public interface IPaymentReconciliationStore
{
    Task<bool> AuthorizeIntentAsync(string pspIdempotencyKey, string providerReference, CancellationToken ct);
    Task<bool> CaptureIntentAsync(string pspIdempotencyKey, CancellationToken ct);
    Task<bool> VoidIntentAsync(string pspIdempotencyKey, CancellationToken ct);
}

/// <summary>
/// Reconciliador (ADR-0006, UC-003-T03): varre sagas PendingReconciliation, consulta o PSP
/// e resolve — pago → confirma pedido/intent; não autorizado → libera reserva e cancela;
/// indeterminado → mantém pendente para o próximo ciclo (NUNCA presume).
/// </summary>
public sealed class CheckoutReconciler(
    ISagaStore sagaStore,
    IOrderReconciliationStore ordersReconciliation,
    IPaymentReconciliationStore paymentsReconciliation,
    IInventoryReservePort inventoryPort,
    IPspReconciliationPort pspReconciliation)
{
    /// <summary>Um ciclo de reconciliação. Retorna nº de sagas resolvidas.</summary>
    public async Task<int> ReconcileBatchAsync(CancellationToken ct)
    {
        var pending = await sagaStore.FindByStateAsync(CheckoutSagaState.PendingReconciliation, ct);
        var resolved = 0;

        foreach (var saga in pending)
        {
            // A autorização no PSP foi feita sob o ORDER ID (o orchestrator passa orderReference
            // como idempotency_key do PSP) — a consulta de reconciliação usa a MESMA chave.
            var (outcome, providerRef) = await pspReconciliation.QueryAuthorizationAsync(
                saga.OrderId ?? saga.IdempotencyKey, ct);

            switch (outcome)
            {
                case ReconciliationOutcome.Paid when saga.OrderId is not null:
                    // pago: confirma intent + captura + order + saga (UC003-T03; política de captura SPEC-006)
                    await paymentsReconciliation.AuthorizeIntentAsync(saga.IdempotencyKey, providerRef!, ct);
                    await paymentsReconciliation.CaptureIntentAsync(saga.IdempotencyKey, ct);
                    if (saga.OrderId is not null && Guid.TryParse(saga.OrderId, out var orderId))
                        await ordersReconciliation.ConfirmOrderAsync(orderId, providerRef!, ct);
                    saga.MarkReconciliationPaid(saga.PaymentIntentId ?? "pi-reconciled", providerRef ?? "", DateTimeOffset.UtcNow);
                    await sagaStore.SaveAsync(saga, ct);
                    resolved++;
                    break;

                case ReconciliationOutcome.NotAuthorized when saga.ReservationId is not null:
                    // não pago: libera reserva, cancela order, compensa
                    await inventoryPort.ReleaseAsync(saga.ReservationId, "reconciled-not-paid", ct);
                    if (saga.OrderId is not null && Guid.TryParse(saga.OrderId, out var ordId))
                        await ordersReconciliation.CancelOrderAsync(ordId, "reconciled-not-paid", ct);
                    await paymentsReconciliation.VoidIntentAsync(saga.IdempotencyKey, ct);
                    saga.MarkReconciliationNotPaid("reconciled-not-paid", DateTimeOffset.UtcNow);
                    await sagaStore.SaveAsync(saga, ct);
                    resolved++;
                    break;

                case ReconciliationOutcome.Indeterminate:
                default:
                    break; // mantém pendente para o próximo ciclo
            }
        }
        return resolved;
    }

    /// <summary>
    /// Recuperação de sagas CRASHADAS (TEST-003-008 — restart/retry): varre estados
    /// intermediários (OrderPlaced/StockReserved/Submitted) mais velhos que o SLA do
    /// fluxo normal e decide SEM executar etapa duas vezes — nenhuma etapa é re-disparada:
    /// OrderPlaced consulta o PSP (pago → confirma; sem registro → compensa; PSP down →
    /// próxima rodada); Submitted/StockReserved compensam (nada chegou ao PSP).
    /// </summary>
    public async Task<int> RecoverStuckAsync(TimeSpan olderThan, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var cutoff = nowUtc - olderThan;
        var recovered = 0;

        foreach (var state in new[] { CheckoutSagaState.OrderPlaced, CheckoutSagaState.StockReserved, CheckoutSagaState.Submitted })
        {
            foreach (var saga in await sagaStore.FindByStateAsync(state, ct))
            {
                if (saga.CreatedAtUtc > cutoff) continue; // fluxo normal ainda pode estar em curso

                if (state == CheckoutSagaState.OrderPlaced && saga.OrderId is not null)
                {
                    var (outcome, providerRef) = await pspReconciliation.QueryAuthorizationAsync(saga.OrderId, ct);
                    if (outcome == ReconciliationOutcome.Indeterminate)
                        continue; // PSP indisponível: decide na próxima rodada (nunca presume)

                    if (outcome == ReconciliationOutcome.Paid)
                    {
                        // crash DEPOIS de autorizar e ANTES de persistir — o PSP confirma
                        await paymentsReconciliation.AuthorizeIntentAsync(saga.IdempotencyKey, providerRef!, ct);
                        if (Guid.TryParse(saga.OrderId, out var orderId))
                            await ordersReconciliation.ConfirmOrderAsync(orderId, providerRef!, ct);
                        saga.MarkReconciliationPaid(saga.PaymentIntentId ?? "pi-recovered", providerRef ?? "", nowUtc);
                        await sagaStore.SaveAsync(saga, ct);
                        recovered++;
                        continue;
                    }
                    // NotAuthorized: PSP não tem registro — autorização nunca ocorreu → compensa
                }

                if (await CompensateStuckAsync(saga, "recovered-stuck", ct)) recovered++;
            }
        }
        return recovered;
    }

    /// <summary>Compensação de saga parada: libera reserva, cancela order, marca Compensated (idempotente por guard).</summary>
    private async Task<bool> CompensateStuckAsync(CheckoutSaga saga, string reason, CancellationToken ct)
    {
        if (saga.ReservationId is not null)
            await inventoryPort.ReleaseAsync(saga.ReservationId, reason, ct);
        if (saga.OrderId is not null && Guid.TryParse(saga.OrderId, out var orderId))
            await ordersReconciliation.CancelOrderAsync(orderId, reason, ct);
        if (!saga.BeginCompensation(reason, DateTimeOffset.UtcNow)) return false;
        saga.MarkCompensated(DateTimeOffset.UtcNow);
        await sagaStore.SaveAsync(saga, ct);
        return true;
    }
}
