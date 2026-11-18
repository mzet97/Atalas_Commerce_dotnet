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

    [Fact]
    public void Despublicado_pode_ser_resubmetido()
    {
        var p = ProductDraft.Create().WithTitle("pt-BR", "X").AddSku("SKU-1", new Dictionary<string, string>());
        p.SubmitForApproval();
        p.Publish("admin", DateTimeOffset.UtcNow);
        p.Unpublish("op", "pausa", DateTimeOffset.UtcNow);
        p.SubmitForApproval();
        Assert.Equal(ProductStatus.PendingApproval, p.Status);
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

    [Fact]
    public void Editar_titulos_do_rascunho_substitui_e_incrementa_versao()
    {
        var p = ProductDraft.Create().WithTitle("pt-BR", "Velho");
        var v0 = p.Version;
        p.UpdateTitles([("pt-BR", "Novo"), ("en-US", "New")]);
        Assert.Equal("Novo", p.TitleFor(new Locale("pt-BR"))!.Text);
        Assert.Equal("New", p.TitleFor(new Locale("en-US"))!.Text);
        Assert.True(p.Version > v0);
    }

    [Fact]
    public void Editar_publicado_é_rejeitado()
    {
        var p = ProductDraft.Create().WithTitle("pt-BR", "X");
        p.AddSku("SKU-1", new Dictionary<string, string>());
        p.SubmitForApproval();
        p.Publish("admin", DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => p.UpdateTitles([("pt-BR", "Y")]));
    }

    [Fact]
    public void Remover_só_rascunho()
    {
        ProductDraft.Create().EnsureDeletable();
        var pendente = ProductDraft.Create().WithTitle("pt-BR", "X");
        pendente.AddSku("SKU-1", new Dictionary<string, string>());
        pendente.SubmitForApproval();
        Assert.Throws<InvalidOperationException>(pendente.EnsureDeletable);
    }

    [Fact]
    public void Despublicar_publicado_emite_evento_e_muda_estado()
    {
        var p = ProductDraft.Create().WithTitle("pt-BR", "X");
        p.AddSku("SKU-1", new Dictionary<string, string>());
        p.SubmitForApproval();
        p.Publish("admin", DateTimeOffset.UtcNow);
        var evt = p.Unpublish("operador", "fora de linha", DateTimeOffset.UtcNow);
        Assert.Equal(ProductStatus.Unpublished, p.Status);
        Assert.Equal(p.Id, evt.ProductId);
        Assert.Equal("operador", evt.ActorSub);
        Assert.Equal(p.Version, evt.AggregateVersion);
    }

    [Fact]
    public void Despublicar_nao_publicado_é_rejeitado()
    {
        var p = ProductDraft.Create().WithTitle("pt-BR", "X");
        Assert.Throws<InvalidOperationException>(() => p.Unpublish("op", "motivo", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Despublicar_sem_motivo_é_rejeitado()
    {
        var p = ProductDraft.Create().WithTitle("pt-BR", "X");
        p.AddSku("SKU-1", new Dictionary<string, string>());
        p.SubmitForApproval();
        p.Publish("admin", DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => p.Unpublish("op", "", DateTimeOffset.UtcNow));
    }
}

public class ProductImageTests
{
    private static ProductDraft Rascunho() => ProductDraft.Create().WithTitle("pt-BR", "Mochila");

    private static string Chave(ProductDraft d, string arq = "a.jpg") => $"products/{d.Id}/{arq}";

    [Fact]
    public void Anexar_imagem_valida_registra_como_capa()
    {
        var d = Rascunho();
        var img = d.AttachImage(Chave(d), "image/jpeg", 1024, DateTimeOffset.UtcNow);
        Assert.Equal(Chave(d), img.Key);
        Assert.Single(d.Images);
        Assert.Same(img, d.Images[0]); // [0] = capa
    }

    [Fact]
    public void Anexar_tipo_invalido_rejeita()
    {
        var d = Rascunho();
        Assert.Throws<ArgumentException>(() => d.AttachImage(Chave(d, "a.gif"), "image/gif", 1024, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Anexar_acima_do_teto_rejeita()
    {
        var d = Rascunho();
        Assert.Throws<ArgumentException>(() => d.AttachImage(Chave(d), "image/png", ProductImage.MaxSizeBytes + 1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Anexar_chave_de_outro_produto_rejeita()
    {
        var d = Rascunho();
        Assert.Throws<ArgumentException>(() => d.AttachImage("products/outro-id/a.jpg", "image/jpeg", 10, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Anexar_duplicada_rejeita()
    {
        var d = Rascunho();
        d.AttachImage(Chave(d), "image/jpeg", 10, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => d.AttachImage(Chave(d), "image/jpeg", 10, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Desanexar_remove_e_reordena_capa()
    {
        var d = Rascunho();
        d.AttachImage(Chave(d, "a.jpg"), "image/jpeg", 10, DateTimeOffset.UtcNow);
        d.AttachImage(Chave(d, "b.png"), "image/png", 20, DateTimeOffset.UtcNow);
        d.DetachImage(Chave(d, "a.jpg"));
        Assert.Single(d.Images);
        Assert.EndsWith("b.png", d.Images[0].Key); // nova capa
    }

    [Fact]
    public void Desanexar_inexistente_rejeita()
    {
        var d = Rascunho();
        Assert.Throws<InvalidOperationException>(() => d.DetachImage(Chave(d, "x.jpg")));
    }
}
