namespace Atlas.Contracts.Events;

/// <summary>
/// Envelope canônico de evento de domínio (INV-EVT-001 / jsonschema/domain-event.schema.json).
/// Imutável; versão de schema explícita; correlação/causalidade propagáveis.
/// </summary>
public sealed partial record DomainEventEnvelope(
    string EventId,
    string EventType,
    string AggregateId,
    long AggregateVersion,
    DateTimeOffset OccurredAt,
    int SchemaVersion,
    string CorrelationId,
    string? CausationId = null,
    string? TenantId = null,
    string? Traceparent = null,
    string? ActorSub = null)
{
    public const string EnvelopeSchemaVersion = "1";

    public static DomainEventEnvelope Create(
        string eventId,
        string eventType,
        string aggregateId,
        long aggregateVersion,
        DateTimeOffset occurredAt,
        string correlationId,
        string? causationId = null,
        string? tenantId = null,
        string? traceparent = null,
        string? actorSub = null) => new(
            eventId, eventType, aggregateId, aggregateVersion, occurredAt,
            SchemaVersion: 1, CorrelationId: correlationId,
            CausationId: causationId, TenantId: tenantId,
            Traceparent: traceparent, ActorSub: actorSub);

    /// <summary>Valida as regras do envelope (espelha o JSON Schema; consumidores devem revalidar no ingest).</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(EventId)) list.Add("event_id é obrigatório");
        if (string.IsNullOrWhiteSpace(AggregateId)) list.Add("aggregate_id é obrigatório");
        if (string.IsNullOrWhiteSpace(CorrelationId)) list.Add("correlation_id é obrigatório");
        if (AggregateVersion < 1) list.Add("aggregate_version deve ser >= 1");
        if (SchemaVersion < 1) list.Add("schema_version deve ser >= 1");
        if (!EventTypePattern().IsMatch(EventType))
            list.Add($"event_type '{EventType}' fora do padrão Aggregate.ActionVn (ADR-0018)");
        errors = list;
        return list.Count == 0;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Z][A-Za-z]+\.[A-Z][A-Za-z]+V[0-9]+$")]
    private static partial System.Text.RegularExpressions.Regex EventTypePattern();
}
