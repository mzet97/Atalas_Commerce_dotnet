using Atlas.Checkout.Domain;
using Xunit;

namespace Atlas.Checkout.Domain.Tests;

public class CheckoutSagaTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CheckoutSaga NewSaga() => CheckoutSaga.Start("chk-key-1", "customer-1", 5000, "BRL", Now);

    [Fact]
    public void Fluxo_feliz_completo_na_ordem()
    {
        var saga = NewSaga();
        Assert.True(saga.MarkStockReserved("res-1", Now));
        Assert.True(saga.MarkOrderPlaced("order-1", Now));
        Assert.True(saga.MarkPaymentAuthorized("pi-1", Now));
        Assert.True(saga.MarkConfirmed(Now));
        Assert.Equal(CheckoutSagaState.Confirmed, saga.State);
        Assert.Equal(4, saga.StepsApplied.Count);
    }

    [Fact]
    public void Passos_fora_de_ordem_são_rejeitados()
    {
        var saga = NewSaga();
        Assert.False(saga.MarkOrderPlaced("order-x", Now));   // sem reserva
        Assert.False(saga.MarkConfirmed(Now));                // sem pagamento
        Assert.Equal(CheckoutSagaState.Submitted, saga.State);
    }

    [Fact]
    public void Reexecução_do_mesmo_passo_é_idempotente()
    {
        var saga = NewSaga();
        Assert.True(saga.MarkStockReserved("res-1", Now));
        var count = saga.StepsApplied.Count;
        Assert.True(saga.MarkStockReserved("res-1", Now));    // mesma etapa → true, sem duplicar
        Assert.Equal(count, saga.StepsApplied.Count);
    }

    [Fact]
    public void Declined_dispara_compensação_ate_compensado()
    {
        var saga = NewSaga();
        saga.MarkStockReserved("res-1", Now);
        saga.MarkOrderPlaced("order-1", Now);
        saga.MarkPaymentDeclined("PSP DECLINED", Now);
        Assert.Equal(CheckoutSagaState.PaymentDeclined, saga.State);

        Assert.True(saga.BeginCompensation("liberar reserva + falhar pedido", Now));
        Assert.True(saga.MarkCompensated(Now));
        Assert.Equal(CheckoutSagaState.Compensated, saga.State);
    }

    [Fact]
    public void Unknown_vira_pending_reconciliation_e_compensa_se_necessário() // UC003-T03
    {
        var saga = NewSaga();
        saga.MarkStockReserved("res-1", Now);
        saga.MarkOrderPlaced("order-1", Now);
        saga.MarkPendingReconciliation("PSP timeout", Now);

        Assert.True(saga.BeginCompensation("timeout — reconciliador assumirá", Now));
        Assert.True(saga.MarkCompensated(Now));
        Assert.Equal(CheckoutSagaState.Compensated, saga.State);
    }

    [Fact]
    public void Confirmed_não_compensa() // confirmação é terminal bem-sucedido
    {
        var saga = NewSaga();
        saga.MarkStockReserved("res-1", Now);
        saga.MarkOrderPlaced("order-1", Now);
        saga.MarkPaymentAuthorized("pi-1", Now);
        saga.MarkConfirmed(Now);
        Assert.False(saga.BeginCompensation("tentativa inválida", Now));
        Assert.Equal(CheckoutSagaState.Confirmed, saga.State);
    }

    [Fact]
    public void Idempotency_key_e_valor_são_obrigatórios()
    {
        Assert.Throws<ArgumentException>(() => CheckoutSaga.Start("", "c", 100, "BRL", Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => CheckoutSaga.Start("k", "c", 0, "BRL", Now));
    }
}
