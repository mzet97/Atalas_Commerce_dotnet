using System.Collections.Concurrent;

namespace Atlas.ShoppingAssistant.Api.Metrics;

/// <summary>
/// Fatias 11/12 (SPEC-007+): contadores thread-safe do assistente.
/// Denials = 403s auditados em /assistant/cart/apply (consent inválido) —
/// a única recusa que o servidor observa de forma confiável.
/// Custo: provedor local (Ollama) não cobra por token → CostUsd = 0 com nota;
/// se um dia houver provider cloud com uso reportado, entra tokens × taxa.
/// </summary>
public sealed class AssistantMetrics(string provider, string model, decimal costPer1kTokensUsd)
{
    private long _turns;
    private long _fallbacks;
    private long _denials;
    private long _latencyMsTotal;
    private readonly ConcurrentDictionary<string, long> _toolCallsByName = new();

    public void RecordTurn(long latencyMs, bool fallback)
    {
        Interlocked.Increment(ref _turns);
        Interlocked.Add(ref _latencyMsTotal, Math.Max(0, latencyMs));
        if (fallback) Interlocked.Increment(ref _fallbacks);
    }

    public void RecordDenial() => Interlocked.Increment(ref _denials);

    public void RecordToolCall(string toolName)
    {
        if (!string.IsNullOrWhiteSpace(toolName))
            _toolCallsByName.AddOrUpdate(toolName, 1, (_, v) => v + 1);
    }

    public AssistantMetricsSnapshot GetSnapshot()
    {
        var turns = Interlocked.Read(ref _turns);
        var latency = Interlocked.Read(ref _latencyMsTotal);
        return new AssistantMetricsSnapshot(
            Turns: turns,
            Fallbacks: Interlocked.Read(ref _fallbacks),
            Denials: Interlocked.Read(ref _denials),
            ToolCalls: _toolCallsByName.Values.Sum(),
            ToolCallsByTool: new Dictionary<string, long>(_toolCallsByName),
            AvgLatencyMs: turns == 0 ? 0 : latency / turns,
            Provider: provider,
            Model: model,
            CostUsd: 0m,
            CostNote: costPer1kTokensUsd == 0m
                ? $"provedor '{provider}' sem cobrança por token (infra local, custo não medido aqui)"
                : $"taxa configurada {costPer1kTokensUsd}/1k tokens, mas o provider não reportou uso neste turno");
    }
}

public sealed record AssistantMetricsSnapshot(
    long Turns,
    long Fallbacks,
    long Denials,
    long ToolCalls,
    Dictionary<string, long> ToolCallsByTool,
    long AvgLatencyMs,
    string Provider,
    string Model,
    decimal CostUsd,
    string CostNote);
