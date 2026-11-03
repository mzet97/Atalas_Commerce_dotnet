# SPEC-006 — spec.md

**Problema:** operar o pós-confirmação do pedido (expedição, entrega, devolução e comunicação) de forma coerente com pagamento/reserva/pedido, tolerante a falhas assíncronas.

**Atores:** fulfillment_operator, cliente, carrier fake (externo simulado), support_agent.

## Requisitos

1. **Fulfillment:** `FulfillmentOrder` criado por `OrderConfirmed`; picking/packing; `Shipment` (multi-remessa suportada — `ShipmentLine` mapeia `OrderLine`); carrier fake com cotação/etiqueta/tracking/proof-of-delivery determinísticos; eventos `ShipmentCreated/Dispatched/Delivered`.
2. **Returns:** `ReturnRequest` (RMA) com janela de política por market; inspeção; aprovação de refund **four-eyes** (REQ-RET-001); `RefundRequested` → Payments owner (idempotente); refund parcial suportado; logística reversa distinta do financeiro (diagrama 17).
3. **Notifications:** templates por locale/evento; envio via RabbitMQ task (quorum, manual ack, prefetch, retry/backoff, DLQ); dedup por `notification_key`; consentimento/.opt-out respeitado (REQ-CST-001); `NotificationSent/Failed` observáveis.
4. **Consistência:** cancelamento durante fulfillment segue política de janela; eventos fora de ordem rejeitados; reenvio de etiqueta idempotente.

## Fluxos alternativos

Carrier fake indisponível → task reenfileirada com backoff; prova de entrega duplicada → dedup; RMA reprovado → item volta/ descarte com auditoria; refund recusado pelo PSP → `RefundPending` + caso de conciliação.

## Critérios de aceite

Refund coerente (nunca sem RMA aprovado nem duplicado); DLQ/replay demonstrados; notificação nunca duplicada sob concorrência; timeline do cliente completa (order→shipment→delivery→return).

## DoD
Cenários PASS com EVD (filas, DLQ, traces, E2E); runbook de DLQ/replay; carrier fake documentado como simulado.
