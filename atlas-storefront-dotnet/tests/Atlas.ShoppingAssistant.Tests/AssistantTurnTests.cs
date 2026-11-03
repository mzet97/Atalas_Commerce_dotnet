
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Atlas.ShoppingAssistant.Api.Fake;
using Xunit;

namespace Atlas.ShoppingAssistant.Tests;

/// <summary>
/// Gate M7 com FAKE (CI): allowlist read-only, proposta inerte, fallback.
/// Tools de mutação/pagamento NÃO EXISTEM no agente — guardrail em código.
/// </summary>
public sealed class AssistantTurnTests
{
    private static ChatClientAgent BuildAgent(ScriptedChatClient client)
    {
        return new ChatClientAgent(
            client,
            instructions: "assistente Atlas (teste)",
            name: "atlas-shopper",
            description: "teste",
            tools: []);
    }

    [Fact]
    public async Task Turn_com_fake_responde_dentro_do_escopo()
    {
        var client = new ScriptedChatClient(
            ScriptedChatClient.Text("Encontrei: Câmera Atlas Pro X200 (grounded na busca)."));
        var agent = BuildAgent(client);
        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync("recomende uma câmera", session);
        Assert.Contains("Câmera", response.Text);
    }

    [Fact]
    public void Allowlist_não_contém_tools_de_mutação_ou_pagamento()
    {
        // reflexão: nenhuma tool pública do módulo pode mutar carrinho/pagamento/estoque/IAM
        var assembly = typeof(ScriptedChatClient).Assembly;
        var forbidden = new[] { "Refund", "Capture", "AdjustStock", "ModifyPrice", "GrantRole", "SubmitCheckout", "PlaceOrder" };
        var offenders = assembly.GetTypes()
            .SelectMany(t => t.GetMethods())
            .Where(m => forbidden.Any(f => m.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();
        Assert.True(offenders.Count == 0, $"tools de mutação presentes: {string.Join(", ", offenders)}");
    }

    [Fact]
    public async Task Proposta_inerte_nunca_aplica_carrinho()
    {
        // ProposeCartChange só FORMATA a proposta — nenhuma chamada HTTP de mutação existe na tool
        var tools = new Atlas.ShoppingAssistant.Api.Tools.CatalogTools(
            new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5180") }, "http://127.0.0.1:5180");
        var result = await tools.ProposeCartChange("SKU-1", "Produto", 1, 100, "BRL", CancellationToken.None);
        Assert.StartsWith("PROPOSTA INERTE", result);
    }
}
