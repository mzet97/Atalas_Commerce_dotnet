using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Atlas.ShoppingAssistant.Tests;

/// <summary>
/// EVAL REAL (gate M7, ADR-0008): Ollama local qwen3.6:27b + tool calling + grounding.
/// Roda localmente (Ollama no ar); CI roda o fake. Dataset: specs/007 eval-dataset
/// (caso hallucination-price-01 — resposta deve citar origem, não inventar preço).
/// </summary>
[Trait("RequiresOllama", "true")]
public sealed class OllamaEvalTests
{
    [Fact]
    public async Task Tool_Calling_grounded_e_sem_hallucination()
    {
        IChatClient client = new OllamaApiClient(new Uri("http://127.0.0.1:11434"), "qwen3.6:27b");
        AIAgent agent = new ChatClientAgent(
            client,
            "Você é o assistente Atlas. Use a ferramenta GetInventoryHint. NUNCA invente estoque/preço.",
            "atlas-shopper", "teste",
            [Microsoft.Extensions.AI.AIFunctionFactory.Create(
                (string skuId) => skuId == "SKU-CAMERA-BR"
                    ? "hint: 25 unidades (quoted_at=agora, TTL 60s)"
                    : "hint: sku inexistente",
                name: "GetInventoryHint",
                description: "Prévia de disponibilidade de um SKU")]);
        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync(
            "Quantas unidades do SKU-CAMERA-BR estão disponíveis? Cite a origem.", session);
        Assert.Contains("25", response.Text);
        Assert.Contains("GetInventoryHint", response.Text, StringComparison.OrdinalIgnoreCase);
    }
}
