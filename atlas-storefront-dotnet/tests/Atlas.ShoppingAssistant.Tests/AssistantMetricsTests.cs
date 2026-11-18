using Atlas.ShoppingAssistant.Api.Metrics;
using Xunit;

namespace Atlas.ShoppingAssistant.Tests;

/// <summary>
/// Fatias 11/12 (SPEC-007+): métricas do assistente — turnos, fallbacks,
/// denials auditados (403), tool calls por nome, latência média e custo.
/// TDD: este teste falha até AssistantMetrics existir com o contrato abaixo.
/// </summary>
public sealed class AssistantMetricsTests
{
    [Fact]
    public void Snapshot_inicial_zerado()
    {
        var metrics = new AssistantMetrics(provider: "ollama-local", model: "qwen3.6:27b", costPer1kTokensUsd: 0m);
        var snap = metrics.GetSnapshot();
        Assert.Equal(0, snap.Turns);
        Assert.Equal(0, snap.Fallbacks);
        Assert.Equal(0, snap.Denials);
        Assert.Equal(0, snap.ToolCalls);
        Assert.Equal(0, snap.AvgLatencyMs);
    }

    [Fact]
    public void Turno_llm_registra_latencia_e_tools()
    {
        var metrics = new AssistantMetrics(provider: "ollama-local", model: "qwen3.6:27b", costPer1kTokensUsd: 0m);
        metrics.RecordToolCall("SearchCatalog");
        metrics.RecordToolCall("SearchCatalog");
        metrics.RecordToolCall("ProposeCartChange");
        metrics.RecordTurn(latencyMs: 1200, fallback: false);

        var snap = metrics.GetSnapshot();
        Assert.Equal(1, snap.Turns);
        Assert.Equal(0, snap.Fallbacks);
        Assert.Equal(3, snap.ToolCalls);
        Assert.Equal(1200, snap.AvgLatencyMs);
        Assert.Equal(2, snap.ToolCallsByTool["SearchCatalog"]);
        Assert.Equal(1, snap.ToolCallsByTool["ProposeCartChange"]);
    }

    [Fact]
    public void Fallback_e_denial_403_sao_contados()
    {
        var metrics = new AssistantMetrics(provider: "ollama-local", model: "qwen3.6:27b", costPer1kTokensUsd: 0m);
        metrics.RecordTurn(latencyMs: 300, fallback: true);
        metrics.RecordDenial();
        metrics.RecordDenial();

        var snap = metrics.GetSnapshot();
        Assert.Equal(1, snap.Turns);
        Assert.Equal(1, snap.Fallbacks);
        Assert.Equal(2, snap.Denials);
    }

    [Fact]
    public void Provedor_local_tem_custo_zero_declarado()
    {
        var metrics = new AssistantMetrics(provider: "ollama-local", model: "qwen3.6:27b", costPer1kTokensUsd: 0m);
        metrics.RecordTurn(latencyMs: 500, fallback: false);
        var snap = metrics.GetSnapshot();
        Assert.Equal(0m, snap.CostUsd);
        Assert.False(string.IsNullOrWhiteSpace(snap.CostNote));
    }
}
