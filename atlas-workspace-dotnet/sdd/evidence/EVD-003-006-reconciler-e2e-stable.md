# EVD-003-006 — Reconciliador E2E estável (UC003-T03) + fidelidade de reidratação da saga

**Data UTC:** 2026-11-02 · **Base:** mongo `atlas-base-mongo-1` (rs0), PSP :5399, Inventory :5291/5292 (h2c), Checkout.Api :5297 com `Psp__Scenario=UNKNOWN` · **Commits:** `5e27ee0` (saga/rota/testes), `783e175` (inventory) · **Resultado:** PASS

## Fluxo real executado (2 runs independentes)

```text
RUN 1: key=t03-1793647551
  POST /admin/positions   → onHand acumulado (seed dev)
  POST /quotes ttl=600s   → quoteId 56e2bfcf…, total 2500 BRL
  POST /checkout          → HTTP 202 {"status":Pending,"orderId":"1cb516b5…",
                            "reason":"PSP indisponível: reconciliação"}
  [ciclo do reconciliador, 5s, hosted service real]
  PSP by-key (orderId)    → AUTHORIZED pr_1cb516b5-686 (resposta autenticada)
  GET  /sagas/t03-…       → {"state":"Confirmed","paymentIntentId":"pi-reconciled",
                             "providerReference":"pr_1cb516b5-686"}   ← doc + leitura FIEIS

RUN 2 (host corrigido, determinístico): key=t03b-1793647844
  POST /checkout          → HTTP 202 Pending (orderId da18aaef-4d17-…)
  [8 s] GET /sagas/t03b-… → {"state":"Confirmed","providerReference":"pr_da18aaef-4d1"}
  PSP by-key              → {"status":"AUTHORIZED","provider_reference":"pr_da18aaef-4d1",
                             "order_id":"da18aaef-4d17-…"}
```

## Bug encontrado e corrigido neste ciclo

**Reidratação infiel da saga reconciliada** (`MongoSagaStore.ToDomain`): `MarkConfirmed`
exige `PaymentAuthorized`; a saga reconciliada vai de `PendingReconciliation` → `Confirmed`
direto, e toda leitura rehidratada reportava `OrderPlaced` (o DOC no Mongo estava `Confirmed`
correto — inconsistência só na leitura).

Correção:
- Domínio: `CheckoutSaga.RestoreReconciliationReference(paymentIntentId, providerReference, confirmed)`
  — reidratação explícita (uso exclusivo do store), restaura estado final do doc.
- Store: `SagaDoc.ProviderReference` persistido; reidratação restaura `Confirmed` + referência.

Regressão automatizada (`MongoSagaStoreTests`, replica set real, 4/4 PASS):
- `Saga_reconciliada_reidrata_Confirmed_com_referência_do_PSP`
- `Saga_pendente_reidrata_PendingReconciliation_para_o_reconciliador`

## Novidades de produto

- Rota read-only `GET /sagas/{idempotencyKey}` (diagnóstico/observabilidade) — estado,
  orderId, reservationId, paymentIntentId, providerReference.
- INV-PAYMENT-001 reforçado: referência do PSP agora persistida NA SAGA (além de order/intent).

## Limitações

- PSP em memória: reconciliação pós-restart do PSP não resolve (dev only; PSP real na SPEC-004+).
- Reconciliador sem lock distribuído multi-instância (single host dev; SPEC-004).
