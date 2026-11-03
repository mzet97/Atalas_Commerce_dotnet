# EVD-003-009 — Recuperação de sagas crashadas (TEST-003-008) + branch "não pago → compensa" e2e

**Data UTC:** 2026-11-02 · **Commit:** `81e218b` (storefront) · **Base:** hosts reais :5291/5292/5399/5398/5297 · **Resultado:** PASS

## Critério de roadmap fechado: restart/retry/recovery cobertos

**`CheckoutReconciler.RecoverStuckAsync`** — varre sagas em estados intermediários
(`Submitted`/`StockReserved`/`OrderPlaced`) com idade acima do SLA do fluxo normal
(`Recovery:StuckAfterSeconds`, 60s dev) e decide **sem executar etapa duas vezes**:

| Estado da saga crashada | Consulta PSP? | Resolução |
|---|---|---|
| `OrderPlaced` + PSP tem AUTHORIZED | sim | **confirma** (crash pós-autorização — a compra não se perde); order confirmada, intent autorizada, saga Confirmed |
| `OrderPlaced` + PSP 404 | sim | **compensa** (autorização nunca ocorreu): reserva liberada, order cancelada, saga Compensated |
| `OrderPlaced` + PSP down (Indeterminate) | sim | aguarda próxima rodada (nunca presume) |
| `StockReserved`/`Submitted` | não | **compensa** (nada chegou ao PSP) |

Suportes: domínio aceita `OrderPlaced` como fonte de `MarkReconciliationPaid`; port de
reconciliação mapeia **404 → NotAuthorized** (PSP fake determinístico: chave inexistente =
autorização não existe — ADR-0017); hosted service roda recovery a cada ciclo de 5s.

## Branch "não pago → compensa" do reconciliador — E2E REAL (run timeout-1793651613)

```text
Psp__Scenario=TIMEOUT (503 SEM registrar autorização)
POST /checkout              → HTTP 202 Pending (orderId 6204ea18-66d6-…, reserva retida)
[ciclo do reconciliador]
[psp-recon] GET by-key=6204ea18-66d6-… → 404
[reconciler] 1 saga(s) resolvidas
GET  /sagas/timeout-…       → {"state":"Compensated","reservationId":"d7a7dc66-…"}
```

Com os branches anteriores, o reconciliador tem TODOS os desfechos provados e2e:
pago → Confirmed (EVD-003-006) · webhook → Authorized + Confirmed (EVD-003-008) ·
sem registro → Compensated (ESTE) · indeterminado → mantém pendente (unit, 17/17).

## Testes automatizados (CheckoutReconcilerTests 17/17)

| Teste | Prova |
|---|---|
| Crashada_após_autorização_PSP_pago_confirma | saga OrderPlaced + PSP Paid → Confirmed; order+intent resolvidos; reserva NÃO liberada |
| Crashada_antes_do_PSP_compensa | saga OrderPlaced + PSP 404 → Compensated; release + cancel |
| Crashada_só_com_reserva_compensa | StockReserved → Compensated sem consultar PSP |
| Crashada_com_PSP_indisponível_aguarda | OrderPlaced intacto, zero efeito |
| Saga_recente_não_é_tocada | dentro do SLA o recovery NÃO interfere no fluxo em curso |

## Limitações

- Release físico de estoque via gRPC chega na SPEC-006 (o port live é stub honesto —
  o release REAL com ledger é provado em Inventory.IntegrationTests 5/5).
- Recovery single-instância (lock distribuído na SPEC-004).
- Timeout de request do cliente ≠ crash: a saga crashada aqui é simulada por estado
  persistido em возрасте > SLA (determinístico; kill -9 de request em voo é caos do M9).
