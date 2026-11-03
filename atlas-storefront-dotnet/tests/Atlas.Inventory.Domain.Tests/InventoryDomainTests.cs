using Atlas.Inventory.Domain;
using Xunit;

namespace Atlas.Inventory.Domain.Tests;

public class StockPositionTests
{
    private static readonly Guid Wh = Guid.NewGuid();
    private static StockKey Key(string sku = "sku-1") => StockKey.Create(Wh, sku);

    [Fact]
    public void Fórmula_de_disponibilidade_respeita_safety_stock()
    {
        var position = StockPosition.Hydrate(Key(), onHand: 10, reserved: 4, safetyStock: 2, version: 1);
        Assert.Equal(4, position.Available); // 10 - 4 - 2
    }

    [Fact]
    public void Reserva_condicionada_por_versão_é_rejeitada_em_conflito()
    {
        var position = StockPosition.Hydrate(Key(), onHand: 5, reserved: 0, safetyStock: 0, version: 7);
        Assert.False(position.TryApplyReserve(1, expectedVersion: 6)); // versão obsoleta
        Assert.True(position.TryApplyReserve(1, expectedVersion: 7));
        Assert.Equal(8, position.Version);
        Assert.Equal(1, position.Reserved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reserva_de_quantidade_inválida_não_aplica(int qty)
    {
        var position = StockPosition.Hydrate(Key(), onHand: 5, reserved: 0, safetyStock: 0, version: 1);
        Assert.False(position.TryApplyReserve(qty, 1));
    }

    [Fact]
    public void Ajuste_negativo_não_ultrapassa_o_disponível()
    {
        var position = StockPosition.Hydrate(Key(), onHand: 5, reserved: 3, safetyStock: 0, version: 1);
        Assert.Equal(2, position.Available);
        Assert.Throws<InvalidOperationException>(() => position.Adjust(-3)); // reservas protegem
        position.Adjust(-2);
        Assert.Equal(3, position.OnHand);
    }

    [Fact]
    public void Liberação_devolve_saldo_incrementando_reserva_disponível()
    {
        var position = StockPosition.Hydrate(Key(), onHand: 5, reserved: 3, safetyStock: 0, version: 1);
        position.Release(3);
        Assert.Equal(5, position.Available);
        Assert.Throws<InvalidOperationException>(() => position.Release(1)); // nada mais reservado
    }
}

/// <summary>
/// Harness in-memory que espelha o update condicional do MongoDB (filtro: _id + version
/// esperada + available suficiente). Prova o MECANISMO de contenção da última unidade
/// (UC003-T01) no nível de domínio; a autoridade real permanece o Mongo (ADR-0002).
/// </summary>
public sealed class InMemoryConditionalPositionStore
{
    private readonly Lock _lock = new();
    private StockPosition? _position;

    public InMemoryConditionalPositionStore(StockPosition initial) => _position = initial;

    /// <summary>Espelha o findOneAndUpdate condicional: nenhum read-modify-write fora do lock.</summary>
    public bool TryReserveAtomically(int quantity)
    {
        lock (_lock)
        {
            var current = _position!;
            if (current.TryApplyReserve(quantity, expectedVersion: current.Version))
            {
                _position = current;
                return true;
            }
            return false;
        }
    }
}

public class ContentionTests
{
    [Fact]
    public async Task Última_unidade_sob_concorrência_permite_exatamente_uma_reserva()
    {
        // UC003-T01 / INV-STOCK-001
        var key = StockKey.Create(Guid.NewGuid(), "sku-hot");
        var store = new InMemoryConditionalPositionStore(
            StockPosition.Hydrate(key, onHand: 1, reserved: 0, safetyStock: 0, version: 1));

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => store.TryReserveAtomically(1))));

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(9, results.Count(r => !r));
    }

    [Fact]
    public async Task Saldo_nunca_fica_negativo_com_reservas_paralelas()
    {
        var key = StockKey.Create(Guid.NewGuid(), "sku-x");
        var store = new InMemoryConditionalPositionStore(
            StockPosition.Hydrate(key, onHand: 3, reserved: 0, safetyStock: 0, version: 1));

        var results = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => store.TryReserveAtomically(2))));

        Assert.Equal(1, results.Count(r => r)); // 3 de saldo, lotes de 2 → apenas 1 lote cabe
    }
}

public class ReservationTests
{
    private static readonly IReadOnlyList<ReservationLine> Lines =
    [new(StockKey.Create(Guid.NewGuid(), "sku-1"), 1)];

    [Fact]
    public void Confirmar_fixa_pedido_e_bloqueia_expiração()
    {
        var now = DateTimeOffset.UtcNow;
        var reservation = Reservation.Create("chk-1", Lines, now, TimeSpan.FromMinutes(15));
        reservation.Confirm("order-1", now.AddMinutes(1));

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal("order-1", reservation.OrderId);
        Assert.Throws<InvalidOperationException>(() => reservation.MarkExpired(now.AddHours(1))); // confirmada NÃO expira
    }

    [Fact]
    public void Confirmar_após_ttl_é_rejeitado()
    {
        var now = DateTimeOffset.UtcNow;
        var reservation = Reservation.Create("chk-1", Lines, now, TimeSpan.FromMinutes(15));
        Assert.Throws<InvalidOperationException>(() => reservation.Confirm("order-1", now.AddMinutes(16)));
    }

    [Fact]
    public void Expiração_só_para_reservas_ativas_vencidas()
    {
        var now = DateTimeOffset.UtcNow;
        var reservation = Reservation.Create("chk-1", Lines, now, TimeSpan.FromMinutes(15));
        Assert.Throws<InvalidOperationException>(() => reservation.MarkExpired(now.AddMinutes(14)));
        reservation.MarkExpired(now.AddMinutes(15));
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public void Liberar_após_confirmar_é_válido_cancelamento_pós_confirmação()
    {
        var now = DateTimeOffset.UtcNow;
        var reservation = Reservation.Create("chk-1", Lines, now, TimeSpan.FromMinutes(15));
        reservation.Confirm("order-1", now.AddMinutes(1));
        reservation.Release("cancelamento pós-confirmação");
        Assert.Equal(ReservationStatus.Released, reservation.Status);
    }

    [Fact]
    public void Chave_de_idempotência_e_linhas_são_obrigatórias()
    {
        Assert.Throws<ArgumentException>(() => Reservation.Create("", Lines, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)));
        Assert.Throws<ArgumentException>(() => Reservation.Create("chk", [], DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Movimento_sem_motivo_ou_ator_é_rejeitado()
    {
        var key = StockKey.Create(Guid.NewGuid(), "sku-1");
        Assert.Throws<ArgumentException>(() => StockMovement.Create(key, 5, " ", "actor", "corr", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => StockMovement.Create(key, 5, "motivo", " ", "corr", DateTimeOffset.UtcNow));
    }
}
