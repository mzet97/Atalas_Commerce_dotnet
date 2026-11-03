# SPEC-006 — design.md

- **Fulfillment Service:** aggregate `FulfillmentOrder`/`Shipment`; state machine `CREATED → PICKING → PACKED → DISPATCHED → IN_TRANSIT → DELIVERED`; integração `ICarrierAdapter` (fake determinístico com atraso/erros configuráveis — Adapter pattern, ADR series); `TrackingEvent` append-only.
- **Returns Service:** aggregate `ReturnRequest` (RMA) `REQUESTED → AWAITING_GOODS → INSPECTING → APPROVED/REJECTED → REFUND_REQUESTED`; refund dispara comando gRPC ao Payments com idempotency_key = return_id+attempt.
- **Notifications Service:** `Notification` doc + templates i18n; **task bridge** Kafka→Rabbit (ADR-0004) gera `notify.customer.*`; worker com manual ack, prefetch, retry exponencial + jitter, DLQ `notifications.dlq`; `notification_key` único (dedup).
- **Workers:** `TaskWorkers` (Rabbit consumers), `Reconciliation` já existente cobre refund pending.

## Tópicos/filas
Kafka: `atlas.fulfillment.v1`, `atlas.returns.v1`. Rabbit: `notify.email`, `notify.push` (quorum), `notifications.dlq`.

## Testes
Unit (state machines, políticas de janela), Integration (RabbitMQ Testcontainers: ack/retry/DLQ/poison), E2E (entrega → devolução → refund), Fault (broker restart, consumer crash pós-ack parcial).
