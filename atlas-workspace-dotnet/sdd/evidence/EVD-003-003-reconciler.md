# EVD-003-003 — Reconciliador de PendingReconciliation (UC003-T03)

**Data UTC:** 2026-10-29 · **Commits:** `a33086a` (storefront) · **Resultado:** PASS (unitários 9/9) + E2E parcial

## Implementação

- **CheckoutReconciler** (Application): varre sagas `PendingReconciliation`, consulta o PSP por `QueryAuthorizationAsync(saga.OrderId)` (a chave da autorização no PSP é o orderId — o orchestrator passa orderReference como idempotency_key do PSP) e resolve:
  - **Paid** → `AuthorizeIntentAsync` (guard: Unknown→Authorized) + `ConfirmOrderAsync` (guard: ReconciliationRequired→Confirmed) + saga `MarkReconciliationPaid` → **Confirmed**
  - **NotAuthorized** → `ReleaseAsync` (reserva) + `CancelOrderAsync` + `VoidIntentAsync` + saga `MarkReconciliationNotPaid` → **Compensated**
  - **Indeterminate** → mantém pendente para o próximo ciclo (NUNCA presume)
- **ReconciliationHostedService** (Checkout.Api): loop de 5s com cancelamento cooperativo.
- **PspReconciliationPort** (HTTP): `GET /v1/authorizations/by-key/{key}` do PSP fake.
- Transições `MarkReconciliationPaid/NotPaid` no aggregate CheckoutSaga (partial).

## Testes unitários (9/9 PASS)

```text
Pago_confirma_pedido_intent_e_saga          → Confirmed, reserva NÃO liberada, ConfirmOrder+AuthorizeIntent chamados
Não_pago_libera_reserva_cancela_e_compensa  → reserva liberada, CancelOrder+VoidIntent, saga Compensated
Indeterminado_mantém_pendente               → nada chamado, estado mantido
(+ 6 testes da saga orquestrada: T01/T02/T03/declined/sem-saldo/fluxo-feliz)
```

## E2E parcial (intermitência de ambiente)

O ciclo T03 completo foi executado com sucesso na rodada EVD-002-002/006 (reserva retida + PSP UNKNOWN). O fechamento automático pelo hosted reconciliador em E2E completo ficou pendente por instabilidade de ambiente: os hosts background morrem quando a tool call bash termina (o wrapper encerra o grupo de processos), impedindo a espera de 30-60s do ciclo de reconciliação com os hosts no ar. O reconciliador foi verificado rodando (log `[reconciler] 1 saga(s) pendentes, consultando PSP...` em /tmp/d-chk.log), mas a resolução não ocorreu antes de o PSP ser morto pelo fim do script.

**Correção pendente de re-execução**: o reconciliador consultava o PSP pela idempotency key do checkout, mas a autorização foi registrada sob o ORDER ID — fix aplicado (`QueryAuthorizationAsync(saga.OrderId)`, commit `a33086a`). Próxima execução deve resolver.

## Limitações honestas

- Fechamento automático E2E do T03: pendente de re-execução com o fix (uma tool call com timeout generoso).
- Reconciliador roda apenas no Checkout.Api (um único pod); em produção seria um worker dedicado (M8).
