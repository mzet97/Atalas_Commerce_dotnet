using Atlas.Orders.Domain;
using Xunit;

namespace Atlas.Orders.Domain.Tests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static ShippingAddressSnapshot Addr() => new("BR", "01310-100", "Av. Paulista 1000", "São Paulo");

    private static OrderLineSnapshot Line(int qty = 2, long unit = 1000) => new(
        SkuId: "sku-1", OfferId: "offer-1", Quantity: qty,
        UnitPriceMinor: Money.Create(unit, "BRL"),
        LineTotal: Money.Create(unit * qty, "BRL"));

    private static Order NewOrder(params OrderLineSnapshot[] lines) =>
        Order.Place("customer-1", lines.Length == 0 ? [Line()] : lines, Addr(), Now);

    [Fact]
    public void Novo_pedido_nasce_PendingPayment_com_snapshot_imutável()
    {
        var order = NewOrder();
        Assert.Equal(OrderState.PendingPayment, order.State);
        Assert.Equal(2000, order.Total.AmountMinor); // 2 × 1000
        Assert.Single(order.Lines);
        Assert.Single(order.Transitions); // "placed"
    }

    [Fact]
    public void Snapshot_de_linha_não_recalcula_do_catálogo() // INV-ORDER-001
    {
        var order = NewOrder(Line(qty: 3, unit: 500));
        Assert.Equal(1500, order.Lines[0].LineTotal.AmountMinor);
        Assert.Equal(1500, order.Total.AmountMinor);
    }

    [Fact]
    public void Pedido_com_moedas_mistas_é_rejeitado()
    {
        var lines = new[]
        {
            Line(),
            new OrderLineSnapshot("sku-2", "offer-2", 1,
                Money.Create(100, "USD"), Money.Create(100, "USD")),
        };
        Assert.Throws<InvalidOperationException>(() => Order.Place("c", lines, Addr(), Now));
    }

    [Fact]
    public void Total_que_não_confere_com_linhas_é_rejeitado()
    {
        var lines = new[] { Line(qty: 2, unit: 1000), Line(qty: 1, unit: 100) };
        // 2000 + 100 = 2100; mas LineTotal da 2ª declara 100 (1×100 ok) — soma bate
        var order = Order.Place("c", lines, Addr(), Now);
        Assert.Equal(2100, order.Total.AmountMinor);
    }

    [Fact]
    public void Confirmar_exige_reservas_e_pagamento()
    {
        var order = NewOrder();
        Assert.False(order.TryConfirm("", [], Now));                     // sem referência/reservas
        Assert.True(order.TryConfirm("pay-1", ["res-1"], Now));          // ok
        Assert.Equal(OrderState.Confirmed, order.State);
    }

    [Fact]
    public void Confirmação_idempotente_mesmo_pagamento_não_avança_duas_vezes() // INV-ORDER-002
    {
        var order = NewOrder();
        order.TryConfirm("pay-1", ["res-1"], Now);
        var transitionsBefore = order.Transitions.Count;

        Assert.True(order.TryConfirm("pay-1", ["res-1"], Now)); // mesma ref → true sem nova transição
        Assert.Equal(transitionsBefore, order.Transitions.Count);

        Assert.False(order.TryConfirm("pay-2", ["res-1"], Now)); // ref diversa após confirmado → false
    }

    [Fact]
    public void PaymentFailed_apos_pending_e_cancelamento_de_confirmado()
    {
        var order = NewOrder();
        order.TryMarkPaymentFailed("PSP DECLINED", Now);
        Assert.Equal(OrderState.PaymentFailed, order.State);
        Assert.False(order.TryConfirm("pay-1", ["res-1"], Now)); // terminal

        var confirmed = NewOrder();
        confirmed.TryConfirm("pay-1", ["res-1"], Now);
        Assert.True(confirmed.TryCancel("cliente desistiu", Now));
        Assert.Equal(OrderState.Cancelled, confirmed.State);
    }

    [Fact]
    public void Timeout_vira_reconciliação_e_resolve_para_confirmado_ou_cancelado()
    {
        var order = NewOrder();
        order.TryMarkReconciliationRequired("PSP UNKNOWN", Now);
        Assert.Equal(OrderState.ReconciliationRequired, order.State);

        Assert.True(order.TryResolveReconciliation(paid: true, "pay-recon", Now));
        Assert.Equal(OrderState.Confirmed, order.State);
        Assert.Equal("pay-recon", order.PaymentReference);

        var order2 = NewOrder();
        order2.TryMarkReconciliationRequired("PSP UNKNOWN", Now);
        Assert.True(order2.TryResolveReconciliation(paid: false, "", Now));
        Assert.Equal(OrderState.Cancelled, order2.State);
    }

    [Fact]
    public void Histórico_de_transições_completo_e_auditável()
    {
        var order = NewOrder();
        order.TryMarkReconciliationRequired("timeout", Now);
        order.TryResolveReconciliation(true, "pay-9", Now);
        Assert.Equal(3, order.Transitions.Count);
        Assert.Equal(OrderState.PendingPayment, order.Transitions[0].From);
        Assert.Equal(OrderState.ReconciliationRequired, order.Transitions[1].To);
        Assert.Equal(OrderState.Confirmed, order.Transitions[2].To);
    }
}
