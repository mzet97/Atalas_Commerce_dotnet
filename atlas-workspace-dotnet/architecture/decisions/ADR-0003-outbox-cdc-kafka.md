# ADR-0003 — Outbox transacional no MongoDB + CDC (Debezium) + Kafka

**Status:** PROPOSED · **Data:** 2026-10-27 · **Absorve:** blueprint ADR-004

## Contexto
Dual write (salvar agregado e publicar evento independentemente) perde eventos em crash entre operações. O contrato proíbe publicar o mesmo evento para Kafka e RabbitMQ no request handler.

## Alternativas
1. **Outbox na mesma transação Mongo do agregado + Debezium CDC → Kafka; bridge idempotente cria tasks Rabbit** (proposta).
2. Publicar direto do handler após commit — proibida: janela de perda entre commit e publish.
3. Change Data Capture da coleção bruta como "evento" — proibida: CDC bruto não é contrato de negócio; o envelope outbox é o contrato.

## Decisão proposta
- Documento outbox imutável gravado na mesma sessão transacional do agregado, com envelope INV-EVT-001.
- Debezium MongoDB connector (Change Streams; replica set necessário) publica no Kafka; tópicos por bounded context; key = aggregate_id (ordem por chave).
- Consumidores com inbox (`processed_events`) na mesma transação da projeção; retry/backoff, DLQ/quarantine, replay.
- Semântica **at-least-once** declarada; nunca prometer exactly-once end-to-end.

## Trade-offs
(+) sem perda de evento pós-commit; replay completo; auditável. (−) Kafka Connect/Debezium na pilha operacional; lag de outbox a monitorar (`outbox_age`); duplicatas exigem idempotência de consumidor.

## Modos de falha / rollback
Falha: connector down → outbox acumula (backpressure monitorado), retoma ao subir. Rollback: outbox é derivável dos agregados; connector substituível por bridge em C# (BackgroundService) sem mudar contratos.

## Fontes
mestre §4.2, §18.1; blueprint #04 §3; docs Debezium MongoDB (link no mestre §6.3).
