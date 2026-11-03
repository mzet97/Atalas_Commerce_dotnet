using Atlas.Offers.Domain;
using Xunit;

namespace Atlas.Offers.Domain.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(0, "BRL")]
    [InlineData(1999, "usd")]          // normaliza para maiúsculas
    [InlineData(1_000_000_00, "EUR")]
    public void Criação_aceita_valores_válidos(long minor, string currency)
    {
        var money = Money.Create(minor, currency);
        Assert.Equal(minor, money.AmountMinor);
        Assert.Equal(currency.ToUpperInvariant(), money.Currency);
    }

    [Theory]
    [InlineData(-1, "BRL")]
    [InlineData(100, "BR")]
    [InlineData(100, "BRLX")]
    [InlineData(100, "12L")]
    [InlineData(100, "")]
    public void Criação_rejeita_argumentos_inválidos(long minor, string currency) =>
        Assert.ThrowsAny<ArgumentException>(() => Money.Create(minor, currency));

    [Fact]
    public void Exponente_por_moede_JPY_0_e_demais_2()
    {
        Assert.Equal(0, Money.Create(500, "JPY").Exponent);
        Assert.Equal(2, Money.Create(100, "BRL").Exponent);
        Assert.Equal(2, Money.Create(100, "GBP").Exponent); // default
    }

    [Theory]
    [InlineData(10.005, "BRL", 1000)]       // banker s rounding: 1000.5 -> 1000 (par)
    [InlineData(10.015, "BRL", 1002)]       // 1001.5 → 1002 (par)
    [InlineData(3.14159, "USD", 314)]
    [InlineData(500, "JPY", 500)]
    public void FromMajor_arredonda_bankers(decimal major, string currency, long expectedMinor)
    {
        Assert.Equal(expectedMinor, Money.FromMajor(major, currency).AmountMinor);
    }

    [Fact]
    public void Soma_de_componentes_reproduz_o_total()
    {
        // INV-MONEY-001: soma das linhas com arredondamento por linha == total arredondado no final
        var fator = 1m / 3m;
        var preco = Money.Create(999, "BRL");
        var l1 = preco.Multiply(fator);
        var l2 = preco.Multiply(fator);
        var l3 = preco.Multiply(fator);
        Assert.Equal(preco.AmountMinor, l1.Add(l2).Add(l3).AmountMinor);
    }

    [Fact]
    public void Adicionar_moedas_diferentes_é_erro() =>
        Assert.Throws<InvalidOperationException>(() =>
            Money.Create(100, "BRL").Add(Money.Create(100, "USD")));
}

public class OfferTests
{
    private static readonly DateTimeOffset From = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Within = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static Offer NewOffer(long price = 1000, long list = 1500) =>
        Offer.Create("sku-1", "seller-1p", "BR", "web",
            Money.Create(price, "BRL"), Money.Create(list, "BRL"), From, To);

    [Fact]
    public void Escopo_e_chave_única_por_sku_seller_market_channel()
    {
        var offer = NewOffer();
        Assert.Equal("sku-1|seller-1p|BR|web", offer.ScopeKey());
    }

    [Fact]
    public void Ativar_dentro_da_janela_funciona_e_incrementa_versão()
    {
        var offer = NewOffer();
        offer.Activate(Within);
        Assert.Equal(OfferStatus.Active, offer.Status);
        Assert.Equal(1, offer.Version);
    }

    [Fact]
    public void Ativar_fora_da_janela_é_rejeitado()
    {
        var offer = NewOffer();
        Assert.Throws<InvalidOperationException>(() => offer.Activate(To));       // expirada
        var futura = Offer.Create("sku-1", "s", "BR", "web",
            Money.Create(100, "BRL"), Money.Create(100, "BRL"), From, To);
        Assert.Throws<InvalidOperationException>(() => futura.Activate(From.AddHours(-1)));
    }

    [Fact]
    public void Preço_acima_do_lista_é_rejeitado_na_criação() =>
        Assert.Throws<InvalidOperationException>(() => NewOffer(price: 2000, list: 1500));

    [Fact]
    public void Reprice_válido_dentro_dos_limites_e_da_janela()
    {
        var offer = NewOffer();
        offer.Activate(Within);
        offer.ChangePrice(Money.Create(900, "BRL"), Within);
        Assert.Equal(900, offer.Price.AmountMinor);
        Assert.Equal(2, offer.Version);
    }

    [Fact]
    public void Reprice_acima_do_lista_ou_fora_da_janela_é_rejeitado()
    {
        var offer = NewOffer();
        offer.Activate(Within);
        Assert.Throws<InvalidOperationException>(() => offer.ChangePrice(Money.Create(1600, "BRL"), Within));
        Assert.Throws<InvalidOperationException>(() => offer.ChangePrice(Money.Create(900, "BRL"), To));
    }

    [Fact]
    public void Desativar_permite_nova_oferta_no_mesmo_escopo_pela_persistência()
    {
        var offer = NewOffer();
        offer.Activate(Within);
        offer.Deactivate();
        Assert.Equal(OfferStatus.Deactivated, offer.Status);
        Assert.True(offer.IsExpiredAt(To)); // janela passada
    }
}
