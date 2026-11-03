# EVD-003-002 — Checkout real: gRPC→owner→PSP→saga persistida; UNKNOWN retém reserva

**Data UTC:** 2026-10-29 · **Commits:** `fe0e520`, `0a4f6c9` · **Resultado:** PASS (parcial — gate M3 IN_PROGRESS)

## Cadeia executada (hosts reais, sem mocks)

Checkout.Api (5297) → port gRPC h2c (5292) → **Inventory.Api owner real** (CAS no Mongo replica set) → PSP fake HTTP (5399, cenário por config) → Orders/Payments stores Mongo → saga persistida em `atlas_checkout`.

## Saídas reais

```text
health: inv=200 psp=200 chk=200

T03 (PSP UNKNOWN):  {"status":1,"orderId":"217a496b-…","reason":"PSP indisponível: reconciliação"}
T02 (mesma key):    {"status":1,"orderId":"217a496b-…","reason":"psp-unknown"}   ← MESMO resultado
Evidência: intents=2, intent_state=4 (Unknown), reserved_final=2 (reserva do T03 RETIDA
           aguardando reconciliação — nunca liberada nem confirmada sem decisão)

Fluxo feliz (APPROVED): {"status":0,"orderId":"c300210a-…"} → Confirmed
Evidência: orders=2, order consistentes, reserva do fluxo feliz consumida pelo fluxo
```

## Bugs reais pegos pelo E2E (cada um com fix e commit)

1. **Warehouse mismatch**: port gRPC usa `Guid.Empty` ("N" = 32 zeros) vs seed uuid4 → "posição inexistente". Seed alinhado ao formato do domínio.
2. **Retriable transaction re-executa o callback** (driver Mongo): InsertOne do Order/Reservation explodia com 11000 no retry → inserts idempotentes (11000 tratado). O contrato do projeto alertava; agora está no código.
3. Cache incremental corrompido escondia 7 erros reais do Checkout.Api (exposto por rebuild limpo) — adaptadores sem usings, CA1725, SubmitCheckoutRequest perdido.

## Limitações honestas (restam p/ gate M3)

- Reconciliador de PendingReconciliation (consulta PSP e resolve saga+order) — próximo.
- Webhook do PSP via HTTP e2e (HMAC já validado isoladamente).
- Quote (TTL) e Cart — domínios pendentes.
- E2E Playwright.

## Infra operacional (Podman)

Keycloak, Mongo rs, PG, Kafka, LGTM + hosts .NET: Catalog 5299/5290, Inventory 5291/5292, PSP 5399, Checkout 5297, Gateway 5180, Next 3001.
