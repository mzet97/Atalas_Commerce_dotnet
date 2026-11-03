using Atlas.Inventory.Application;
using Atlas.Inventory.Domain;
using Atlas.Inventory.Infrastructure;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Inventory.IntegrationTests;

/// <summary>
/// Integração REAL contra o replica set local (mongodb://127.0.0.1:27017/?directConnection=true —
/// EVD-001-006). Banco dedicado por execução (drop no fim). Transações exigem replica set: ✔.
/// </summary>
public sealed class MongoInventoryStoreTests : IAsyncLifetime
{
    private readonly string _dbName = $"atlas_it_{Guid.NewGuid():N}";
    private MongoInventoryStore _store = default!;
    private IMongoDatabase _db = default!;
    private static readonly string ConnStr = "mongodb://127.0.0.1:27017/?directConnection=true";

    public async Task InitializeAsync()
    {
        _store = MongoInventoryStore.Create(ConnStr, _dbName);
        _db = new MongoClient(ConnStr).GetDatabase(_dbName);
        await SeedPositionAsync(wh: Guid.NewGuid(), "sku-last", onHand: 1);
        await SeedPositionAsync(_wh5 = Guid.NewGuid(), "sku-5", onHand: 5);
        await SeedPositionAsync(_whMulti = Guid.NewGuid(), "sku-m1", onHand: 5);
        await SeedPositionAsync(_whMulti, "sku-m2", onHand: 0);
    }

    public async Task DisposeAsync()
    {
        await new MongoClient(ConnStr).GetDatabase(_dbName).Client
            .DropDatabaseAsync(_dbName);
    }

    private Guid _wh5, _whMulti;

    private async Task SeedPositionAsync(Guid wh, string sku, int onHand, int safetyStock = 0)
    {
        var key = StockKey.Create(wh, sku).ToString();
        await _db.GetCollection<MongoInventoryStore.PositionDoc>("inventory.positions")
            .UpdateOneAsync(
                p => p.Id == key,
                Builders<MongoInventoryStore.PositionDoc>.Update
                    .Set(p => p.OnHand, onHand)
                    .Set(p => p.SafetyStock, safetyStock)
                    .Set(p => p.Reserved, 0)
                    .Set(p => p.Version, 1)
                    .SetOnInsert(p => p.Id, key),
                new UpdateOptions { IsUpsert = true });
    }

    private async Task<((int onHand, int reserved, long version) pos, int outbox, int movements, int reservations)> SnapshotAsync(Guid wh, string sku)
    {
        var id = StockKey.Create(wh, sku).ToString();
        var pos = await _db.GetCollection<MongoInventoryStore.PositionDoc>("inventory.positions").Find(p => p.Id == id).SingleAsync();
        var outbox = (int)await _db.GetCollection<MongoInventoryStore.OutboxDoc>("inventory.outbox").CountDocumentsAsync(_ => true);
        var movements = (int)await _db.GetCollection<MongoInventoryStore.MovementDoc>("inventory.movements").CountDocumentsAsync(m => m.PositionKey == id);
        var reservations = (int)await _db.GetCollection<MongoInventoryStore.ReservationDoc>("inventory.reservations").CountDocumentsAsync(_ => true);
        return ((pos.OnHand, pos.Reserved, pos.Version), outbox, movements, reservations);
    }

