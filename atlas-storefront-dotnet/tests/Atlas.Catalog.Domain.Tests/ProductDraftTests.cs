using Atlas.Catalog.Domain;
using Xunit;

namespace Atlas.Catalog.Domain.Tests;

public class ProductDraftTests
{
    [Fact]
    public void Criar_produto_gera_id_valido_em_rascunho()
    {
        var product = ProductDraft.Create();
        Assert.True(product.Id.IsValid);
        Assert.Equal(ProductStatus.Draft, product.Status);
    }

    [Fact]
    public void Submeter_sem_titulo_é_rejeitado()
    {
        var product = ProductDraft.Create();
        Assert.Throws<InvalidOperationException>(product.SubmitForApproval);
    }

    [Fact]
    public void Submeter_com_titulo_localizado_avanca_para_pending_approval()
    {
        var product = ProductDraft.Create().WithTitle("pt-BR", "Tênis de corrida");
        product.SubmitForApproval();
        Assert.Equal(ProductStatus.PendingApproval, product.Status);
    }

    [Fact]
    public void Re_submeter_produto_já_submetido_é_rejeitado()
    {
        var product = ProductDraft.Create().WithTitle("en-US", "Running shoes");
        product.SubmitForApproval();
        Assert.Throws<InvalidOperationException>(product.SubmitForApproval);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    public void Locales_suportados_são_aceitos(string locale)
    {
        var product = ProductDraft.Create().WithTitle(locale, "ok");
        Assert.NotNull(product.TitleFor(new Locale(locale)));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("pt")]
    [InlineData("")]
    public void Locales_não_suportados_são_rejeitados(string locale)
    {
        Assert.Throws<ArgumentException>(() => ProductDraft.Create().WithTitle(locale, "x"));
    }

    [Fact]
    public void Título_vazio_é_rejeitado()
    {
        Assert.Throws<ArgumentException>(() => ProductDraft.Create().WithTitle("pt-BR", "  "));
    }
}
