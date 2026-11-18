using Atlas.Offers.Application;
using Atlas.Offers.Domain;
using Atlas.Offers.Infrastructure;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Offers.IntegrationTests;

/// <summary>Integração real: replica set local; banco por execução. TEST-002-003 (escopo único ativo).</summary>
public sealed class MongoOffersStoreTests : IAsyncLifetime
{
    private const string ConnStr = "mongodb://127.0.0.1:27017/?directConnection=true";
    private readonly string _dbName = $"atlas_off_{Guid.NewGuid():N}";
    private MongoOffersStore _store = default!;
    private IMongoDatabase _db = default!;
    private readonly DateTimeOffset _from = DateTimeOffset.UtcNow.AddMinutes(-1);
    private readonly DateTimeOffset _to = DateTimeOffset.UtcNow.AddDays(30);

    public Task InitializeAsync()
    {
        _store = MongoOffersStore.Create(ConnStr, _dbName);
        _db = new MongoClient(ConnStr).GetDatabase(_dbName);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _db.Client.DropDatabaseAsync(_dbName);

    private PublishOfferCommand Cmd(string requestId = "off-1", long price = 1000, string sku = "sku-1") => new(
        RequestId: requestId, ActorSub: "pricing-mgr-1",
        SkuId: sku, SellerId: "seller-1p", MarketId: "BR", Channel: "web",
        Currency: "BRL", PriceMinor: price, ListPriceMinor: 1500,
        ValidFromUtc: _from, ValidToUtc: _to, CorrelationId: $"corr-{requestId}");

    private async Task<int> OutboxCountAsync() =>
        (int)await _db.GetCollection<MongoOffersStore.OutboxDoc>("offers.outbox").CountDocumentsAsync(_ => true);

    [Fact]
    public async Task Publicar_oferta_ativa_grava_evento_e_segunda_no_mesmo_escopo_é_rejeitada()
    {
        var first = await _store.PublishInTransactionAsync(Cmd(), CancellationToken.None);
        Assert.True(first.Accepted, first.Reason);
        Assert.Equal(1, await OutboxCountAsync());

        // sequencial: mesma chave de escopo, requestId diferente
        var second = await _store.PublishInTransactionAsync(Cmd("off-2"), CancellationToken.None);
        Assert.False(second.Accepted);
        Assert.Contains("escopo", second.Reason);
        Assert.Equal(1, await OutboxCountAsync()); // rejeitada não gera evento
    }

    [Fact]
    public async Task Corrida_no_mesmo_escopo_elege_exatamente_uma_oferta_ativa()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            Task.Run(async () => await _store.PublishInTransactionAsync(Cmd($"race-{i}", price: 900 + i), CancellationToken.None))));

        Assert.Equal(1, results.Count(r => r.Accepted));
        Assert.Equal(7, results.Count(r => !r.Accepted));
        Assert.Equal(1, await OutboxCountAsync()); // apenas o vencedor publica evento
    }

    [Fact]
    public async Task Idempotência_por_request_id_não_duplica_evento()
    {
        var first = await _store.PublishInTransactionAsync(Cmd("idem-1"), CancellationToken.None);
        var again = await _store.PublishInTransactionAsync(Cmd("idem-1"), CancellationToken.None);

        Assert.True(first.Accepted);
        Assert.True(again.Accepted); // idempotente = sucesso sem efeito colateral
        Assert.Equal(first.OfferId, again.OfferId);
        Assert.Equal(1, await OutboxCountAsync());
    }

    [Fact]
    public async Task Preço_acima_do_lista_é_negado_pelo_domínio_sem_evento()
    {
        var outcome = await _store.PublishInTransactionAsync(Cmd("off-x", price: 2000), CancellationToken.None);
        Assert.False(outcome.Accepted);
        Assert.Equal(0, await OutboxCountAsync());
    }

    [Fact]
    public async Task Desativação_libera_o_escopo_para_nova_oferta_ativa()
    {
        var first = await _store.PublishInTransactionAsync(Cmd("off-1"), CancellationToken.None);
        Assert.True(first.Accepted);

        var col = _db.GetCollection<MongoOffersStore.OfferDoc>("offers.offers");
        await col.UpdateOneAsync(o => o.Id == first.OfferId.Value,
            Builders<MongoOffersStore.OfferDoc>.Update.Set(o => o.Status, OfferStatus.Deactivated));

        var nova = await _store.PublishInTransactionAsync(Cmd("off-3", price: 1200), CancellationToken.None);
        Assert.True(nova.Accepted, nova.Reason); // índice parcial não enxerga a desativada
        Assert.Equal(2, await OutboxCountAsync());
    }
}

