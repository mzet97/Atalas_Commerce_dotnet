using System.Text.RegularExpressions;
using Atlas.ShoppingAssistant.Api.Tools;
using Microsoft.Agents.AI.Workflows;

namespace Atlas.ShoppingAssistant.Api.Workflows;

/// <summary>
/// F2 (SPEC-007+): executores do grafo de comparação sobre o MAF Workflows 1.26.0
/// (versão pinada do MAF). Stateless → declareCrossRunShareable: true.
/// Todos os passos são read-only e grounded nos owners (INV-AI-001, INV-CQRS-001).
/// </summary>
public sealed partial class SearchExecutor(CatalogTools tools)
    : Executor<CompareRequest>("search", null, declareCrossRunShareable: true)
{
    [GeneratedRegex(@"^- (.+) \(mercado (.+), id (.+)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex ProductLine();

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        base.ConfigureProtocol(protocolBuilder);
        protocolBuilder.SendsMessage<ProductShortlist>(); return protocolBuilder;
    }

    public override async ValueTask HandleAsync(
        CompareRequest message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        var text = await tools.SearchCatalog(message.Query, message.Market, cancellationToken);
        var items = new List<ShortProduct>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var m = ProductLine().Match(line);
            if (m.Success) items.Add(new ShortProduct(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value));
        }
        await context.SendMessageAsync(new ProductShortlist(message.Query, message.Market, items), "details", cancellationToken);
    }
}

public sealed partial class DetailsExecutor(CatalogTools tools)
    : Executor<ProductShortlist>("details", null, declareCrossRunShareable: true)
{
    [GeneratedRegex(@"oferta: (\d+) minor (\w+)", RegexOptions.CultureInvariant)]
    private static partial Regex OfferLine();

    [GeneratedRegex(@"skus ([A-Za-z0-9\-,]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SkusLine();

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        base.ConfigureProtocol(protocolBuilder);
        protocolBuilder.SendsMessage<ComparisonDraft>(); return protocolBuilder;
    }

    public override async ValueTask HandleAsync(
        ProductShortlist message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        var candidates = new List<RankedCandidate>();
        foreach (var p in message.Items.Take(3))
        {
            var details = await tools.GetProductDetails(p.ProductId, cancellationToken);
            long? price = null;
            string? currency = null;
            var om = OfferLine().Match(details);
            if (om.Success)
            {
                price = long.Parse(om.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                currency = om.Groups[2].Value;
            }
            var sku = SkusLine().Match(details) is { Success: true } sm
                ? sm.Groups[1].Value.Split(',').First()
                : p.ProductId;
            var hint = await tools.GetInventoryHint(sku, cancellationToken);
            candidates.Add(new RankedCandidate(sku, p.Title, price, currency, hint));
        }
        await context.SendMessageAsync(new ComparisonDraft(candidates), "rank", cancellationToken);
    }
}

public sealed class RankExecutor(CatalogTools tools)
    : Executor<ComparisonDraft>("rank", null, declareCrossRunShareable: true)
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        base.ConfigureProtocol(protocolBuilder);
        protocolBuilder.YieldsOutput<string>(); return protocolBuilder;
    }

    public override async ValueTask HandleAsync(
        ComparisonDraft message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        if (message.Candidates.Count == 0)
        {
            await context.YieldOutputAsync("nenhum produto encontrado para comparar (busca grounded, sem invenção).", cancellationToken);
            return;
        }

        var ordered = message.Candidates
            .OrderBy(c => c.PriceMinor is null)
            .ThenBy(c => c.PriceMinor ?? long.MaxValue)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("comparação grounded (preços dos owners, hint não confirma estoque):");
        var pos = 1;
        foreach (var c in ordered)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"{pos}. {c.Title} [{c.SkuId}] — {(c.PriceMinor is null ? "sem oferta ativa" : $"{c.PriceMinor} {c.Currency}")} — {c.Hint}");
            pos++;
        }

        var best = ordered.First();
        if (best.PriceMinor is not null)
        {
            var proposal = await tools.ProposeCartChange(
                best.SkuId, best.Title, 1, best.PriceMinor.Value, best.Currency ?? "BRL", cancellationToken);
            sb.AppendLine(proposal);
        }

        await context.YieldOutputAsync(sb.ToString(), cancellationToken);
    }
}
