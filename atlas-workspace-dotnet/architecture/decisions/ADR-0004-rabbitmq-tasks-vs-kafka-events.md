# ADR-0004 — RabbitMQ para tasks vs Kafka para eventos

**Status:** PROPOSED · **Data:** 2026-10-27 · **Absorve:** blueprint ADR-003

## Contexto
Duas semânticas de mensageria coexistem: log de eventos de domínio (durável, replayável, fan-out para projeções/analytics) e tarefas pontuais (workflow com ack, retry, DLQ).

## Alternativas
1. **Kafka para eventos de domínio (CDC/outbox) + RabbitMQ para tasks/workflows** (proposta do contrato).
2. Apenas Kafka (tasks via tópicos/consumer groups) — possível, mas per-ack semântico e DLQ por mensagem exigem engenharia extra; contraria a separação semântica.
3. Apenas RabbitMQ — replay de log de eventos e ordem por chave em escala ficam comprometidos.

## Decisão proposta
- Kafka: `OrderConfirmed`, `ProductPublished`… → projeções PG, analytics; schema evolution; consumer groups; backpressure.
- RabbitMQ: notification, geração de etiqueta, import job, compensações temporais — **quorum queues**, manual ack, prefetch, publisher confirms, DLQ, retry com backoff, dedup na aplicação.
- Tasks Rabbit nascem **somente** de consumer/bridge idempotente do Kafka (nunca do request handler) — anti-padrão dual-write.

## Trade-offs
(+) ferramenta certa por semântica; operação razonável on-prem. (−) dois brokers para operar; regras de roteamento precisam ser explícitas na documentação (quando usar qual).

## Modos de falha / rollback
Falha: broker down → outbox acumula (Kafka) / tasks reenfileiradas (Rabbit). Poison message → DLQ + reprocesso com dry-run e autorização (UC-008). Rollback: topologias versionadas em infra repo.

## Fontes
mestre §2.4, §4.2; blueprint #06 §3 (tabela de comunicação); RabbitMQ quorum queues (mestre §6.3).
