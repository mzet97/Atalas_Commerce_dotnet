using Atlas.Checkout.Application;
using Atlas.Checkout.Domain;
using Xunit;

namespace Atlas.Checkout.Application.Tests;

/// <summary>
/// UC003-T03 completo: reconciliador consulta o PSP e resolve — pago → confirma;
/// não pago → compensa; indeterminado → mantém pendente. Com fakes dos ports.
/// </summary>
public sealed class CheckoutReconcilerTests
{
    private sealed class FakeSagaStore(List<CheckoutSaga> sagas) : ISagaStore
    {
        public Task SaveAsync(CheckoutSaga saga, CancellationToken ct) => Task.CompletedTask;
        public Task<CheckoutSaga?> FindByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<CheckoutSaga?>(null);
        public Task<IReadOnlyList<CheckoutSaga>> FindByStateAsync(CheckoutSagaState state, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CheckoutSaga>>(sagas.Where(s => s.State == state).ToList());
        public List<CheckoutSaga> All => sagas;
    }

    private sealed class FakeReconciliation : IOrderReconciliationStore, IPaymentReconciliationStore
    {
        public List<(Guid OrderId, string Ref)> Confirmed { get; } = [];
        public List<(Guid OrderId, string Reason)> Cancelled { get; } = [];
        public List<(string Key, string Ref)> Authorized { get; } = [];
        public List<string> Voided { get; } = [];
        public List<string> Captured { get; } = [];
        public List<string> Released { get; } = [];

        public Task<bool> ConfirmOrderAsync(Guid orderId, string paymentReference, CancellationToken ct)
        { Confirmed.Add((orderId, paymentReference)); return Task.FromResult(true); }
        public Task<bool> CancelOrderAsync(Guid orderId, string reason, CancellationToken ct)
        { Cancelled.Add((orderId, reason)); return Task.FromResult(true); }
        public Task<bool> AuthorizeIntentAsync(string key, string providerReference, CancellationToken ct)
        { Authorized.Add((key, providerReference)); return Task.FromResult(true); }
        public Task<bool> CaptureIntentAsync(string key, CancellationToken ct)
        { Captured.Add(key); return Task.FromResult(true); }
        public Task<bool> VoidIntentAsync(string key, CancellationToken ct)
        { Voided.Add(key); return Task.FromResult(true); }
    }

    private sealed class FakeRelease : IInventoryReservePort
    {
        public List<string> Released { get; } = [];
        public Task<string?> ReserveAsync(string checkoutId, IReadOnlyList<(string SkuId, int Quantity)> lines, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<bool> ReleaseAsync(string reservationId, string reason, CancellationToken ct) { Released.Add(reservationId); return Task.FromResult(true); }
    }

    private sealed class FakePspReconciliation(ReconciliationOutcome outcome) : IPspReconciliationPort
    {
        public Task<(ReconciliationOutcome Outcome, string? ProviderReference)> QueryAuthorizationAsync(
            string idempotencyKey, CancellationToken ct) =>
            Task.FromResult((outcome, outcome == ReconciliationOutcome.Paid ? $"pr_{idempotencyKey}" : null));
    }

    private static CheckoutSaga PendingSaga(string key, string orderId, string reservationId)
    {
        var saga = CheckoutSaga.Start(key, "c1", 5000, "BRL", DateTimeOffset.UtcNow);
        saga.MarkStockReserved(reservationId, DateTimeOffset.UtcNow);
        saga.MarkOrderPlaced(orderId, DateTimeOffset.UtcNow);
        saga.MarkPendingReconciliation("psp-unknown", DateTimeOffset.UtcNow);
        return saga;
    }

