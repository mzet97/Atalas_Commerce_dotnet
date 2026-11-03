using Atlas.Checkout.Application;
using Atlas.Checkout.Domain;
using Atlas.Orders.Domain;
using Atlas.Payments.Domain;
using Xunit;

namespace Atlas.Checkout.Application.Tests;

/// <summary>
/// A saga orquestrada com PORTS substituíveis — cenários T01..T05 do UC-003 e COM-08/INV-QUOTE-001,
/// sem containers (os owners reais são provados nas suítes de integração deles).
/// </summary>
public sealed class CheckoutOrchestratorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed class FakeQuoteStore : IQuoteStore
    {
        public Guid DefaultId { get; } = Guid.NewGuid();
        private readonly Dictionary<Guid, PriceQuote> _quotes = [];
        public void Seed(TimeSpan ttl, DateTimeOffset? issuedAtUtc = null)
        {
            var issuedAt = issuedAtUtc ?? DateTimeOffset.UtcNow;
            _quotes[DefaultId] = PriceQuote.Issue(
                "cart-test", [new PriceQuote.QuoteLine("sku-1", 2, 2500, 5000)],
                "BRL", issuedAt, ttl);
        }
        public Task<PriceQuote?> FindByIdAsync(Guid quoteId, CancellationToken ct) =>
            Task.FromResult(_quotes.TryGetValue(quoteId, out var q) ? q : null);
    }

    private static SubmitCheckoutCommand Cmd(string key, Guid quoteId, long total = 5000) => new(
        IdempotencyKey: key,
        CustomerId: "customer-1",
        QuoteId: quoteId,
        Lines: [new CheckoutLineDto("sku-1", 2, 2500)],
        TotalMinor: total,
        Currency: "BRL",
        Shipping: new OrderShippingAddressDto("BR", "01310-100", "Av. Paulista 1000", "SP"));

    private sealed class FakeInventory(int stock, List<string> released) : IInventoryReservePort
    {
        public int ReserveCalls { get; private set; }
        private int _available = stock;
        public Task<string?> ReserveAsync(string checkoutId, IReadOnlyList<(string SkuId, int Quantity)> lines, CancellationToken ct)
        {
            ReserveCalls++;
            var qty = lines.Sum(l => l.Quantity);
            if (_available < qty) return Task.FromResult<string?>(null); // CAS: sem saldo, sem reserva
            _available -= qty;
            return Task.FromResult<string?>($"res-{ReserveCalls}");
        }
        public Task<bool> ReleaseAsync(string reservationId, string reason, CancellationToken ct)
        {
            released.Add(reservationId);
            return Task.FromResult(true);
        }
    }

    private sealed class FakePsp(string scenario) : IPspPort
    {
        public Task<(PspOutcome Outcome, string? ProviderReference, string? Reason)> AuthorizeAsync(
            string orderReference, long amountMinor, string currency, CancellationToken ct) =>
            Task.FromResult(scenario switch
            {
                "APPROVED" => (PspOutcome.Authorized, $"pr_{orderReference[..8]}", (string?)null),
                "DECLINED" => (PspOutcome.Declined, null, (string?)"cartão recusado (simulado)"),
                _ => (PspOutcome.Unknown, null, (string?)"timeout"),
            });
    }

    private sealed class SagaStoreAdapter(InMemoryStore<CheckoutSaga> inner) : ISagaStore
    {
        public Task SaveAsync(CheckoutSaga saga, CancellationToken ct) => inner.SaveAsync(saga, ct);
        public Task<CheckoutSaga?> FindByIdempotencyKeyAsync(string key, CancellationToken ct) => inner.FindByIdempotencyKeyAsync(key, ct);
        public Task<IReadOnlyList<CheckoutSaga>> FindByStateAsync(CheckoutSagaState state, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CheckoutSaga>>(inner.Items.Values.Where(s => s.State == state).ToList());
    }

    private sealed class OrdersStoreAdapter(InMemoryStore<Order> inner) : IOrdersStore
    {
        public Task SaveAsync(Order order, CancellationToken ct) => inner.SaveAsync(order, ct);
        public Task<bool> MarkReconciliationRequiredAsync(Guid orderId, string reason, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> ConfirmOrderAsync(Guid orderId, string paymentReference, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> MarkPaymentFailedAsync(Guid orderId, string reason, CancellationToken ct)
        {
            foreach (var o in inner.Items.Values.Where(o => o.Id.Value == orderId))
                o.TryMarkPaymentFailed(reason, DateTimeOffset.UtcNow);
            return Task.FromResult(true);
        }
    }

    private sealed class PaymentsStoreAdapter(InMemoryStore<PaymentIntent> inner) : IPaymentsStore
    {
        public Task SaveAsync(PaymentIntent intent, CancellationToken ct) => inner.SaveAsync(intent, ct);
        public Task<PaymentIntent?> FindByPspKeyAsync(string key, CancellationToken ct) => inner.FindByIdempotencyKeyAsync(key, ct);
    }

    private sealed class InMemoryStore<T> where T : class
    {
        public Dictionary<string, T> Items { get; } = [];
        public int Saves { get; private set; }
        public Task SaveAsync(T item, CancellationToken ct)
        {
            var key = item switch
            {
                CheckoutSaga s => s.IdempotencyKey,
                Order o => o.Id.Value.ToString(),
                PaymentIntent pi => pi.PspIdempotencyKey,
                _ => throw new NotSupportedException(),
            };
            Items[key] = item;
            Saves++;
            return Task.CompletedTask;
        }
        public Task<T?> FindByIdempotencyKeyAsync(string key, CancellationToken ct) =>
            Task.FromResult(Items.TryGetValue(key, out var v) ? v : null);
        public Task<IReadOnlyList<T>> FindByStateAsync(T state, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<T>>(Items.Values.Where(v => EqualityComparer<T>.Default.Equals(v, state)).ToList());
    }

    private sealed record Fixture(
        CheckoutOrchestrator Orchestrator,
        InMemoryStore<CheckoutSaga> Sagas,
        InMemoryStore<Order> Orders,
        InMemoryStore<PaymentIntent> Payments,
        FakeQuoteStore Quotes,
        FakeInventory Inventory,
        List<string> Released)
    {
        public Guid QuoteId => Quotes.DefaultId;
    }

    private static Fixture Build(int stock, string pspScenario, FakeInventory? sharedInventory = null)
    {
        var sagas = new InMemoryStore<CheckoutSaga>();
        var orders = new InMemoryStore<Order>();
        var payments = new InMemoryStore<PaymentIntent>();
        var quotes = new FakeQuoteStore();
        quotes.Seed(TimeSpan.FromMinutes(10));
        var released = new List<string>();
        var inventory = sharedInventory ?? new FakeInventory(stock, released);
        var orchestrator = new CheckoutOrchestrator(
            new SagaStoreAdapter(sagas), new OrdersStoreAdapter(orders),
            new PaymentsStoreAdapter(payments), quotes, inventory, new FakePsp(pspScenario));
        return new(orchestrator, sagas, orders, payments, quotes, inventory, released);
    }

    // T01: contenda pela última unidade — exatamente 1 confirmado, 1 rejeitado
    [Fact]
    public async Task Contenda_última_unidade_exatamente_um_confirmado()
    {
        var sharedInventory = new FakeInventory(stock: 2, []);
        var fixtureA = Build(stock: 2, "APPROVED", sharedInventory);
        var fixtureB = Build(0, "APPROVED", sharedInventory);
        var rA = await fixtureA.Orchestrator.SubmitAsync(Cmd("chk-A", fixtureA.QuoteId), CancellationToken.None);
        var rB = await fixtureB.Orchestrator.SubmitAsync(Cmd("chk-B", fixtureB.QuoteId), CancellationToken.None);

        Assert.Equal(1, new[] { rA, rB }.Count(r => r.Status == CheckoutResultStatus.Confirmed));
        Assert.Equal(1, new[] { rA, rB }.Count(r => r.Status == CheckoutResultStatus.Rejected));
        Assert.Equal("OUT_OF_STOCK", (rA.Status == CheckoutResultStatus.Rejected ? rA : rB).Reason);
    }

    // T02: mesma idempotency key → mesmo resultado, reserva não repetida
    [Fact]
    public async Task Mesma_idempotency_key_retorna_mesmo_resultado()
    {
        var f = Build(stock: 5, "APPROVED");
        var first = await f.Orchestrator.SubmitAsync(Cmd("chk-idem", f.QuoteId), CancellationToken.None);
        var second = await f.Orchestrator.SubmitAsync(Cmd("chk-idem", f.QuoteId), CancellationToken.None);

        Assert.Equal(CheckoutResultStatus.Confirmed, first.Status);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.OrderId, second.OrderId);
        Assert.Equal(1, f.Inventory.ReserveCalls);
    }

    // T03: PSP UNKNOWN → PENDING (reconciliação), nunca sucesso presumido
    [Fact]
    public async Task Timeout_vira_Pending_com_reconciliação()
    {
        var f = Build(stock: 5, "UNKNOWN");
        var result = await f.Orchestrator.SubmitAsync(Cmd("chk-to", f.QuoteId), CancellationToken.None);

        Assert.Equal(CheckoutResultStatus.Pending, result.Status);
        var saga = await f.Sagas.FindByIdempotencyKeyAsync("chk-to", CancellationToken.None);
        Assert.Equal(CheckoutSagaState.PendingReconciliation, saga!.State);
        var order = f.Orders.Items[saga.OrderId!];
        Assert.Equal(OrderState.ReconciliationRequired, order.State);
        Assert.Equal(PaymentIntentState.Unknown, f.Payments.Items["chk-to"].State);
    }

    // DECLINED → compensação completa: reserva liberada, saga Compensated
    [Fact]
    public async Task Declined_compensa_liberando_reserva()
    {
        var f = Build(stock: 5, "DECLINED");
        var result = await f.Orchestrator.SubmitAsync(Cmd("chk-dec", f.QuoteId), CancellationToken.None);

        Assert.Equal(CheckoutResultStatus.Rejected, result.Status);
        Assert.Single(f.Released);
        var saga = await f.Sagas.FindByIdempotencyKeyAsync("chk-dec", CancellationToken.None);
        Assert.Equal(CheckoutSagaState.Compensated, saga!.State);
        var order = f.Orders.Items[saga.OrderId!];
        Assert.Equal(OrderState.PaymentFailed, order.State); // TEST-003-007: sem order órfã
    }

    // sem saldo → rejeita antes de criar pedido
    [Fact]
    public async Task Sem_saldo_rejeita_antes_de_criar_pedido()
    {
        var f = Build(stock: 0, "APPROVED");
        var result = await f.Orchestrator.SubmitAsync(Cmd("chk-none", f.QuoteId), CancellationToken.None);

        Assert.Equal(CheckoutResultStatus.Rejected, result.Status);
        Assert.Equal(1, f.Inventory.ReserveCalls);
        Assert.Empty(f.Orders.Items);
        var saga = await f.Sagas.FindByIdempotencyKeyAsync("chk-none", CancellationToken.None);
        Assert.Equal(CheckoutSagaState.Compensated, saga!.State);
    }

    // fluxo feliz completo
    [Fact]
    public async Task Fluxo_feliz_confirma_com_pedido_e_pagamento_persistidos()
    {
        var f = Build(stock: 5, "APPROVED");
        var result = await f.Orchestrator.SubmitAsync(Cmd("chk-ok", f.QuoteId), CancellationToken.None);

        Assert.Equal(CheckoutResultStatus.Confirmed, result.Status);
        var saga = await f.Sagas.FindByIdempotencyKeyAsync("chk-ok", CancellationToken.None);
        Assert.Equal(CheckoutSagaState.Confirmed, saga!.State);
        var order = f.Orders.Items[saga.OrderId!];
        Assert.Equal(OrderState.Confirmed, order.State);
        Assert.Equal(5000, order.Total.AmountMinor);
        Assert.Equal(PaymentIntentState.Captured, f.Payments.Items["chk-ok"].State);
        Assert.Empty(f.Released);
    }

    // COM-08/INV-QUOTE-001: quote expirada → rejeita ANTES de reservar (efeito colateral zero)
    [Fact]
    public async Task Quote_expirada_rejeita_antes_de_reservar()
    {
        var f = Build(stock: 5, "APPROVED");
        f.Quotes.Seed(TimeSpan.FromMinutes(10), DateTimeOffset.UtcNow.AddMinutes(-11)); // emitida há 11 min, TTL 10

        var result = await f.Orchestrator.SubmitAsync(Cmd("chk-exp", f.QuoteId), CancellationToken.None);

        Assert.Equal(CheckoutResultStatus.Rejected, result.Status);
        Assert.Contains("expirada", result.Reason, StringComparison.Ordinal);
        Assert.Equal(0, f.Inventory.ReserveCalls); // zero efeito colateral: sem reserva
        Assert.Empty(f.Orders.Items);              // sem pedido
        Assert.Empty(f.Sagas.Items);               // sem saga iniciada
    }

    // COM-08/INV-QUOTE-001: total divergente da cotação congelada → rejeita
    [Fact]
    public async Task Total_diverge_da_cotacao_rejeita()
    {
        var f = Build(stock: 5, "APPROVED");
        var result = await f.Orchestrator.SubmitAsync(Cmd("chk-div", f.QuoteId, total: 4999), CancellationToken.None);

        Assert.Equal(CheckoutResultStatus.Rejected, result.Status);
        Assert.Contains("diverge", result.Reason, StringComparison.Ordinal);
        Assert.Equal(0, f.Inventory.ReserveCalls);
        Assert.Empty(f.Orders.Items);
    }

    // COM-08/INV-QUOTE-001: quote inexistente → rejeita com re-cotação obrigatória
    [Fact]
    public async Task Quote_inexistente_rejeita_sem_reservar()
    {
        var f = Build(stock: 5, "APPROVED");
        var result = await f.Orchestrator.SubmitAsync(Cmd("chk-q404", Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(CheckoutResultStatus.Rejected, result.Status);
        Assert.Contains("re-cotação", result.Reason, StringComparison.Ordinal);
        Assert.Equal(0, f.Inventory.ReserveCalls);
        Assert.Empty(f.Sagas.Items);
    }
}
