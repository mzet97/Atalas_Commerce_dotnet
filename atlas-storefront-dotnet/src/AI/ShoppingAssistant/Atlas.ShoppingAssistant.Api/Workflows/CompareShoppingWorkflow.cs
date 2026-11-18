using Atlas.ShoppingAssistant.Api.Metrics;
using Atlas.ShoppingAssistant.Api.Tools;
using Microsoft.Agents.AI.Workflows;

namespace Atlas.ShoppingAssistant.Api.Workflows;

/// <summary>
/// F2 (SPEC-007+): grafo de comparação sobre o MAF Workflows 1.26.0
/// (versão pinada, mesma do MAF): search → details → rank.
/// Sem LLM: executores chamam as tools read-only; saída via output do grafo.
/// </summary>
public sealed class CompareShoppingWorkflow
{
    private readonly Workflow _workflow;

    public CompareShoppingWorkflow(CatalogTools tools)
    {
        var search = new SearchExecutor(tools);
        var details = new DetailsExecutor(tools);
        var rank = new RankExecutor(tools);

        var searchBinding = search.BindExecutor();
        var detailsBinding = details.BindExecutor();
        var rankBinding = rank.BindExecutor();

        var builder = new WorkflowBuilder(searchBinding);
        builder.AddEdge(searchBinding, detailsBinding);
        builder.AddEdge(detailsBinding, rankBinding);
        builder.WithOutputFrom([rankBinding]);
        _workflow = builder.Build(validateOrphans: false);
    }

    public async Task<string> CompareAsync(string query, string market, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        await using var run = await InProcessExecution.RunStreamingAsync(
            _workflow, new CompareRequest(query, market), cancellationToken: ct);
        await foreach (var evt in run.WatchStreamAsync(ct))
        {
            if (evt is WorkflowOutputEvent output && output.Is<string>(out var text) && !string.IsNullOrEmpty(text))
                sb.Append(text);
        }
        var result = sb.ToString();
        return string.IsNullOrEmpty(result)
            ? "comparação indisponível (grafo sem saída)"
            : result;
    }
}