    [Fact]
    public async Task Pago_confirma_pedido_intent_e_saga()
    {
        var saga = PendingSaga("recon-paid", "11111111-1111-1111-1111-111111111111", "res-1");
        var sagaStore = new FakeSagaStore([saga]);
        var recon = new FakeReconciliation();
        var release = new FakeRelease();
        var reconciler = new CheckoutReconciler(sagaStore, recon, recon, release, new FakePspReconciliation(ReconciliationOutcome.Paid));

        var resolved = await reconciler.ReconcileBatchAsync(CancellationToken.None);

        Assert.Equal(1, resolved);
        Assert.Single(recon.Confirmed);
        Assert.Equal(CheckoutSagaState.Confirmed, sagaStore.All[0].State);
        Assert.Empty(release.Released); // pago → reserva NÃO liberada
    }

    [Fact]
    public async Task Não_pago_libera_reserva_cancela_e_compensa()
    {
        var saga = PendingSaga("recon-nopaid", "22222222-2222-2222-2222-222222222222", "res-2");
        var sagaStore = new FakeSagaStore([saga]);
        var recon = new FakeReconciliation();
        var release = new FakeRelease();
        var reconciler = new CheckoutReconciler(sagaStore, recon, recon, release, new FakePspReconciliation(ReconciliationOutcome.NotAuthorized));

        var resolved = await reconciler.ReconcileBatchAsync(CancellationToken.None);

        Assert.Equal(1, resolved);
        Assert.Single(recon.Cancelled);
        Assert.Single(recon.Voided);
        Assert.Single(release.Released); // reserva liberada
        Assert.Equal(CheckoutSagaState.Compensated, sagaStore.All[0].State);
    }

    [Fact]
    public async Task Indeterminado_mantém_pendente()
    {
        var saga = PendingSaga("recon-indet", "33333333-3333-3333-3333-333333333333", "res-3");
        var sagaStore = new FakeSagaStore([saga]);
        var recon = new FakeReconciliation();
        var release = new FakeRelease();
        var reconciler = new CheckoutReconciler(sagaStore, recon, recon, release, new FakePspReconciliation(ReconciliationOutcome.Indeterminate));

        var resolved = await reconciler.ReconcileBatchAsync(CancellationToken.None);

        Assert.Equal(0, resolved);
        Assert.Empty(recon.Confirmed);
        Assert.Empty(recon.Cancelled);
        Assert.Equal(CheckoutSagaState.PendingReconciliation, sagaStore.All[0].State);
    }

    // ---- Recuperação de sagas crashadas (TEST-003-008 — restart/retry) ----

    private static DateTimeOffset Old(DateTimeOffset now) => now.AddMinutes(-10);

