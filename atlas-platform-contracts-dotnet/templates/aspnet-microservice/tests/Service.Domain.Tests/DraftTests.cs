using __SERVICE__.Domain;
using Xunit;

namespace __SERVICE__.Domain.Tests;

public class DraftTests
{
    [Fact]
    public void Criar_gera_id_valido()
    {
        var draft = Draft.Create();
        Assert.True(draft.Id.IsValid);
    }

    [Fact]
    public void Resumo_vazio_é_rejeitado()
    {
        var draft = Draft.Create();
        Assert.Throws<ArgumentException>(() => draft.SetSummary(" "));
    }
}
