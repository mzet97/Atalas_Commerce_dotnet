namespace Atlas.Payments.Domain;

using System.Security.Cryptography;
using System.Text;

public readonly record struct PaymentIntentId(Guid Value)
{
    public static PaymentIntentId New() => new(Guid.NewGuid());
}

public enum PaymentIntentState
{
    Created = 0,
    Authorized = 1,
    Captured = 2,
    Declined = 3,
    Unknown = 4,          // PSP timeout — reconciliador resolve (INV-PAYMENT-001)
    AuthorizationVoided = 5,
    RefundPending = 6,
    Refunded = 7,
    RefundFailed = 8,
}

/// <summary>Tentativa auditável (ledger — nunca edit in place, INV-PAYMENT-001).</summary>
public sealed record PaymentAttempt(
    Guid Id, string Provider, string Scenario, PaymentIntentState ResultState, DateTimeOffset OccurredAtUtc);

/// <summary>
/// Agregado PaymentIntent (INV-PAYMENT-001): idempotency key do PSP, tentativas em ledger,
/// webhooks DEDUPLICADOS por id — o estado final só muda por resposta autenticada do PSP
/// ou reconciliação; timeout é estado UNKNOWN, nunca sucesso presumido.
/// </summary>
public sealed class PaymentIntent
{
    private readonly HashSet<string> _processedWebhookIds;
    private readonly List<PaymentAttempt> _attempts;

    private PaymentIntent(PaymentIntentId id, string orderId, long amountMinor, string currency,
        string pspIdempotencyKey, PaymentIntentState state, HashSet<string> webhookIds, List<PaymentAttempt> attempts)
    {
        Id = id;
        OrderId = orderId;
        AmountMinor = amountMinor;
        Currency = currency;
        PspIdempotencyKey = pspIdempotencyKey;
        State = state;
        _processedWebhookIds = webhookIds;
        _attempts = attempts;
    }

    public PaymentIntentId Id { get; }
    public string OrderId { get; }
    public long AmountMinor { get; }
    public string Currency { get; }
    public string PspIdempotencyKey { get; }
    public PaymentIntentState State { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? DeclineReason { get; private set; }
    public IReadOnlyList<PaymentAttempt> Attempts => _attempts;
    public IReadOnlyCollection<string> ProcessedWebhookIds => _processedWebhookIds;

    public static PaymentIntent Create(string orderId, long amountMinor, string currency, string pspIdempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountMinor);
        if (string.IsNullOrWhiteSpace(pspIdempotencyKey)) throw new ArgumentException("idempotency_key do PSP obrigatória");
        return new(PaymentIntentId.New(), orderId, amountMinor, currency, pspIdempotencyKey,
            PaymentIntentState.Created, new HashSet<string>(), new List<PaymentAttempt>());
    }

    public static PaymentIntent Hydrate(PaymentIntentId id, string orderId, long amountMinor, string currency,
        string pspIdempotencyKey, PaymentIntentState state, string? providerReference, string? declineReason,
        IEnumerable<string> webhookIds, IEnumerable<PaymentAttempt> attempts) =>
        new(id, orderId, amountMinor, currency, pspIdempotencyKey, state,
            new HashSet<string>(webhookIds), new List<PaymentAttempt>(attempts))
        {
            ProviderReference = providerReference, DeclineReason = declineReason,
        };

    /// <summary>Autorização confirmada (síncrona autenticada ou webhook) — dedup por webhook id.</summary>
    public bool TryAuthorize(string providerReference, string? webhookId, DateTimeOffset nowUtc)
    {
        if (webhookId is not null && !_processedWebhookIds.Add(webhookId))
            return false; // webhook duplicado: nenhum efeito (INV-PAYMENT-001)
        if (State == PaymentIntentState.Authorized) return State == PaymentIntentState.Authorized;
        if (State != PaymentIntentState.Created) return false;

        State = PaymentIntentState.Authorized;
        ProviderReference = providerReference;
        _attempts.Add(new(Guid.NewGuid(), "simulated-psp", "authorize", PaymentIntentState.Authorized, nowUtc));
        return true;
    }

    public void MarkDeclined(string reason, DateTimeOffset nowUtc)
    {
        if (State != PaymentIntentState.Created) throw new InvalidOperationException($"Transição inválida: {State} → Declined");
        State = PaymentIntentState.Declined;
        DeclineReason = reason;
        _attempts.Add(new(Guid.NewGuid(), "simulated-psp", "decline", PaymentIntentState.Declined, nowUtc));
    }

    /// <summary>Timeout: Unknown (reconciliável), nunca sucesso presumido.</summary>
    public void MarkUnknown(string reason, DateTimeOffset nowUtc)
    {
        if (State != PaymentIntentState.Created) throw new InvalidOperationException($"Transição inválida: {State} → Unknown");
        State = PaymentIntentState.Unknown;
        DeclineReason = reason;
        _attempts.Add(new(Guid.NewGuid(), "simulated-psp", "timeout", PaymentIntentState.Unknown, nowUtc));
    }

    public bool TryCapture(DateTimeOffset nowUtc)
    {
        if (State != PaymentIntentState.Authorized) return false;
        State = PaymentIntentState.Captured;
        _attempts.Add(new(Guid.NewGuid(), "simulated-psp", "capture", PaymentIntentState.Captured, nowUtc));
        return true;
    }

    public bool TryStartRefund(string reason, DateTimeOffset nowUtc)
    {
        if (State != PaymentIntentState.Captured) return false;
        State = PaymentIntentState.RefundPending;
        _attempts.Add(new(Guid.NewGuid(), "simulated-psp", $"refund:{reason}", PaymentIntentState.RefundPending, nowUtc));
        return true;
    }

    public void CompleteRefund(DateTimeOffset nowUtc)
    {
        if (State != PaymentIntentState.RefundPending) throw new InvalidOperationException($"Transição inválida: {State} → Refunded");
        State = PaymentIntentState.Refunded;
        _attempts.Add(new(Guid.NewGuid(), "simulated-psp", "refund-complete", PaymentIntentState.Refunded, nowUtc));
    }

    public bool TryResolveUnknown(bool authorized, string providerReference, DateTimeOffset nowUtc)
    {
        if (State != PaymentIntentState.Unknown) return false;
        if (authorized)
        {
            State = PaymentIntentState.Authorized;
            ProviderReference = providerReference;
        }
        else
        {
            State = PaymentIntentState.Declined;
            DeclineReason = "reconciled-not-authorized";
        }
        _attempts.Add(new(Guid.NewGuid(), "simulated-psp", "reconcile", State, nowUtc));
        return true;
    }
}

/// <summary>Validador do webhook assinado do PSP fake (HMAC-SHA256 — ADR-0017; simétrico no emissor).</summary>
public static class PspWebhookSignature
{
    public static string Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    public static bool IsValid(string body, string signature, string secret) =>
        !string.IsNullOrEmpty(signature) &&
        CryptographicOperations.FixedTimeEquals(
            Sign(body, secret).Select(c => (byte)c).ToArray(),
            signature.Select(c => (byte)c).ToArray());
}
