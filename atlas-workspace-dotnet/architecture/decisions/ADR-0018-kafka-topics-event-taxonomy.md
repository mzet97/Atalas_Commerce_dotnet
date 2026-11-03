# ADR-0018 — Taxonomia de tópicos Kafka e naming de eventos

**Status:** PROPOSED · **Data:** 2026-10-28

## Contexto
Múltiplos contextos publicam eventos via outbox→CDC; sem convenção estável desde o M2, consumers e replay viram dívida.

## Alternativas
1. **Tópicos por bounded context com sufixo de versão de schema: `atlas.{context}.v{n}` (ex.: `atlas.catalog.v1`, `atlas.orders.v1`); `event_type = {Aggregate}.{Action}V{n}`; key = `aggregate_id`; envelope canônico INV-EVT-001.** (proposta)
2. Tópico único por tipo de evento (`atlas.events.product-published`) — rejeitado: explosão de tópicos e fraca coesão de contexto para replay.
3. Tópico único global — rejeitado: sem isolamento de consumer group/lag por contexto.

## Decisão proposta
- `atlas.{catalog|offers|inventory|cart|checkout|orders|payments|fulfillment|returns|notifications|control}.v1`.
- `event_type`: `Product.Published`, `Stock.Reserved`, `Payment.Captured`… versão major no tipo quando schema quebra (compatibilidade testada por schema registry/JSON Schema no CI).
- Particionamento por `aggregate_id` (ordem por agregado); consumer groups por worker (`projections-catalog`, `funnel-analytics`).
- RabbitMQ naming espelhado: `atlas.task.{context}.{action}` (quorum), DLQ `atlas.task.{context}.dlq`.

## Rollback
Renames exigem dupla publicação N/N-1 durante janela; breaking checks no CI (buf/JSON Schema diff).
