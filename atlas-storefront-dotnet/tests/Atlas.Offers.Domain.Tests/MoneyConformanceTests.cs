using Xunit;
using OffersMoney = Atlas.Offers.Domain.Money;
using OrdersMoney = Atlas.Orders.Domain.Money;

namespace Atlas.Offers.Domain.Tests;

/// <summary>
/// Trava de conformidade INV-MONEY-001: as cópias de <c>Money</c> em Offers e Orders
/// devem honrar o mesmo contrato (minor units + moeda ISO 4217, sem float/double)
/// até a consolidação no pacote de contratos versionado. Qualquer divergência de
/// comportamento quebra aqui — não em produção.
/// NOTA: Orders.Money é intencionalmente um subconjunto (snapshot imutável, sem
/// Multiply/FromMajor — o pedido nunca recalcula valores); a conformidade cobre
/// a interseção (Create/Add).
/// </summary>
public sealed class MoneyConformanceTests
{
    [Theory]
    [InlineData(0, "BRL")]
    [InlineData(1999, "usd")]
    [InlineData(1_000_000_00, "EUR")]
    [InlineData(500, "JPY")]
    public void Criação_aceita_os_mesmos_valores_nas_duas_cópias(long minor, string currency)
    {
        var offers = OffersMoney.Create(minor, currency);
        var orders = OrdersMoney.Create(minor, currency);

        Assert.Equal(orders.AmountMinor, offers.AmountMinor);
        Assert.Equal(orders.Currency, offers.Currency);
        Assert.Equal(currency.ToUpperInvariant(), offers.Currency);
    }

    [Theory]
    [InlineData(-1, "BRL")]
    [InlineData(100, "BR")]
    [InlineData(100, "BRLX")]
    [InlineData(100, "")]
    public void Criação_rejeita_os_mesmos_inválidos_nas_duas_cópias(long minor, string currency)
    {
        Assert.ThrowsAny<ArgumentException>(() => OffersMoney.Create(minor, currency));
        Assert.ThrowsAny<ArgumentException>(() => OrdersMoney.Create(minor, currency));
    }

    [Fact]
    public void Criação_negativa_é_ArgumentOutOfRange_nas_duas_cópias()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OffersMoney.Create(-1, "BRL"));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrdersMoney.Create(-1, "BRL"));
    }

    [Fact]
    public void Soma_mesma_moeda_coincide_nas_duas_cópias()
    {
        var offers = OffersMoney.Create(999, "BRL").Add(OffersMoney.Create(1, "BRL"));
        var orders = OrdersMoney.Create(999, "BRL").Add(OrdersMoney.Create(1, "BRL"));

        Assert.Equal(orders.AmountMinor, offers.AmountMinor);
        Assert.Equal(orders.Currency, offers.Currency);
    }

    [Fact]
    public void Soma_moedas_diferentes_é_erro_nas_duas_cópias()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OffersMoney.Create(100, "BRL").Add(OffersMoney.Create(100, "USD")));
        Assert.Throws<InvalidOperationException>(() =>
            OrdersMoney.Create(100, "BRL").Add(OrdersMoney.Create(100, "USD")));
    }

    [Fact]
    public void Overflow_checked_nas_duas_cópias()
    {
        Assert.Throws<OverflowException>(() =>
            OffersMoney.Create(long.MaxValue, "BRL").Add(OffersMoney.Create(1, "BRL")));
        Assert.Throws<OverflowException>(() =>
            OrdersMoney.Create(long.MaxValue, "BRL").Add(OrdersMoney.Create(1, "BRL")));
    }
}
