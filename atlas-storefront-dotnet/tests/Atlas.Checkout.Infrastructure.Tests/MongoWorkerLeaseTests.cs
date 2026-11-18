using Atlas.Checkout.Application;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Checkout.Infrastructure.Tests;

/// <summary>
/// REGRESSÃO cluster .51: o reconciliador parou para sempre após cada restart dos pods —
/// TryAcquireAsync nunca liberava lease vencida de OUTRO holder (o CAS de expiração
/// exigia Holder == solicitante). Lease travada = saga PendingReconciliation eterna.
/// Integração REAL: replica set local (mesmo padrão dos demais testes deste projeto);
/// db por execução (drop no fim).
/// </summary>
public sealed class MongoWorkerLeaseTests : IAsyncLifetime
{
    // Convenção do repo: mongo local sem auth. Override SOMENTE para teste (ex.: cluster
    // com auth via tunnel): ATLAS_TEST_MONGO_CONN="mongodb://user:pass@127.0.0.1:27017/
    // ?directConnection=true&replicaSet=rs0&authSource=admin". Default preserva o local.
    private static readonly string ConnStr =
        Environment.GetEnvironmentVariable("ATLAS_TEST_MONGO_CONN")
        ?? "mongodb://127.0.0.1:27017/?directConnection=true&replicaSet=rs0";
    private readonly string _dbName = $"atlas_lease_{Guid.NewGuid():N}";
    private IMongoDatabase _db = default!;

    public Task InitializeAsync()
    {
        _db = new MongoClient(ConnStr).GetDatabase(_dbName);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _db.Client.DropDatabaseAsync(_dbName);

    private MongoWorkerLease Leases() => new(_db);

    [Fact]
    public async Task Primeira_aquisicao_vence_e_segunda_eh_negada()
    {
        Assert.True(await Leases().TryAcquireAsync("k", "host-a", TimeSpan.FromMinutes(5), CancellationToken.None));
        Assert.False(await Leases().TryAcquireAsync("k", "host-b", TimeSpan.FromMinutes(5), CancellationToken.None));
    }

    [Fact]
    public async Task Mesmo_holder_renova_heartbeat()
    {
        Assert.True(await Leases().TryAcquireAsync("k", "host-a", TimeSpan.FromMinutes(5), CancellationToken.None));
        Assert.True(await Leases().TryAcquireAsync("k", "host-a", TimeSpan.FromMinutes(5), CancellationToken.None));
    }

    [Fact]
    public async Task Holder_diferente_assume_lease_vencida()
    {
        // holder antigo com TTL já vencido (simula crash do pod)
        Assert.True(await Leases().TryAcquireAsync("k", "host-morto", TimeSpan.FromMilliseconds(50), CancellationToken.None));
        await Task.Delay(150);

        Assert.True(await Leases().TryAcquireAsync("k", "host-vivo", TimeSpan.FromMinutes(5), CancellationToken.None));
        // o morto não retoma por cima do vivo dentro do TTL
        Assert.False(await Leases().TryAcquireAsync("k", "host-morto", TimeSpan.FromMinutes(5), CancellationToken.None));
    }
}
