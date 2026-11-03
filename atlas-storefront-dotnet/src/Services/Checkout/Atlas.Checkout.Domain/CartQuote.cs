namespace Atlas.Checkout.Domain;

/// <summary>Identidades fortes do contexto Cart/Quote.</summary>
public readonly record struct CartId(Guid Value)
{
    public static CartId New() => new(Guid.NewGuid());
    public bool IsValid => Value != Guid.Empty;
}

public readonly record struct QuoteId(Guid Value)
{
    public static QuoteId New() => new(Guid.NewGuid());
    public bool IsValid => Value != Guid.Empty;
}

/// <summary>Linha do carrinho: SKU + quantidade. Preço NÃO é armazenado (a cotação define).</summary>
public sealed record CartLine(string SkuId, int Quantity);

/// <summary>
/// Agregado Cart (UC-002): linhas SKU+quantidade do cliente autenticado ou guest.
/// O carrinho NÃO define preço nem confirma estoque (INV-CQRS-001) — a cotação faz.
/// Estoque 0 pode exibir indisponível, mas não promete checkout (INV-03).
/// </summary>
public sealed class Cart
{
    private readonly List<CartLine> _lines;

    private Cart(CartId id, string ownerId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        OwnerId = ownerId;
        CreatedAtUtc = createdAtUtc;
        _lines = [];
    }

    public CartId Id { get; }
    public string OwnerId { get; }         // customer_id ou guest_id
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public IReadOnlyList<CartLine> Lines => _lines;

    public static Cart Create(string ownerId, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("owner_id obrigatório", nameof(ownerId));
        return new(CartId.New(), ownerId, nowUtc);
    }

    public static Cart Hydrate(CartId id, string ownerId, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        IReadOnlyList<CartLine> lines)
    {
        var cart = new Cart(id, ownerId, createdAtUtc);
        cart.UpdatedAtUtc = updatedAtUtc;
        cart._lines.AddRange(lines);
        return cart;
    }

    /// <summary>Adiciona SKU: soma quantidade se já existir; rejeita qty ≤ 0 e carrinho > 50 linhas.</summary>
    public Cart AddItem(string skuId, int quantity, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(skuId)) throw new ArgumentException("sku_id obrigatório", nameof(skuId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (_lines.Count >= 50 && _lines.All(l => l.SkuId != skuId))
            throw new InvalidOperationException("Carrinho com número máximo de linhas atingido");

        var existing = _lines.FindIndex(l => l.SkuId == skuId);
        if (existing >= 0)
            _lines[existing] = _lines[existing] with { Quantity = _lines[existing].Quantity + quantity };
        else
            _lines.Add(new CartLine(skuId, quantity));

        UpdatedAtUtc = nowUtc;
        return this;
    }

    /// <summary>Remove linha inteira; rejeita se SKU não está no carrinho.</summary>
    public Cart RemoveItem(string skuId, DateTimeOffset nowUtc)
    {
        var removed = _lines.RemoveAll(l => l.SkuId == skuId);
        if (removed == 0) throw new InvalidOperationException($"SKU {skuId} não está no carrinho");
        UpdatedAtUtc = nowUtc;
        return this;
    }

    /// <summary>Limpa o carrinho (após confirmação ou abandono).</summary>
    public void Clear(DateTimeOffset nowUtc)
    {
        _lines.Clear();
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Faz merge das linhas do carrinho guest no carrinho do cliente no login (soma quantidades).</summary>
    public static Cart Merge(Cart guestCart, Cart customerCart, DateTimeOffset nowUtc)
    {
        foreach (var line in guestCart._lines)
            customerCart.AddItem(line.SkuId, line.Quantity, nowUtc);
        return customerCart;
    }
}

/// <summary>
/// Price Quote (UC-002, INV-QUOTE-001): cotação CONGELADA com TTL e breakdown.
/// A confirmação do checkout exige quote vigente (COM-08); vencida exige re-cotação
/// com revisão da diferença. Nenhum preço é recalculado da Offer corrente na confirmação.
/// </summary>
public sealed class PriceQuote
{
    public QuoteId Id { get; }
    public string CartId { get; }
    public long TotalMinor { get; }
    public string Currency { get; }
    public IReadOnlyList<QuoteLine> Lines { get; }
    public DateTimeOffset IssuedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }

    public PriceQuote(QuoteId id, string cartId, long totalMinor, string currency,
        IReadOnlyList<QuoteLine> lines, DateTimeOffset issuedAtUtc, DateTimeOffset expiresAtUtc)
    {
        if (expiresAtUtc <= issuedAtUtc) throw new ArgumentException("TTL da cotação inválido (fim <= início)");
        Id = id;
        CartId = cartId;
        TotalMinor = totalMinor;
        Currency = currency;
        Lines = lines;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public sealed record QuoteLine(string SkuId, int Quantity, long UnitPriceMinor, long LineTotalMinor);

    public static PriceQuote Issue(string cartId, IReadOnlyList<QuoteLine> lines,
        string currency, DateTimeOffset nowUtc, TimeSpan ttl)
    {
        if (lines.Count == 0) throw new ArgumentException("Cotação sem linhas", nameof(lines));
        if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl), "TTL deve ser > 0");
        var total = lines.Aggregate(0L, (acc, l) => acc + l.LineTotalMinor);
        return new(QuoteId.New(), cartId, total, currency, lines, nowUtc, nowUtc.Add(ttl));
    }

    /// <summary>A cotação está vigente AGORA (clock do servidor — nunca do cliente, INV-QUOTE-001).</summary>
    public bool IsValidAt(DateTimeOffset nowUtc) => nowUtc < ExpiresAtUtc;

    /// <summary>Diferença de totais para a re-cotação (COM-08: revisão da diferença).</summary>
    public long DifferenceMinor(PriceQuote newQuote)
    {
        if (Currency != newQuote.Currency) throw new InvalidOperationException("Moedas incompatíveis na re-cotação");
        return newQuote.TotalMinor - TotalMinor;
    }
}
