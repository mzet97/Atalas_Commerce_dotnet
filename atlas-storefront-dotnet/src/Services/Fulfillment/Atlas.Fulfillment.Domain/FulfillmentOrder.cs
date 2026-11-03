namespace Atlas.Fulfillment.Domain;

/// <summary>
/// FulfillmentOrder (SPEC-006 req.1): nasce de Order.ConfirmedV1; máquina de estados
/// Picking→Packed→Shipped→Delivered com transições idempotentes e eventos de domínio.
/// Carrier fake determinístico: tracking = ATLAS-{orderId:N8}; etiqueta reenviável (req.4).
/// </summary>
public sealed partial class FulfillmentOrder
{
    public enum FulfillmentState { Picking = 1, Packed = 2, Shipped = 3, Delivered = 4, Cancelled = 5 }

    public Guid OrderId { get; }
    public FulfillmentState State { get; private set; }
    public string Carrier { get; private set; }
    public string TrackingCode { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyList<FulfillmentLine> Lines => _lines;
    private readonly List<FulfillmentLine> _lines = [];
    private readonly List<string> _steps = [];
    public IReadOnlyList<string> Steps => _steps;

    private FulfillmentOrder(Guid orderId, FulfillmentState state, string carrier, string tracking, long version)
    {
        OrderId = orderId; State = state; Carrier = carrier; TrackingCode = tracking; Version = version;
    }

    public static FulfillmentOrder StartFromOrderConfirmed(Guid orderId, IReadOnlyList<(string SkuId, int Quantity)> lines, DateTimeOffset nowUtc)
    {
        var f = new FulfillmentOrder(orderId, FulfillmentState.Picking, "atlas-fake", TrackingCodeFor(orderId), 1);
        f._lines.AddRange(lines.Select(l => new FulfillmentLine(l.SkuId, l.Quantity)));
        f._steps.Add($"created:{nowUtc:O}");
        return f;
    }

    public static string TrackingCodeFor(Guid orderId) => $"ATLAS-{orderId:N}"[..14].ToUpperInvariant();

    /// <summary>Transição idempotente: repetir mesmo estado = no-op true (req.4 reenvio idempotente).</summary>
    public bool TryAdvance(FulfillmentState to, DateTimeOffset nowUtc)
    {
        if (State == to) return true;
        if ((int)to != (int)State + 1) return false; // só caminho linear; fora de ordem rejeitado (req.4)
        State = to; Version++;
        _steps.Add($"{to}:{nowUtc:O}");
        return true;
    }

    public bool TryCancel(string reason, DateTimeOffset nowUtc)
    {
        if (State is FulfillmentState.Shipped or FulfillmentState.Delivered) return false; // janela req.4
        State = FulfillmentState.Cancelled;
        _steps.Add($"cancelled:{reason}:{nowUtc:O}");
        return true;
    }
}

public sealed record FulfillmentLine(string SkuId, int Quantity);

public partial class FulfillmentOrder
{
    public static FulfillmentOrder Hydrate(Guid orderId, FulfillmentState state, string carrier, string tracking, long version, IReadOnlyList<string> steps)
    {
        var f = new FulfillmentOrder(orderId, state, carrier, tracking, version);
        foreach (var st in steps) f._steps.Add(st);
        return f;
    }
}