/// <summary>Reprice/deactivate via store (fatia preço no Control): CAS + eventos.
/// Requer Mongo (mesmo harness do projeto); sem runner local, roda em CI/infra.
/// </summary>
public sealed class OfferPriceChangeStoreTests : IAsyncLifetime
{
    private const string ConnStr = "mongodb://127.0.0.1:27017/?directConnection=true";
    private readonly string _dbName = $"atlas_off_{Guid.NewGuid():N}";
    private MongoOffersStore _store = default!;
    private IMongoDatabase _db = default!;
    private readonly DateTimeOffset _from = DateTimeOffset.UtcNow.AddMinutes(-1);
    private readonly DateTimeOffset _to = DateTimeOffset.UtcNow.AddDays(30);

    public Task InitializeAsync()
    {
        _store = MongoOffersStore.Create(ConnStr, _dbName);
        _db = new MongoClient(ConnStr).GetDatabase(_dbName);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _db.Client.DropDatabaseAsync(_dbName);

    private async Task<OfferId> PublishAsync(string req = "off-p1", long price = 1000)
    {
        var r = await _store.PublishInTransactionAsync(new PublishOfferCommand(
            RequestId: req, ActorSub: "pricing-mgr-1", SkuId: "sku-p1", SellerId: "seller-1p",
            MarketId: "BR", Channel: "web", Currency: "BRL", PriceMinor: price, ListPriceMinor: 1500,
            ValidFromUtc: _from, ValidToUtc: _to, CorrelationId: $"corr-{req}"), CancellationToken.None);
        Assert.True(r.Accepted, r.Reason);
        return r.OfferId;
    }

    private async Task<int> OutboxCountAsync() =>
        (int)await _db.GetCollection<MongoOffersStore.OutboxDoc>("offers.outbox").CountDocumentsAsync(_ => true);

    private async Task<long> VersionAsync(OfferId id) =>
        (await _db.GetCollection<MongoOffersStore.OfferDoc>("offers.offers")
            .Find(o => o.Id == id.Value).FirstAsync()).Version;

    [Fact]
    public async Task Reprice_valido_incrementa_versao_e_emite_PriceChangedV1()
    {
        var id = await PublishAsync();
        var v0 = await VersionAsync(id);
        var r = await _store.UpdatePriceInTransactionAsync(new UpdateOfferPriceCommand(
            "upd-1", "pricing-mgr-1", id, v0, 900, "corr-upd-1"), CancellationToken.None);
        Assert.True(r.Accepted, r.Reason);
        Assert.Equal(v0 + 1, r.Version);
        Assert.NotNull(r.EventId);
        Assert.Equal(2, await OutboxCountAsync()); // published + pricechanged
    }

    [Fact]
    public async Task Reprice_com_versao_defasada_é_conflito_sem_evento()
    {
        var id = await PublishAsync();
        var v0 = await VersionAsync(id);
        var ok = await _store.UpdatePriceInTransactionAsync(new UpdateOfferPriceCommand(
            "upd-1", "pricing-mgr-1", id, v0, 900, "corr-upd-1"), CancellationToken.None);
        Assert.True(ok.Accepted);
        var stale = await _store.UpdatePriceInTransactionAsync(new UpdateOfferPriceCommand(
            "upd-2", "pricing-mgr-1", id, v0, 800, "corr-upd-2"), CancellationToken.None);
        Assert.False(stale.Accepted);
        Assert.Contains("conflito", stale.Reason);
        Assert.Equal(2, await OutboxCountAsync()); // rejeitado não gera evento
    }

    [Fact]
    public async Task Reprice_acima_do_lista_é_negado_sem_evento()
    {
        var id = await PublishAsync();
        var v0 = await VersionAsync(id);
        var r = await _store.UpdatePriceInTransactionAsync(new UpdateOfferPriceCommand(
            "upd-1", "pricing-mgr-1", id, v0, 1600, "corr-upd-1"), CancellationToken.None);
        Assert.False(r.Accepted);
        Assert.Equal(1, await OutboxCountAsync());
    }

    [Fact]
    public async Task Desativar_emite_OfferDeactivatedV1_e_libera_reprice_futuro()
    {
        var id = await PublishAsync();
        var v0 = await VersionAsync(id);
        var d = await _store.DeactivateInTransactionAsync(
            "deact-1", "pricing-mgr-1", id, v0, "corr-deact-1", CancellationToken.None);
        Assert.True(d.Accepted, d.Reason);
        Assert.Equal(2, await OutboxCountAsync());
        var reprice = await _store.UpdatePriceInTransactionAsync(new UpdateOfferPriceCommand(
            "upd-2", "pricing-mgr-1", id, d.Version, 800, "corr-upd-2"), CancellationToken.None);
        Assert.False(reprice.Accepted); // desativada não aceita reprice
    }
}
