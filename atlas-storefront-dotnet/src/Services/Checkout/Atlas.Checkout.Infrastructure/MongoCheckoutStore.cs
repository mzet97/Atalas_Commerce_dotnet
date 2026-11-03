namespace Atlas.Checkout.Infrastructure;

using Atlas.Checkout.Application;
using Atlas.Checkout.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

/// <summary>Store Mongo do Cart e PriceQuote (ADR-0002): mesmo owner (Checkout Service).</summary>
public sealed class MongoCheckoutStore : IQuoteStore
{
    private static readonly bool _guidRegistered = RegisterGuid();

    private static bool RegisterGuid()
    {
        try { BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); }
        catch (BsonSerializationException) { }
        return true;
    }

    private readonly IMongoDatabase _db;

    public MongoCheckoutStore(IMongoDatabase db) => _db = db;

    public static MongoCheckoutStore Create(string connectionString, string databaseName) =>
        new(new MongoClient(connectionString).GetDatabase(databaseName));

    private IMongoCollection<CartDoc> Carts() => _db.GetCollection<CartDoc>("checkout.carts");
    private IMongoCollection<QuoteDoc> Quotes() => _db.GetCollection<QuoteDoc>("checkout.quotes");

    // ---- Cart ----
    public async Task SaveCartAsync(Cart cart, CancellationToken ct)
    {
        var doc = CartDoc.From(cart);
        await Carts().ReplaceOneAsync(
            c => c.OwnerId == doc.OwnerId, doc,
            new ReplaceOptions { IsUpsert = true }, cancellationToken: ct);
    }

    public async Task<Cart?> FindCartByOwnerAsync(string ownerId, CancellationToken ct)
    {
        var doc = await Carts().Find(c => c.OwnerId == ownerId).FirstOrDefaultAsync(ct);
        return doc?.ToCart();
    }

    // ---- Quote (IQuoteStore) ----
    public async Task<Atlas.Checkout.Domain.PriceQuote?> FindByIdAsync(Guid quoteId, CancellationToken ct)
    {
        var doc = await Quotes().Find(q => q.Id == quoteId).FirstOrDefaultAsync(ct);
        return doc?.ToQuote();
    }

    public async Task SaveQuoteAsync(Atlas.Checkout.Domain.PriceQuote quote, CancellationToken ct)
    {
        var doc = QuoteDoc.From(quote);
        await Quotes().ReplaceOneAsync(
            q => q.Id == doc.Id, doc,
            new ReplaceOptions { IsUpsert = true }, cancellationToken: ct);
    }

    // ---- documentos ----
    public sealed class CartDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string OwnerId { get; set; } = default!;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public List<CartLineDoc> Lines { get; set; } = [];

        public sealed record CartLineDoc(string SkuId, int Quantity);

        public static CartDoc From(Cart c) => new()
        {
            Id = c.Id.Value,
            OwnerId = c.OwnerId,
            CreatedAtUtc = c.CreatedAtUtc,
            UpdatedAtUtc = c.UpdatedAtUtc,
            Lines = [.. c.Lines.Select(l => new CartLineDoc(l.SkuId, l.Quantity))],
        };

        public Cart ToCart() => Cart.Hydrate(
            new CartId(Id), OwnerId, CreatedAtUtc, UpdatedAtUtc,
            [.. Lines.Select(l => new CartLine(l.SkuId, l.Quantity))]);
    }

    public sealed class QuoteDoc
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string CartId { get; set; } = default!;
        public long TotalMinor { get; set; }
        public string Currency { get; set; } = default!;
        public List<QuoteLineDoc> Lines { get; set; } = [];
        public DateTimeOffset IssuedAtUtc { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }

        public sealed record QuoteLineDoc(string SkuId, int Quantity, long UnitPriceMinor, long LineTotalMinor);

        public static QuoteDoc From(Atlas.Checkout.Domain.PriceQuote q) => new()
        {
            Id = q.Id.Value,
            CartId = q.CartId,
            TotalMinor = q.TotalMinor,
            Currency = q.Currency,
            Lines = [.. q.Lines.Select(l => new QuoteLineDoc(l.SkuId, l.Quantity, l.UnitPriceMinor, l.LineTotalMinor))],
            IssuedAtUtc = q.IssuedAtUtc,
            ExpiresAtUtc = q.ExpiresAtUtc,
        };

        public Atlas.Checkout.Domain.PriceQuote ToQuote() => new(
            new Atlas.Checkout.Domain.QuoteId(Id), CartId, TotalMinor, Currency,
            [.. Lines.Select(l => new Atlas.Checkout.Domain.PriceQuote.QuoteLine(l.SkuId, l.Quantity, l.UnitPriceMinor, l.LineTotalMinor))],
            IssuedAtUtc, ExpiresAtUtc);
    }
}
