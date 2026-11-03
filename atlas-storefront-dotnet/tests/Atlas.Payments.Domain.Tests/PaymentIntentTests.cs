using Atlas.Payments.Domain;
using Xunit;

namespace Atlas.Payments.Domain.Tests;

public class PaymentIntentTests
{
    private static PaymentIntent NewIntent() => PaymentIntent.Create("order-1", 5000, "BRL", "psp-key-1");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Novo_intent_nasce_Created()
    {
        var intent = NewIntent();
        Assert.Equal(PaymentIntentState.Created, intent.State);
        Assert.Empty(intent.Attempts);
    }

    [Fact]
    public void Autoriza_uma_vez_e_webhook_duplicado_é_inerte() // UC003-T05 / INV-PAYMENT-001
    {
        var intent = NewIntent();
        Assert.True(intent.TryAuthorize("pr_1", webhookId: "wh-1", Now));
        var attemptsAfterFirst = intent.Attempts.Count;

        Assert.False(intent.TryAuthorize("pr_1", webhookId: "wh-1", Now)); // duplicado → inerte
        Assert.Equal(attemptsAfterFirst, intent.Attempts.Count);
        Assert.Equal(PaymentIntentState.Authorized, intent.State);
    }

    [Fact]
    public void Capture_só_após_authorize()
    {
        var intent = NewIntent();
        Assert.False(intent.TryCapture(Now));
        intent.TryAuthorize("pr_1", null, Now);
        Assert.True(intent.TryCapture(Now));
        Assert.Equal(PaymentIntentState.Captured, intent.State);
    }

    [Fact]
    public void Timeout_vira_Unknown_e_reconcilia() // UC003-T03
    {
        var intent = NewIntent();
        intent.MarkUnknown("PSP timeout", Now);
        Assert.Equal(PaymentIntentState.Unknown, intent.State);

        Assert.True(intent.TryResolveUnknown(authorized: true, "pr_recon", Now));
        Assert.Equal(PaymentIntentState.Authorized, intent.State);
        Assert.Contains(intent.Attempts, a => a.Scenario == "reconcile");
    }

    [Fact]
    public void Refund_só_de_captured_e_fluxo_completo()
    {
        var intent = NewIntent();
        Assert.False(intent.TryStartRefund("devolução", Now)); // ainda Created
        intent.TryAuthorize("pr_1", null, Now);
        intent.TryCapture(Now);
        Assert.True(intent.TryStartRefund("devolução", Now));
        intent.CompleteRefund(Now);
        Assert.Equal(PaymentIntentState.Refunded, intent.State);
    }

    [Fact]
    public void Declined_é_terminal()
    {
        var intent = NewIntent();
        intent.MarkDeclined("cartão recusado", Now);
        Assert.Throws<InvalidOperationException>(() => intent.MarkUnknown("x", Now));
        Assert.False(intent.TryCapture(Now));
    }

    [Fact]
    public void Assinatura_de_webhook_HMAC_valida_e_rejeita_adulteração()
    {
        const string secret = "dev-psp-secret";
        const string body = "{\"event\":\"payment.authorized\"}";
        var sig = PspWebhookSignature.Sign(body, secret);
        Assert.True(PspWebhookSignature.IsValid(body, sig, secret));
        Assert.False(PspWebhookSignature.IsValid(body + " ", sig, secret));
        Assert.False(PspWebhookSignature.IsValid(body, sig, "outro-secret"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Valor_inválido_é_rejeitado(long amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentIntent.Create("o", amount, "BRL", "k"));
}
