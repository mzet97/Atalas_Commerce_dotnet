namespace Atlas.Workers.Notifications;

using System.Text.Json;

/// <summary>Envelope canônico dos eventos (mesma forma do dispatcher dos projections).</summary>
public sealed record EventEnvelope(
    Guid EventId,
    string EventType,
    string AggregateId,
    long AggregateVersion,
    string CorrelationId,
    JsonElement Payload);
