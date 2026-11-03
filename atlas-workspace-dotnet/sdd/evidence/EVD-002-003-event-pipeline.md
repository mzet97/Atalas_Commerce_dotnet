# EVD-002-003 — Pipeline de eventos M2: outbox → Kafka → projeção PostgreSQL (idempotente)

**Data UTC:** 2026-10-29 · **Repo:** atlas-storefront-dotnet · **Commit:** `9b51374` (dispatcher/projector `f197a1b` antecede) · **Resultado:** PASS (E2E real, 1/1; 64 testes no repo; CVE zero)

## Infra utilizada (Podman rootless)

- **Kafka single-node KRaft** (`apache/kafka:3.9.0`, hostname atlas-kafka, advertised PLAINTEXT_HOST 127.0.0.1:29092; binds `://:porta` — literal 0.0.0.0 quebra o wrapper). "Kafka Server started" + porta aberta.
- **PostgreSQL 17** (já do M1): tabelas de teste criadas/derrubadas por execução.
- MongoDB replica set (outbox dos owners).

## Traçado real do teste E2E (`OutboxToProjectionE2ETests`, saída detalhada)

```text
[1] dispatcher publicou 1 evento(s) em atlas.catalog.v1.e2e.<guid>   (owner gravou aggregate+outbox na mesma transação)
[2] consumido: Product.PublishedV1 ver=1                              (consumer group novo, earliest)
[3] projeção aplicada: rows=1 source_version=1                        (upsert + processed_events na MESMA transação PG)
[4] reentrega do mesmo event_id: dedup — projeção permanece 1 linha   ← INV-EVT-001 (at-least-once seguro)
[5] evento stale (ver=1) ignorado: source_version permanece 1         ← ordem por aggregate não retroage
Passed! 1/1 (4s)
```

## Implementação

- `Atlas.Workers.Projections/OutboxDispatcher` — bridge outbox→Kafka (ADR-0003): lote de não-publicados, produce key=aggregate_id, marca `Published` pós-ack (persistência do broker). Kafka down → outbox acumula e retoma (OPS-07).
- `Atlas.Workers.Projections/CatalogProjector` — consome envelopes (canônico: event_id/type/version/correlation/payload), `INSERT ... ON CONFLICT DO NOTHING` em `processed_events` + upsert condicional `WHERE source_version < @ver`, **na mesma transação PG** (ADR-0011: Npgsql direto).
- Hosted services (loop) entram quando o Catalog.Api hospedar os workers; no M2 o caminho testado é o mesmo código de batch.

## Fatos de engenharia do ciclo (diagnóstico real)

1. apache/kafka wrapper: advertised deriva do bind literal → usar `PLAINTEXT://:9092` (sem host) e advertised só dos listeners externos.
2. Consumidor antes da propagação do tópico → `AdminClient.CreateTopicsAsync` explícito no setup.
3. Dispatcher respeita `entry.Topic` (canônico) — teste roteia ao tópico efêmero explicitamente.
4. OutboxEntry de leitura usa Guid-as-string (mesma serialização dos owners).

## Suítes do repo (todas verdes)

Cat.Api 3 · Cat.Domain 11 · Inv.Domain 14 · Off.Domain 22 · Cat.Int 3 · Inv.Int 5 · Off.Int 5 · **Projection E2E 1** = **64**

## Limitações honestas

- Debezium/Kafka Connect não instalado — a bridge C# é a rota ADR-0003 (fallback aceito); migração para connector decide-se por benchmark/operacional no M3+.
- Esquema de evento via convenção (sem schema registry); evolução validada por contrato nos próximos ciclos (jsonschema já no contracts).
- gRPC owners e Gateway/BFF/vitrine: fatias seguintes do M2.