    [Fact]
    public async Task Crashada_após_autorização_PSP_pago_confirma_a_compra()
    {
        // crash DEPOIS do PSP autorizar e ANTES de persistir: saga ficou OrderPlaced,
        // PSP TEM a autorização → recuperação confirma (a compra não se perde)
        var now = DateTimeOffset.UtcNow;
        var saga = CheckoutSaga.Start("crash-paid", "c1", 5000, "BRL", Old(now));
        saga.MarkStockReserved("res-crash-1", Old(now));
        saga.MarkOrderPlaced("44444444-4444-4444-4444-444444444444", Old(now));
        var sagaStore = new FakeSagaStore([saga]);
        var recon = new FakeReconciliation();
        var release = new FakeRelease();
        var reconciler = new CheckoutReconciler(sagaStore, recon, recon, release, new FakePspReconciliation(ReconciliationOutcome.Paid));

        var recovered = await reconciler.RecoverStuckAsync(TimeSpan.FromMinutes(5), now, CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal(CheckoutSagaState.Confirmed, sagaStore.All[0].State);
        Assert.Single(recon.Confirmed);            // order confirmada
        Assert.Single(recon.Authorized);           // intent autorizada
        Assert.Empty(release.Released);            // reserva NÃO liberada
    }

    [Fact]
    public async Task Crashada_antes_do_PSP_compensa_liberando_reserva()
    {
        // crash DEPOIS da reserva e ANTES do PSP: OrderPlaced sem registro no PSP → compensa
        var now = DateTimeOffset.UtcNow;
        var saga = CheckoutSaga.Start("crash-nopsp", "c1", 5000, "BRL", Old(now));
        saga.MarkStockReserved("res-crash-2", Old(now));
        saga.MarkOrderPlaced("55555555-5555-5555-5555-555555555555", Old(now));
        var sagaStore = new FakeSagaStore([saga]);
        var recon = new FakeReconciliation();
        var release = new FakeRelease();
        var reconciler = new CheckoutReconciler(sagaStore, recon, recon, release, new FakePspReconciliation(ReconciliationOutcome.NotAuthorized));

        var recovered = await reconciler.RecoverStuckAsync(TimeSpan.FromMinutes(5), now, CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal(CheckoutSagaState.Compensated, sagaStore.All[0].State);
        Assert.Contains("res-crash-2", release.Released); // reserva devolvida
        Assert.Single(recon.Cancelled);                    // order cancelada
        Assert.Empty(recon.Confirmed);
    }

    [Fact]
    public async Task Crashada_só_com_reserva_compensa_sem_consultar_PSP()
    {
        // crash entre reserva e criação do pedido: nada chegou ao PSP → compensa direto
        var now = DateTimeOffset.UtcNow;
        var saga = CheckoutSaga.Start("crash-stock", "c1", 5000, "BRL", Old(now));
        saga.MarkStockReserved("res-crash-3", Old(now));
        var sagaStore = new FakeSagaStore([saga]);
        var recon = new FakeReconciliation();
        var release = new FakeRelease();
        var reconciler = new CheckoutReconciler(sagaStore, recon, recon, release, new FakePspReconciliation(ReconciliationOutcome.Paid));

        var recovered = await reconciler.RecoverStuckAsync(TimeSpan.FromMinutes(5), now, CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal(CheckoutSagaState.Compensated, sagaStore.All[0].State);
        Assert.Contains("res-crash-3", release.Released);
        Assert.Empty(recon.Confirmed);             // sem order e sem autorização: nada confirmado
        Assert.Empty(recon.Authorized);
    }

    [Fact]
    public async Task Crashada_com_PSP_indisponível_aguarda_próxima_rodada()
    {
        var now = DateTimeOffset.UtcNow;
        var saga = CheckoutSaga.Start("crash-indet", "c1", 5000, "BRL", Old(now));
        saga.MarkStockReserved("res-crash-4", Old(now));
        saga.MarkOrderPlaced("66666666-6666-6666-6666-666666666666", Old(now));
        var sagaStore = new FakeSagaStore([saga]);
        var recon = new FakeReconciliation();
        var release = new FakeRelease();
        var reconciler = new CheckoutReconciler(sagaStore, recon, recon, release, new FakePspReconciliation(ReconciliationOutcome.Indeterminate));

        var recovered = await reconciler.RecoverStuckAsync(TimeSpan.FromMinutes(5), now, CancellationToken.None);

        Assert.Equal(0, recovered);
        Assert.Equal(CheckoutSagaState.OrderPlaced, sagaStore.All[0].State); // intacta, decide depois
        Assert.Empty(release.Released);
        Assert.Empty(recon.Confirmed);
    }

    [Fact]
    public async Task Saga_recente_não_é_tocada_pela_recuperação()
    {
        // dentro do SLA do fluxo normal (request em curso) — recovery NÃO interfere
        var now = DateTimeOffset.UtcNow;
        var saga = CheckoutSaga.Start("fresh", "c1", 5000, "BRL", now.AddSeconds(-2));
        saga.MarkStockReserved("res-fresh", now.AddSeconds(-1));
        var sagaStore = new FakeSagaStore([saga]);
        var recon = new FakeReconciliation();
        var release = new FakeRelease();
        var reconciler = new CheckoutReconciler(sagaStore, recon, recon, release, new FakePspReconciliation(ReconciliationOutcome.NotAuthorized));

        var recovered = await reconciler.RecoverStuckAsync(TimeSpan.FromMinutes(5), now, CancellationToken.None);

        Assert.Equal(0, recovered);
        Assert.Equal(CheckoutSagaState.StockReserved, sagaStore.All[0].State);
        Assert.Empty(release.Released);
    }
}