    // UC003-T01 / INV-STOCK-001 — a prova real, com CAS no MongoDB de verdade
    [Fact]
    public async Task Última_unidade_sob_10_reservas_concorrentes_permite_exatamente_uma()
    {
        var wh = (await _db.GetCollection<MongoInventoryStore.PositionDoc>("inventory.positions")
            .Find(p => p.Id.EndsWith("sku-last")).SingleAsync()).Id.Split(':')[0];
        var warehouse = Guid.Parse(wh);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            Task.Run(async () => await _store.ReserveInTransactionAsync(
                Reservation.Create($"race-{i}", [new(StockKey.Create(warehouse, "sku-last"), 1)],
                    DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)),
                [new StockKey(warehouse, "sku-last")], $"corr-{i}", "race-actor", CancellationToken.None))));

        foreach (var (r, idx) in results.Select((v, i) => (v, i)))
            if (r is null) throw new Xunit.Sdk.XunitException($"elemento nulo em [{idx}]");
        var granted = results.Count(r => r.Status == ReserveStockStatus.Granted);
        var denied = results.Count(r => r.Status == ReserveStockStatus.OutOfStock);
        var others = results.Length - granted - denied;
        Assert.True(granted == 1 && denied == 9, $"granted={granted} denied={denied} outros={others} :: {string.Join(" | ", results.Select(r => $"{r?.Status}:{string.Join(",", r?.DeniedSkus ?? [])}"))}");

        var snap = await SnapshotAsync(warehouse, "sku-last");
        Assert.Equal(1, snap.pos.onHand);
        Assert.Equal(1, snap.pos.reserved);            // nunca negativo / nunca oversell
        Assert.Equal(2, snap.pos.version);             // 1 update atômico
        Assert.Equal(1, snap.outbox);                  // 1 evento Stock.ReservedV1
        Assert.Equal(1, snap.reservations);
        Assert.Equal(1, snap.movements);               // ledger do vencedor
    }

    [Fact]
    public async Task Mesma_idempotency_key_retorna_a_mesma_reserva_sem_reduzir_duas_vezes()
    {
        var first = await _store.ReserveInTransactionAsync(
            Reservation.Create("idem-1", [new(StockKey.Create(_wh5, "sku-5"), 2)], DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)),
            [new StockKey(_wh5, "sku-5")], "corr-a", "actor", CancellationToken.None);
        var second = await _store.ReserveInTransactionAsync(
            Reservation.Create("idem-1", [new(StockKey.Create(_wh5, "sku-5"), 2)], DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)),
            [new StockKey(_wh5, "sku-5")], "corr-b", "actor", CancellationToken.None);

        Assert.Equal(ReserveStockStatus.Granted, first.Status);
        Assert.Equal(ReserveStockStatus.DuplicateKey, second.Status);
        Assert.Equal(first.Reservation!.Id, second.Reservation!.Id);   // MESMA reserva

        var snap = await SnapshotAsync(_wh5, "sku-5");
        Assert.Equal(2, snap.pos.reserved);                             // reduzido uma única vez
        Assert.Equal(1, snap.outbox);
    }

    [Fact]
    public async Task Mesma_chave_em_coração_elege_um_vencedor_pelo_índice_único()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            Task.Run(async () => await _store.ReserveInTransactionAsync(
                Reservation.Create("same-key-race", [new(StockKey.Create(_wh5, "sku-5"), 1)], DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)),
                [new StockKey(_wh5, "sku-5")], "corr-race", "race", CancellationToken.None))));

        Assert.Equal(1, results.Count(r => r.Status == ReserveStockStatus.Granted));
        Assert.Equal(5, results.Count(r => r.Status == ReserveStockStatus.DuplicateKey));
        Assert.Single(results.Select(r => r.Reservation!.Id).Distinct()); // todos com a MESMA reserva
    }

    [Fact]
    public async Task Multi_linha_all_or_nothing_transação_abortada_não_persiste_nada()
    {
        var result = await _store.ReserveInTransactionAsync(
            Reservation.Create("multi-1",
                [new(StockKey.Create(_whMulti, "sku-m1"), 3), new(StockKey.Create(_whMulti, "sku-m2"), 1)],
                DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)),
            [new StockKey(_whMulti, "sku-m1"), new StockKey(_whMulti, "sku-m2")],
            "corr-multi", "actor", CancellationToken.None);

        Assert.Equal(ReserveStockStatus.OutOfStock, result.Status);
        Assert.Contains("sku-m2", result.DeniedSkus);

        var m1 = await SnapshotAsync(_whMulti, "sku-m1");
        Assert.Equal(0, m1.pos.reserved);      // linha 1 bem-sucedida foi DESFEITA (abort)
        Assert.Equal(0, m1.reservations);
        Assert.Equal(0, m1.outbox);
    }

    [Fact]
    public async Task Confirmar_e_liberar_devolvem_saldo_com_ledger_e_evento()
    {
        var reserved = await _store.ReserveInTransactionAsync(
            Reservation.Create("confirm-1", [new(StockKey.Create(_wh5, "sku-5"), 2)], DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)),
            [new StockKey(_wh5, "sku-5")], "corr-c", "actor", CancellationToken.None);
        var id = reserved.Reservation!.Id;

        Assert.True(await _store.ConfirmReservationAsync(id, "order-99", CancellationToken.None));

        var released = await _store.ReleaseReservationAsync(id, "cancelamento pós-confirmação", "corr-c", "actor", CancellationToken.None);
        Assert.True(released);

        var snap = await SnapshotAsync(_wh5, "sku-5");
        Assert.Equal(0, snap.pos.reserved);                       // saldo devolvido
        Assert.Equal(2, snap.movements);                          // reserve + release no ledger
        Assert.Equal(2, snap.outbox);                             // Stock.ReservedV1 + Reservation.ReleasedV1
    }
}
