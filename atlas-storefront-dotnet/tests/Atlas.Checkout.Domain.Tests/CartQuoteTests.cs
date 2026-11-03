using Atlas.Checkout.Domain;
using Xunit;

namespace Atlas.Checkout.Domain.Tests;

/// <summary>UC-002 + COM-08 + INV-QUOTE-001: Cart e PriceQuote.</summary>
public class CartTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Adiciona_e_soma_quantidade_do_mesmo_sku()
    {
        var cart = Cart.Create("customer-1", Now);
        cart.AddItem("sku-1", 1, Now);
        cart.AddItem("sku-1", 2, Now);
        Assert.Single(cart.Lines);
        Assert.Equal(3, cart.Lines[0].Quantity);
    }

    [Fact]
    public void Remove_sku_inexistente_é_rejeitado()
    {
        var cart = Cart.Create("customer-1", Now);
        Assert.Throws<InvalidOperationException>(() => cart.RemoveItem("sku-x", Now));
    }

    [Fact]
    public void Quantity_zero_ou_negativa_é_rejeitada()
    {
        var cart = Cart.Create("customer-1", Now);
        Assert.Throws<ArgumentOutOfRangeException>(() => cart.AddItem("sku-1", 0, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => cart.AddItem("sku-1", -1, Now));
    }

    [Fact]
    public void Merge_do_guest_no_cliente_soma_quantidades()
    {
        var guest = Cart.Create("guest-1", Now);
        guest.AddItem("sku-a", 1, Now);
        guest.AddItem("sku-b", 2, Now);
        var customer = Cart.Create("customer-1", Now);
        customer.AddItem("sku-a", 2, Now);

        Cart.Merge(guest, customer, Now);
        Assert.Equal(2, customer.Lines.Count);
        Assert.Equal(3, customer.Lines.First(l => l.SkuId == "sku-a").Quantity);
        Assert.Equal(2, customer.Lines.First(l => l.SkuId == "sku-b").Quantity);
    }

    [Fact]
    public void Clear_limpa_linhas()
    {
        var cart = Cart.Create("customer-1", Now);
        cart.AddItem("sku-1", 1, Now);
        cart.Clear(Now);
        Assert.Empty(cart.Lines);
    }
}

public class PriceQuoteTests
{
    private static PriceQuote NewQuote(long total = 5000, int ttlMinutes = 10) =>
        PriceQuote.Issue(
            "cart-1",
            [new PriceQuote.QuoteLine("sku-1", 2, 2500, 5000)],
            "BRL", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(ttlMinutes));

    [Fact]
    public void Cotação_nasce_vigente_com_ttl()
    {
        var quote = NewQuote();
        Assert.True(quote.IsValidAt(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Cotação_expirada_não_é_vigente() // COM-08 / INV-QUOTE-001
    {
        var quote = NewQuote(ttlMinutes: 10);
        Assert.False(quote.IsValidAt(DateTimeOffset.UtcNow.AddMinutes(11)));
    }

    [Fact]
    public void Diferença_na_re_cotação_é_calculada()
    {
        var old = NewQuote(total: 5000);
        var newQuote = PriceQuote.Issue(
            "cart-1", [new PriceQuote.QuoteLine("sku-1", 2, 2600, 5200)],
            "BRL", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        Assert.Equal(200, old.DifferenceMinor(newQuote)); // subiu 200 minor units
    }

    [Fact]
    public void Moedas_incompatíveis_na_re_cotação_é_erro()
    {
        var old = NewQuote();
        var newQuote = PriceQuote.Issue(
            "cart-1", [new PriceQuote.QuoteLine("sku-1", 2, 2600, 5200)],
            "USD", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        Assert.Throws<InvalidOperationException>(() => old.DifferenceMinor(newQuote));
    }

    [Fact]
    public void TTL_invalido_é_rejeitado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PriceQuote.Issue("cart-1", [new PriceQuote.QuoteLine("sku-1", 1, 100, 100)],
                "BRL", DateTimeOffset.UtcNow, TimeSpan.Zero));
    }

    [Fact]
    public void Cotação_sem_linhas_é_rejeitada()
    {
        Assert.Throws<ArgumentException>(() =>
            PriceQuote.Issue("cart-1", [], "BRL", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10)));
    }
}
