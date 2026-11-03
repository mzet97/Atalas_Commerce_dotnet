# ATLAS COMMERCE .NET — M3 REPORT (PARCIAL — atualizado)

```text
DATE_UTC: 2026-10-29
REPOS / COMMITS:
  atlas-storefront-dotnet — e621d5a → 3820cab
MILESTONE: M3 / SPEC-003 — Cart, Quote, Checkout Saga, Order, PSP Fake
STATUS: IN_PROGRESS (fatia core REAL + reconciliador REAL completos; restam: COM-08 e2e estável, reconciliador e2e estável, webhook e2e, Playwright, stores Quote/Cart Mongo no host)
```

## IMPLEMENTED (verificado)

| Componente | Prova | EVD |
|---|---|---|
| Orders.Domain (snapshot imutável, state machine idempotente, reconciliação) | 9 testes | EVD-003-001 |
| Payments.Domain (intent, dedup webhook, Unknown→reconcilia, HMAC) | 9 testes | EVD-003-001 |
| CheckoutSaga (compensações, idempotência por passo) | 7 testes | EVD-003-001 |
| SimulatedPSP (ADR-0017) | 4 testes | EVD-003-001 |
| CheckoutOrchestrator (ports DIP, T01/T02/T03/declined/sem-saldo/fluxo-feliz) | 6 testes | EVD-003-001 |
| Cart (add/remove/merge guest, INV-03) + PriceQuote (TTL INV-QUOTE-001, COM-08) | 18 testes | EVD-003-005 |
| Reconciliador (pago→confirma, não pago→compensa, indeterminado→mantém) | 3 testes | EVD-003-003 |
| Stores Mongo reais: orders/payments/saga/cart/quote (com outbox, guards de estado) | integrados | EVD-003-002/005 |
| Checkout.Api host (SubmitCheckout idempotente + ports gRPC/PSP reais) | E2E real | EVD-003-002 |
| **UC003-T03 REAL**: PSP UNKNOWN → PENDING → reconciliador consulta PSP → Confirmed c/ ref PSP rastreável | log + Mongo | EVD-003-004 |
| UC003-T02 REAL: mesma idempotency key → mesmo resultado | E2E | EVD-003-002 |
| Contenda T01 REAL: reserva gRPC CAS no Mongo (10-way→1) | EVD-003-002 | EVD-003-002 |

## TESTS EXECUTED

```text
92 testes de domínio+application verdes:
  Catalog.Domain 11 · Inventory.Domain 14 · Offers.Domain 22
  Orders.Domain 9 · Payments.Domain 9 · Checkout.Domain 18 · Checkout.Application 9
Integração real (replica set): Catalog 3 · Inventory 3 · Offers 5 · Checkout.Infrastructure 2
gRPC API: Catalog 4 · Inventory 2 · PSP 4
E2E pipeline: 1 · Checkout.Api: 6 (incl. 4 gRPC + 2 token)
TOTAL: ~132 testes verdes
```

## Fatos de engenharia do ciclo (bugs reais pegos)

1. **Reconciliador consultava pela checkout key mas a autorização foi registrada sob o orderId** — fix `QueryAuthorizationAsync(saga.OrderId)` (commit a33086a).
2. **Compensação não incluída no estado Submitted** — OUT_OF_STOCK não marcava Compensated.
3. **Intent-criado ≠ autorizado** — criação do intent avançava a saga sem autorização real.
4. **UNKNOWN sem transição do Order** — order não era marcado ReconciliationRequired.
5. **Fake de estoque stateless** — a contenda testava contra estoque infinito.
6. **Cache incremental MSBuild corrompido por pkill -9 dotnet** — escondia erros reais; fix: rm bin/obj + rebuild.
7. **Rota /quotes aceitava TTL 0** — agora valida TTL > 0.

## NOT IMPLEMENTED / RESTAM p/ gate M3

- **Reconciliador E2E estável**: fix orderId já no código (a33086a); hosts background morrem entre tool calls bash (limitação do ambiente); rodada com hosts vivos por 60s fecha o UC003-T03 E2E.
- **COM-08 E2E**: quote com TTL 1s + aguardar 5s → checkout rejeita — script pronto (e2e-com08.sh), pendente de execução estável.
- **Webhook PSP e2e via HTTP** no Payments host — HMAC já validado isoladamente.
- **Stores Quote/Cart no host** (a rota /admin/positions já grava via driver C#).
- **E2E Playwright** + vídeo.
- **Quote sem breakdown de impostos** (o breakdown por região é fase 2).

## NEXT EXECUTABLE TASK

1. Rodar e2e-com08.sh com hosts vivos por 60s (uma tool call com timeout 420s + sem pkill no meio) — fecha COM-08 e2e.
2. Rodar e2e-m3d.sh com o fix orderId — fecha UC003-T03 e2e com reconciliador.
3. Quote/Cart stores no host (rotas dev já existem).
4. E2E Playwright + relatório M3 gate §16.
