using Atlas.Contracts.Events;
using Xunit;

namespace Atlas.Contracts.Events.Tests;

public class DomainEventEnvelopeTests
{
    [Fact]
    public void Envelope_válido_passa_na_validação()
    {
        var envelope = DomainEventEnvelope.Create(
            eventId: "019314f0-0000-7000-8000-000000000001",
            eventType: "Product.PublishedV1",
            aggregateId: "019314f0-0000-7000-8000-000000000002",
            aggregateVersion: 3,
            occurredAt: DateTimeOffset.Parse("2026-10-28T00:00:00Z"),
            correlationId: "corr-1");

        Assert.True(envelope.IsValid(out var errors));
        Assert.Empty(errors);
        Assert.Equal(1, envelope.SchemaVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("product.published")]
    [InlineData("ProductPublished")]
    [InlineData("Product.Published")]
    [InlineData("Product.Published.Version1")]
    public void Event_type_fora_do_padrão_é_rejeitado(string eventType)
    {
        var envelope = DomainEventEnvelope.Create(
            "id", eventType, "agg", 1, DateTimeOffset.UtcNow, "corr");
        Assert.False(envelope.IsValid(out var errors));
        Assert.Contains(errors, e => e.Contains("event_type"));
    }

    [Fact]
    public void Versões_e_campos_obrigatários_são_validados()
    {
        var envelope = new DomainEventEnvelope(
            EventId: "", EventType: "Order.ConfirmedV1", AggregateId: "",
            AggregateVersion: 0, OccurredAt: DateTimeOffset.UtcNow,
            SchemaVersion: 0, CorrelationId: "");

        Assert.False(envelope.IsValid(out var errors));
        Assert.Contains(errors, e => e.Contains("event_id"));
        Assert.Contains(errors, e => e.Contains("aggregate_id"));
        Assert.Contains(errors, e => e.Contains("aggregate_version"));
        Assert.Contains(errors, e => e.Contains("schema_version"));
        Assert.Contains(errors, e => e.Contains("correlation_id"));
    }

    [Fact]
    public void Campos_opcionais_não_invalidam()
    {
        var envelope = DomainEventEnvelope.Create(
            "id", "Payment.CapturedV2", "agg", 2, DateTimeOffset.UtcNow, "corr",
            causationId: "cause-1", tenantId: "seller-1", traceparent: "00-abc-def-01", actorSub: "user-1");
        Assert.True(envelope.IsValid(out _));
        Assert.Equal("cause-1", envelope.CausationId);
    }
}
