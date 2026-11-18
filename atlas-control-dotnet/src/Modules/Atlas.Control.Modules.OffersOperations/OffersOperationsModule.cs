using Atlas.Control.Modules.Contracts;
using Atlas.Control.Modules.Kernel;
using Microsoft.AspNetCore.Http;
using Results = Microsoft.AspNetCore.Http.Results;

namespace Atlas.Control.Modules.OffersOperations;

/// <summary>Oferta da grade (preço por SKU × mercado; record serializável).</summary>
public sealed record OfferRowDto(
    string OfferId, string SkuId, string SellerId, string MarketId, string Channel,
    string Currency, long PriceMinor, long ListPriceMinor, long Version, string ValidToUtc);

/// <summary>Port do Control para o Offers owner (CRUD de preço; DIP: adapter no Host).</summary>
public interface IOffersOwnerCatalog
{
    Task<(IReadOnlyList<OfferRowDto> Items, long Total)> ListAsync(
        string? marketId, string? skuId, int page, int pageSize, CancellationToken ct);
    Task<(bool Accepted, string OfferId, long Version, string? Reason)> PublishAsync(
        string requestId, string actorSub, string skuId, string sellerId, string marketId,
        string channel, string currency, long priceMinor, long listPriceMinor, CancellationToken ct);
    Task<(bool Accepted, long Version, string? Reason, string? EventId)> UpdatePriceAsync(
        string requestId, string actorSub, string offerId, long expectedVersion, long priceMinor, CancellationToken ct);
    Task<(bool Accepted, long Version, string? Reason, string? EventId)> DeactivateAsync(
        string requestId, string actorSub, string offerId, long expectedVersion, CancellationToken ct);
}

/// <summary>
/// Operações de ofertas do Control (ERP master data): preço por SKU × mercado.
/// Role `catalog_editor` + auditoria em tudo (mesmo perímetro do catálogo).
/// Reprice/desativação têm efeito IMEDIATO na vitrine — mudar regra p/ four-eyes
/// é evolução explícita (oferta carrega version; CAS já blinda concorrência).
/// </summary>
public sealed class OffersOperationsModule(
    IOffersOwnerCatalog offers,
    AuditChainStore audit) : IAtlasModule
{
    public string Name => "offers-operations";

    public void Configure(IModuleEndpoints endpoints)
    {
        endpoints.MapGet("/modules/offers-operations/status", async (CancellationToken ct) =>
        {
            await Task.CompletedTask;
            return Results.Ok(new { module = Name });
        });

        // ---- grade de ofertas (leitura operacional) ----
        endpoints.MapGet("/modules/offers", async (
            string? market, string? sku, int? page, int? pageSize, CancellationToken ct) =>
        {
            var (items, total) = await offers.ListAsync(market, sku, page ?? 1, pageSize ?? 50, ct);
            return Results.Ok(new { items, total, page = page ?? 1, pageSize = pageSize ?? 50 });
        });

        // ---- criar oferta (preço por SKU × mercado × canal) ----
        endpoints.MapPost("/modules/offers", "role:catalog_editor",
            async (HttpContext http, OfferRequest req, CancellationToken ct) =>
        {
            var actor = Sub(http);
            if (string.IsNullOrWhiteSpace(req.SkuId) || string.IsNullOrWhiteSpace(req.MarketId))
                return Results.Json(new { error = "skuId e marketId obrigatórios", audited = true }, statusCode: 400);
            if (string.IsNullOrWhiteSpace(req.Currency) || req.PriceMinor <= 0)
                return Results.Json(new { error = "moeda (ISO 4217) e priceMinor > 0 obrigatórios", audited = true }, statusCode: 400);
            var (accepted, offerId, version, reason) = await offers.PublishAsync(
                $"web-{Guid.NewGuid():N}", actor, req.SkuId, req.SellerId ?? "atlas-1p",
                req.MarketId, req.Channel ?? "web", req.Currency.ToUpperInvariant(),
                req.PriceMinor, req.ListPriceMinor <= 0 ? req.PriceMinor : req.ListPriceMinor, ct);
            if (!accepted)
            {
                await audit.AppendAsync(actor, "offer.publish", "DENIED", $"sku={req.SkuId} market={req.MarketId} motivo={reason}", Corr(), ct);
                return Results.Json(new { error = reason, audited = true }, statusCode: 409);
            }
            await audit.AppendAsync(actor, "offer.publish", "PUBLISHED", $"offer={offerId} sku={req.SkuId} market={req.MarketId} preco={req.PriceMinor} {req.Currency}", Corr(), ct);
            return Results.Created($"/modules/offers/{offerId}", new { offerId, version });
        });

        // ---- reprice com CAS (efeito imediato na vitrine) ----
        endpoints.MapPut("/modules/offers/{id}", "role:catalog_editor",
            async (HttpContext http, string id, OfferReprice req, CancellationToken ct) =>
        {
            var actor = Sub(http);
            if (req.PriceMinor <= 0)
                return Results.Json(new { error = "priceMinor > 0 obrigatório", audited = true }, statusCode: 400);
            var (accepted, version, reason, _) = await offers.UpdatePriceAsync(
                $"web-{Guid.NewGuid():N}", actor, id, req.ExpectedVersion, req.PriceMinor, ct);
            if (!accepted)
            {
                await audit.AppendAsync(actor, "offer.reprice", "DENIED", $"offer={id} motivo={reason}", Corr(), ct);
                var notFound = reason == "oferta inexistente";
                return Results.Json(new { error = reason, audited = true }, statusCode: notFound ? 404 : 409);
            }
            await audit.AppendAsync(actor, "offer.reprice", "REPRICED", $"offer={id} preco={req.PriceMinor} v={version}", Corr(), ct);
            return Results.Ok(new { offerId = id, version });
        });

        // ---- desativar (sai da vitrine; histórico preservado) ----
        endpoints.MapDelete("/modules/offers/{id}", "role:catalog_editor",
            async (HttpContext http, string id, long expectedVersion, CancellationToken ct) =>
        {
            var actor = Sub(http);
            var (accepted, version, reason, _) = await offers.DeactivateAsync(
                $"web-{Guid.NewGuid():N}", actor, id, expectedVersion, ct);
            if (!accepted)
            {
                await audit.AppendAsync(actor, "offer.deactivate", "DENIED", $"offer={id} motivo={reason}", Corr(), ct);
                var notFound = reason == "oferta inexistente";
                return Results.Json(new { error = reason, audited = true }, statusCode: notFound ? 404 : 409);
            }
            await audit.AppendAsync(actor, "offer.deactivate", "DEACTIVATED", $"offer={id} v={version}", Corr(), ct);
            return Results.Ok(new { offerId = id, version });
        });
    }

    private static string Sub(HttpContext http) =>
        http.User.FindFirst("sub")?.Value
        ?? http.User.FindFirst("preferred_username")?.Value
        ?? http.User.Identity?.Name
        ?? "anonymous";

    private static string Corr() => $"m4-offers-{Guid.NewGuid():N}";

    public sealed record OfferRequest(
        string SkuId, string? SellerId, string MarketId, string? Channel,
        string Currency, long PriceMinor, long ListPriceMinor);
    public sealed record OfferReprice(long ExpectedVersion, long PriceMinor);
}
