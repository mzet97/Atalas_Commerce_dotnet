namespace Atlas.Offers.Domain;

/// <summary>Identidades fortes do contexto Merchandising.</summary>
public readonly record struct OfferId(Guid Value)
{
    public static OfferId New() => new(Guid.NewGuid());
    public bool IsValid => Value != Guid.Empty;
}

public readonly record struct PriceListId(Guid Value)
{
    public static PriceListId New() => new(Guid.NewGuid());
}

public enum OfferStatus
{
    Draft = 0,
    Active = 1,
    Deactivated = 2,
    Expired = 3,
}

/// <summary>
/// Agregado Offer (blueprint #02 §2): proposta de venda de um SKU por seller+market+canal
/// com janela de validade. A unicidade de "uma oferta ativa por escopo" é garantida por
/// índice único no store (persistência); o domínio valida janela e transições.
/// Alterações nunca reescrevem Order confirmado (INV-ORDER-001 — snapshots).
/// </summary>
public sealed class Offer
{
    public OfferId Id { get; }
    public string SkuId { get; }
    public string SellerId { get; }          // 1P = seller da plataforma no MVP
    public string MarketId { get; }
    public string Channel { get; }
    public Money Price { get; private set; }
    public Money ListPrice { get; }
    public DateTimeOffset ValidFromUtc { get; }
    public DateTimeOffset ValidToUtc { get; }
    public OfferStatus Status { get; private set; } = OfferStatus.Draft;
    public long Version { get; private set; }

    private Offer(OfferId id, string skuId, string sellerId, string marketId, string channel,
                  Money price, Money listPrice, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        Id = id;
        SkuId = skuId;
        SellerId = sellerId;
        MarketId = marketId;
        Channel = channel;
        Price = price;
        ListPrice = listPrice;
        ValidFromUtc = fromUtc;
        ValidToUtc = toUtc;
    }

    public static Offer Create(string skuId, string sellerId, string marketId, string channel,
        Money price, Money listPrice, DateTimeOffset validFromUtc, DateTimeOffset validToUtc)
    {
        if (string.IsNullOrWhiteSpace(skuId)) throw new ArgumentException("skuId obrigatório", nameof(skuId));
        if (string.IsNullOrWhiteSpace(sellerId)) throw new ArgumentException("sellerId obrigatório", nameof(sellerId));
        if (string.IsNullOrWhiteSpace(marketId)) throw new ArgumentException("marketId obrigatório", nameof(marketId));
        if (string.IsNullOrWhiteSpace(channel)) throw new ArgumentException("channel obrigatório", nameof(channel));
        if (price.Currency != listPrice.Currency)
            throw new InvalidOperationException("Preço e preço de lista devem ter a mesma moeda");
        if (price.AmountMinor <= 0) throw new ArgumentOutOfRangeException(nameof(price), "Preço deve ser > 0");
        if (listPrice.AmountMinor <= 0) throw new ArgumentOutOfRangeException(nameof(listPrice));
        if (price.AmountMinor > listPrice.AmountMinor)
            throw new InvalidOperationException("Preço promocional não pode exceder o preço de lista");
        if (validToUtc <= validFromUtc) throw new ArgumentException("Janela de validade inválida (fim <= início)");
        return new(OfferId.New(), skuId, sellerId, marketId, channel, price, listPrice, validFromUtc, validToUtc);
    }

    /// <summary>Ativa a oferta dentro da janela de validade (REQ-OFF-001).</summary>
    public void Activate(DateTimeOffset nowUtc)
    {
        EnsureStatus(OfferStatus.Draft, "ativar");
        if (nowUtc < ValidFromUtc) throw new InvalidOperationException("Oferta fora da janela (ainda não vigente)");
        if (nowUtc >= ValidToUtc) throw new InvalidOperationException("Oferta fora da janela (expirada)");
        Status = OfferStatus.Active;
        Version++;
    }

    public void ChangePrice(Money newPrice, DateTimeOffset nowUtc)
    {
        EnsureStatus(OfferStatus.Active, "alterar preço");
        if (newPrice.Currency != Price.Currency) throw new InvalidOperationException("Moeda não pode mudar em reprice");
        if (newPrice.AmountMinor <= 0 || newPrice.AmountMinor > ListPrice.AmountMinor)
            throw new InvalidOperationException("Novo preço inválido (fora dos limites da oferta)");
        if (nowUtc < ValidFromUtc || nowUtc >= ValidToUtc)
            throw new InvalidOperationException("Reprice fora da janela de validade");
        Price = newPrice;
        Version++;
    }

    public void Deactivate()
    {
        EnsureStatus(OfferStatus.Active, "desativar");
        Status = OfferStatus.Deactivated;
        Version++;
    }

    public bool IsExpiredAt(DateTimeOffset nowUtc) => nowUtc >= ValidToUtc;

    /// <summary>Chave de escopo para o índice único de "uma ativa por escopo" (persistência).</summary>
    public string ScopeKey() => $"{SkuId}|{SellerId}|{MarketId}|{Channel}";

    private void EnsureStatus(OfferStatus expected, string action)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Transição inválida: {Status} → {action} (esperado {expected})");
    }
}

/// <summary>Eventos de domínio do contexto (envelope aplicado na camada Application).</summary>
public sealed record OfferPublished(string OfferId, string ScopeKey, string Currency, long PriceMinor, long AggregateVersion, DateTimeOffset OccurredAtUtc);
public sealed record PriceChanged(string OfferId, long OldPriceMinor, long NewPriceMinor, long AggregateVersion, DateTimeOffset OccurredAtUtc);
