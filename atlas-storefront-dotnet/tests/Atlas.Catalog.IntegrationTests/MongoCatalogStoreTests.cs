using Atlas.Catalog.Application;
using Atlas.Catalog.Domain;
using Atlas.Catalog.Infrastructure;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Catalog.IntegrationTests;

/// <summary>Integração real: replica set local (EVD-001-006); banco por execução, drop no fim.</summary>
public sealed class MongoCatalogStoreTests : IAsyncLifetime
{
    private const string ConnStr = "mongodb://127.0.0.1:27017/?directConnection=true";
    private readonly string _dbName = $"atlas_cat_{Guid.NewGuid():N}";
    private MongoCatalogStore _store = default!;
    private IMongoDatabase _db = default!;

    public Task InitializeAsync()
    {
        _store = MongoCatalogStore.Create(ConnStr, _dbName);
        _db = new MongoClient(ConnStr).GetDatabase(_dbName);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _db.Client.DropDatabaseAsync(_dbName);

    private static UpsertDraftCommand Cmd(string requestId = "req-1") => new(
        RequestId: requestId,
        ActorSub: "catalog-editor-1",
        MarketId: "BR",
        Titles: [("pt-BR", "Tênis Atlas Runner"), ("en-US", "Atlas Runner Shoe")],
        SkuCodes: ["ATL-RUN-42"]);

    private async Task<int> OutboxCountAsync() =>
        (int)await _db.GetCollection<MongoCatalogStore.OutboxDoc>("catalog.outbox").CountDocumentsAsync(_ => true);

    [Fact]
    public async Task Upsert_idempotente_por_request_id_e_publicação_ponta_a_ponta_com_outbox()
    {
        var created = await _store.UpsertDraftInTransactionAsync(Cmd(), CancellationToken.None);
        Assert.Equal(UpsertDraftStatus.Created, created.Status);
        Assert.Equal(1, created.Version);

        var again = await _store.UpsertDraftInTransactionAsync(Cmd(), CancellationToken.None);
        Assert.Equal(UpsertDraftStatus.Updated, again.Status);
        Assert.Equal(created.ProductId, again.ProductId);

        // publish sem aprovação prévia é negado pelo DOMÍNIO (owner revalida)
        var direto = await _store.PublishInTransactionAsync(created.ProductId, created.Version, "approver-1", "BR", "corr-1", CancellationToken.None);
        Assert.False(direto.Accepted);
        Assert.Equal(0, await OutboxCountAsync());

        // fluxo correto: submit → publish com version esperada
        var submitted = await _store.SubmitForApprovalAsync(created.ProductId, created.Version, CancellationToken.None);
        Assert.True(submitted.Accepted);

        var publish = await _store.PublishInTransactionAsync(created.ProductId, created.Version + 1, "approver-1", "BR", "corr-2", CancellationToken.None);
        Assert.True(publish.Accepted);
        Assert.NotNull(publish.EventId);
        Assert.Equal(1, await OutboxCountAsync());

        // evento repetido/ordem invertida NÃO duplica outbox (idempotência da operação por version)
        var repetido = await _store.PublishInTransactionAsync(created.ProductId, created.Version + 1, "approver-1", "BR", "corr-3", CancellationToken.None);
        Assert.False(repetido.Accepted); // version já avançou → conflito, sem segundo evento
        Assert.Equal(1, await OutboxCountAsync());
    }

    [Fact]
    public async Task Publicação_com_conflito_de_versão_rejeita_sem_evento()
    {
        var created = await _store.UpsertDraftInTransactionAsync(Cmd("req-2"), CancellationToken.None);
        await _store.SubmitForApprovalAsync(created.ProductId, created.Version, CancellationToken.None);

        var stale = await _store.PublishInTransactionAsync(created.ProductId, expectedVersion: 99, "approver-1", "BR", "corr", CancellationToken.None);
        Assert.False(stale.Accepted);
        Assert.Contains("conflito", stale.Reason);
        Assert.Equal(0, await OutboxCountAsync());
    }

    [Fact]
    public async Task Publicar_sem_sku_é_negado_pelo_domínio()
    {
        var cmd = new UpsertDraftCommand("req-3", "editor", "BR", [("pt-BR", "Sem SKU")], []);
        var created = await _store.UpsertDraftInTransactionAsync(cmd, CancellationToken.None);
        await _store.SubmitForApprovalAsync(created.ProductId, created.Version, CancellationToken.None);

        var outcome = await _store.PublishInTransactionAsync(created.ProductId, created.Version + 1, "approver-1", "BR", "corr", CancellationToken.None);
        Assert.False(outcome.Accepted);
        Assert.Contains("SKU", outcome.Reason);
    }
}
