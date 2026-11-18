namespace Atlas.ShoppingAssistant.Api.Workflows;

/// <summary>
/// F2 (SPEC-007+): mensagens do grafo de comparação (search → details → rank).
/// Somente leitura; a mutação continua fora do grafo (consent artifact ADR-0009).
/// </summary>
public sealed record CompareRequest(string Query, string Market);

public sealed record ShortProduct(string Title, string MarketId, string ProductId);

public sealed record ProductShortlist(string Query, string Market, List<ShortProduct> Items);

public sealed record RankedCandidate(string SkuId, string Title, long? PriceMinor, string? Currency, string Hint);

public sealed record ComparisonDraft(List<RankedCandidate> Candidates);
