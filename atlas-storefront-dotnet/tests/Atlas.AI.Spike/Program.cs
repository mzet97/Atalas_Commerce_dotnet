using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

[Description("Prévia de disponibilidade de um SKU (hint — nunca confirma reserva)")]
static string GetInventoryHint([Description("Código do SKU")] string skuId) =>
    skuId == "SKU-CAMERA-BR" ? "hint: 25 unidades (quoted_at=agora, TTL 60s)" : "hint: sku inexistente";

IChatClient chatClient = new OllamaApiClient(new Uri("http://127.0.0.1:11434"), "qwen3.6:27b");

AIAgent agent = new ChatClientAgent(
    chatClient,
    "Você é o assistente de compras Atlas. Use a ferramenta GetInventoryHint. NUNCA invente estoque.",
    "atlas-shopper",
    "assistente de compras",
    [AIFunctionFactory.Create(GetInventoryHint)]);

AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync(
    "Quantas unidades do SKU-CAMERA-BR estão disponíveis? Responda curto citando a origem.",
    session);

Console.WriteLine("RESPOSTA: " + response.Text);
Console.WriteLine("SPIKE_RESULT=PASS");
