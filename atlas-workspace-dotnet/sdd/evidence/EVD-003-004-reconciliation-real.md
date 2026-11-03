# EVD-003-004 — Reconciliação REAL: T03 fechado automaticamente pelo reconciliador

**Data UTC:** 2026-10-29 · **Commit:** `8ac1e85` · **Resultado:** PASS (UC003-T03 completo de ponta a ponta)

## Cadeia executada (hosts reais: Checkout.Api 5297 + Inventory.Api 5291/5292 + SimulatedPsp 5399 + Mongo replica set)

```text
1) POST /checkout (idempotencyKey=t03-real, cenário PSP UNKNOWN)
   → {"status":1 (Pending),"orderId":"dbe3c251-…","reason":"PSP indisponível: reconciliação"}
   → Order PendingPayment; saga PendingReconciliation; reserva RETIDA (Reserved=1)

2) Reconciliador (hosted service, ciclo 5s):
   → varre sagas PendingReconciliation
   → consulta PSP: GET /v1/authorizations/by-key/t03-real → AUTHORIZED
   → AuthorizeIntentAsync (Unknown→Authorized, guard de estado, outbox na transação)
   → ConfirmOrderAsync (PendingPayment→Confirmed, guard, outbox)
   → saga MarkReconciliationPaid → Confirmed
   → log: "[reconciler] 1 saga(s) resolvidas"

3) Estado final verificado no Mongo:
   SAGA=4 (Confirmed) · ORDER=1 (Confirmed) · ref=pr_dbe3c251-e6f… · INTENT=1 (Authorized) · RESERVED=1 (pago → mantida)
```

## Fixes reais do ciclo

1. **Reconciliador consultava pela checkout key mas a autorização foi registrada sob o orderId** — fix: `QueryAuthorizationAsync(saga.OrderId)` (a orchestrator passa orderReference como idempotency_key do PSP).
2. **Order não era marcado ReconciliationRequired no UNKNOWN** — adicionado `MarkReconciliationRequiredAsync` ao port (transição com guard PendingPayment→ReconciliationRequired + outbox).
3. **Adaptadores Confirm/Cancel com fallback duplo de estado** (PendingPayment OU ReconciliationRequired → Confirmed) — async-or nos adapters.

## Correções de infraestrutura de build (padrão recorrente)

- **Cache incremental MSBuild corrompido por `pkill -9 dotnet`** nos scripts E2E: esconde 5-7 erros reais (CS1501/CS0535/CS0246) e serve binários velhos — o build reportava 0 erros com DLL antiga. Fix de processo: `rm -rf bin obj` do projeto afetado + rebuild. **Lição**: após qualquer pkill -9 dotnet, SEMPRE limpar bin/obj dos projetos editados.

## Limitações honestas

- Reconciliador roda apenas no Checkout.Api (um pod); worker dedicado em M8.
- PSP fake: UNKNOWN registra AUTORIZADO + responde 503 (o caso "pago com resposta perdida"). O caso "não processado" (NOT_FOUND no by-key) → reconciliador compensa — fluxo simétrico já implementado (NotAuthorized → Compensated).
- Playwright E2E da jornada: pendente (gate M3).
