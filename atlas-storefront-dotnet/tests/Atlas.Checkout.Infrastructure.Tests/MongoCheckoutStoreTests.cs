using Atlas.Checkout.Application;
using Atlas.Checkout.Domain;
using Atlas.Checkout.Infrastructure;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Checkout.Infrastructure.Tests;

/// <summary>Integração REAL: replica set local; db por execução (drop no fim).</summary>
public sealed class MongoCheckoutStoreTests : IAsyncLifetime
{
    private const string ConnStr = "mongodb://127.0.0.1:27017/?directConnection=true";
    private readonly string _dbName = $"atlas_cart_{Guid.NewGuid():N}";
    private MongoCheckoutStore _store = default!;
    private IMongoDatabase _db = default!;

    public Task InitializeAsync()
    {
        _store = MongoCheckoutStore.Create(ConnStr, _dbName);
        _db = new MongoClient(ConnStr).GetDatabase(_dbName);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _db.Client.DropDatabaseAsync(_dbName);

    [Fact]
    public async Task Cart_upsert_por_owner_e_reidratação_completa()
    {
        var cart = Cart.Create("customer-1", DateTimeOffset.UtcNow);
        cart.AddItem("sku-a", 1, DateTimeOffset.UtcNow);
        cart.AddItem("sku-b", 2, DateTimeOffset.UtcNow);
        await _store.SaveCartAsync(cart, CancellationToken.None);

        var loaded = await _store.FindCartByOwnerAsync("customer-1", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(cart.Id, loaded.Id);
        Assert.Equal(2, loaded.Lines.Count);
        Assert.Equal(2, loaded.Lines.First(l => l.SkuId == "sku-b").Quantity);
    }

    [Fact]
    public async Task Quote_salvo_e_recuperado_com_TTL_e_linhas()
    {
        var now = DateTimeOffset.UtcNow;
        var quote = PriceQuote.Issue(
            "cart-1", [new PriceQuote.QuoteLine("sku-1", 2, 2500, 5000)],
            "BRL", now, TimeSpan.FromMinutes(10));
        await _store.SaveQuoteAsync(quote, CancellationToken.None);

        var loaded = await _store.FindByIdAsync(quote.Id.Value, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(5000, loaded.TotalMinor);
        Assert.Equal(quote.ExpiresAtUtc, loaded.ExpiresAtUtc);
        Assert.True(loaded.IsValidAt(now.AddMinutes(5)));
        Assert.False(loaded.IsValidAt(now.AddMinutes(11)));
    }
}
