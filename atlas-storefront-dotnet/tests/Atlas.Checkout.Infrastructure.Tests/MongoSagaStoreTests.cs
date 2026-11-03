using Atlas.Checkout.Domain;
using Atlas.Checkout.Infrastructure;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Checkout.Infrastructure.Tests;

/// <summary>
/// Integração REAL: round-trip da saga no replica set — fidelidade de reidratação
/// após reconciliação (UC003-T03): Confirmed + referência do PSP preservadas.
/// </summary>
public sealed class MongoSagaStoreTests : IAsyncLifetime
{
    private const string ConnStr = "mongodb://127.0.0.1:27017/?directConnection=true";
    private readonly string _dbName = $"atlas_saga_{Guid.NewGuid():N}";
    private Atlas.Checkout.Infrastructure.MongoSagaStore _store = default!;
    private IMongoDatabase _db = default!;

    public Task InitializeAsync()
    {
        _store = Atlas.Checkout.Infrastructure.MongoSagaStore.Create(ConnStr, _dbName);
        _db = new MongoClient(ConnStr).GetDatabase(_dbName);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _db.Client.DropDatabaseAsync(_dbName);

    [Fact]
    public async Task Saga_reconciliada_reidrata_Confirmed_com_referência_do_PSP()
    {
        var now = DateTimeOffset.UtcNow;
        var saga = CheckoutSaga.Start("idem-reconciled", "customer-1", 2500, "BRL", now);
        saga.MarkStockReserved("res-1", now);
        saga.MarkOrderPlaced("d4a0e4a0-0000-0000-0000-000000000001", now);
        saga.MarkPendingReconciliation("psp-unknown", now);
        await _store.SaveAsync(saga, CancellationToken.None);

        // ciclo do reconciliador: consulta PSP → pago → Confirmed com referência
        var pending = await _store.FindByStateAsync(CheckoutSagaState.PendingReconciliation, CancellationToken.None);
        var target = Assert.Single(pending);
        target.MarkReconciliationPaid("pi-reconciled", "pr_abc123def456", now.AddSeconds(5));
        await _store.SaveAsync(target, CancellationToken.None);

        // leitura posterior (rota, auditor, reconciliador): estado e referência FIEIS ao doc
        var rehydrated = await _store.FindByIdempotencyKeyAsync("idem-reconciled", CancellationToken.None);
        Assert.NotNull(rehydrated);
        Assert.Equal(CheckoutSagaState.Confirmed, rehydrated.State);
        Assert.Equal("pi-reconciled", rehydrated.PaymentIntentId);
        Assert.Equal("pr_abc123def456", rehydrated.ProviderReference);
    }

    [Fact]
    public async Task Saga_pendente_reidrata_PendingReconciliation_para_o_reconciliador()
    {
        var now = DateTimeOffset.UtcNow;
        var saga = CheckoutSaga.Start("idem-pending", "customer-1", 9900, "BRL", now);
        saga.MarkStockReserved("res-2", now);
        saga.MarkOrderPlaced("d4a0e4a0-0000-0000-0000-000000000002", now);
        saga.MarkPendingReconciliation("psp-unknown", now);
        await _store.SaveAsync(saga, CancellationToken.None);

        var pending = await _store.FindByStateAsync(CheckoutSagaState.PendingReconciliation, CancellationToken.None);
        var target = Assert.Single(pending);
        Assert.Equal(CheckoutSagaState.PendingReconciliation, target.State);

        // o reconciliador resolve: pago → Confirmed direto (sem passar por PaymentAuthorized)
        Assert.True(target.MarkReconciliationPaid("pi-x", "pr_x", DateTimeOffset.UtcNow));
        Assert.Equal(CheckoutSagaState.Confirmed, target.State);
    }
}
