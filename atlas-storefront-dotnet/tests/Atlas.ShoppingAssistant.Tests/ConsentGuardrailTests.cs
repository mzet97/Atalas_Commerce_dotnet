using Atlas.ShoppingAssistant.Domain;
using Xunit;

namespace Atlas.ShoppingAssistant.Tests;

/// <summary>
/// INV-AI-001 (gate M7): consent artifact inviolável — subject/payload/exp/assinatura.
/// CI roda com FAKE (sem modelo); Ollama real é o teste marcado RequiresOllama.
/// </summary>
public sealed class ConsentGuardrailTests
{
    private const string Secret = "dev-consent-secret";

    [Fact]
    public void Consent_válido_para_payload_exato()
    {
        var token = ConsentArtifact.Issue("subj-1", "SKU-CAMERA-BR", 2, TimeSpan.FromMinutes(5), Secret, DateTimeOffset.UtcNow, out _);
        var result = ConsentArtifact.Validate(token, Secret, "subj-1", "SKU-CAMERA-BR", 2, DateTimeOffset.UtcNow);
        Assert.NotNull(result);
        Assert.Equal(("subj-1", "SKU-CAMERA-BR", 2), result.Value);
    }

    [Fact]
    public void Consent_para_payload_diverso_é_rejeitado()
    {
        var token = ConsentArtifact.Issue("subj-1", "SKU-CAMERA-BR", 2, TimeSpan.FromMinutes(5), Secret, DateTimeOffset.UtcNow, out _);
        Assert.Null(ConsentArtifact.Validate(token, Secret, "subj-1", "SKU-FONE-US", 2, DateTimeOffset.UtcNow)); // sku diverso
        Assert.Null(ConsentArtifact.Validate(token, Secret, "subj-1", "SKU-CAMERA-BR", 99, DateTimeOffset.UtcNow)); // qty diversa
        Assert.Null(ConsentArtifact.Validate(token, Secret, "subj-OUTRO", "SKU-CAMERA-BR", 2, DateTimeOffset.UtcNow)); // subject diverso
    }

    [Fact]
    public void Consent_expirado_e_assinatura_forged_são_rejeitados()
    {
        var expired = ConsentArtifact.Issue("s", "sku", 1, TimeSpan.FromSeconds(-1), Secret, DateTimeOffset.UtcNow, out _);
        Assert.Null(ConsentArtifact.Validate(expired, Secret, "s", "sku", 1, DateTimeOffset.UtcNow));
        Assert.Null(ConsentArtifact.Validate("fake.forjado", Secret, "s", "sku", 1, DateTimeOffset.UtcNow));
    }
}
