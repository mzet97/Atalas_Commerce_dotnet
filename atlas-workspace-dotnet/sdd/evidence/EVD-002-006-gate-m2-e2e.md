# EVD-002-006 — E2E do gate M2: publicação real de ponta a ponta até a vitrine

**Data UTC:** 2026-10-29 · **Commits:** storefront `1fe3ffe`+`e621d5a` · **Resultado:** PASS

## Cadeia executada (todos os elos REAIS)

```text
Rota dev (owner) → draft v1 PendingApproval
Control (JWT Keycloak real): request(editor) → decide(approver≠requester) → token HMAC
DISPATCH: Control → gRPC h2c (127.0.0.1:5290) → Catalog owner
Catalog: valida HMAC + payload + versão → publica → Mongo (aggregate+outbox NA transação)
  DISPATCH={"state":"Executed","eventId":"9b16aa5a-…"}  (E2E) / c1522950-… (smoke)
Outbox: 1 evento Product.PublishedV1, payload com product_id/market/approved_by rastreável
Worker (hosted): dispatcher→Kafka (tópicos canônicos) → projector→PG (dedup transacional)
BFF (Gateway YARP): GET /api/catalog/products → count crescente (11→16 nas rodadas)
VITRINE (Next 3001): "Produto Smoke M2" renderizado entre os product-items ✓
```

## Infra no momento do teste (Podman rootless)

Keycloak 26.0.7 (realm atlas) · Mongo 7.0.14 replica set rs0 · PostgreSQL 17 · Kafka 3.9 KRaft · grafana/otel-lgtm — todos containers Podman; hosts .NET no distrobox.

## Correções reais deste ciclo (cada uma com diagnóstico documentado)

1. `Product_id vazio no token` — [FromBody] explícito nos DTOs de módulo; teste fortalecido (assert do doc no Mongo).
2. gRPC pendura em cleartext HTTP/1.1 — endpoint h2c 5290 no Catalog.
3. `--urls`/launchSettings vs Listen manual — `--no-launch-profile` + Listen duplo no código.
4. Portas ocupadas por hosts antigos → novos morrem em bind e o fluxo conversa com binário antigo — script espera portas liberarem (pkill -9 + loop ss).
5. **NU1109** (CPM) impedia rebuild completo → binário antigo publicando sem título; fix Hosting 10.0.14.
6. Porta 3000 = Grafana (LGTM) → Next na 3001.
7. Worker crash em CreateTopics idempotente → catch amplo documentado.

## Evidência de trace (M1, reutilizada)

Traces Control→Catalog no Tempo (6 cross-process, EVD-001-008) — o mesmo caminho do dispatch.

## Limitações honestas

- Títulos: eventos anteriores ao fix exibem placeholder (por design: source of truth é o evento).
- Escopo 403 por ROLE (M2); ABAC por warehouse específico: SPEC-004.
- Debezium substituído pela bridge C# (ADR-0003 — fallback aceito).
- Vitrine: lista mínima SSR (UX rica é M5).
