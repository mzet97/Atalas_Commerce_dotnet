# ATLAS COMMERCE .NET — M3 GATE REPORT

```text
DATE_UTC: 2026-11-02
REPOS / COMMITS:
  atlas-storefront-dotnet — f18994e → … → 3820cab → a641f69 → 783e175 → 5e27ee0 → ebdf026 → 81e218b → 84a67fb
  atlas-control-dotnet    — 0f419c6 (sem mudanças no M3 — four-eyes já provado no M2)
  atlas-workspace-dotnet (coord.) — (este commit: EVD-003-001..009 + relatórios)
MILESTONE: M3 / SPEC-003 — Cart, Quote, Checkout Saga, Order, PSP Fake
STATUS: **PASS** (gate M3; único item pendente externo: git push p/ CI remoto)
```

## Gate M3 (critérios do contrato §12/roadmap) — resposta item a item

| Critério | Evidência | Status |
|---|---|---|
| **E2E local** (compra com PSP fake, saga, cache/read model) | E2E REAIS com 4 hosts vivos (PSP :5399, Payments :5398, Inventory :5291/5292 h2c, Checkout :5297) sobre mongo rs0: quote→checkout→order→intent→outbox; resposta 202/200/409 com rastro no Mongo e no PSP (EVD-003-002/006/007/008/009) | **PASS** |
| **oversell = 0** | T01 com CAS real no replica set (10 reservas concorrentes → 1 vencedor; `$expr` server-side) — EVD-003-002 (+ gRPC real no M2) | **PASS** |
| **duplicate_paid_order = 0** | T02 mesma idempotency key → mesmo resultado sem nova reserva (E2E real) + T05 webhook duplicado → `duplicate` sem re-efeito (E2E real, dedup transacional por índice único) — EVD-003-002/008 | **PASS** |
| **timeout coberto** | T03: UNKNOWN → 202 Pending (nunca sucesso presumido) → reconciliador consulta PSP por orderId → Confirmed com providerReference rastreável (E2E real, 2 runs + smoke) — EVD-003-006 | **PASS** |
| **retry coberto** | T02 + idempotência do PSP (mesma key → mesma autorização) + re-envio de webhook idempotente; testes 17/17 orchestrator | **PASS** |
| **restart coberto** | TEST-003-008: `RecoverStuckAsync` varre sagas crashadas em idade > SLA — crash pós-autorização → **Confirmed** (compra não se perde); sem registro no PSP → **Compensated**; PSP down → próxima rodada (EVD-003-009, 5/5 unit + reidratação fiel 4/4) | **PASS** |
| **replay coberto** | reentrega outbox→Kafka→PG com dedup transacional (M2, EVD-002-003) + webhook re-enviado idempotente (T05) + guards de estado em toda transição (CAS) | **PASS** |
| Cenários T01..T05 + COM-08 do UC-003 | TODOS com evidência real (tabela abaixo) | **PASS** |

## Matriz UC-003 — cenário × camadas × evidência

| Cenário | Unit | Integração (replica set real) | E2E (hosts reais) |
|---|---|---|---|
| T01 contenda última unidade | ✔ (fake stateful) | ✔ CAS Mongo 10-way | ✔ gRPC real (M2) |
| T02 mesma key → mesmo resultado | ✔ | — | ✔ HTTP real |
| T03 UNKNOWN → reconciliador → Confirmed | ✔ | ✔ round-trip saga 4/4 | ✔ reconciliador hosted + ref PSP |
| T04 leitura defasada não autoriza | ✔ (orchestrator valida no owner) | — | ✔ (reserva autoritativa CAS) |
| T05 webhook duplicado/fora de ordem | ✔ | ✔ 6/6 endpoint tests | ✔ HMAC→processed→duplicate |
| COM-08 quote expirada | ✔ 3 casos (zero efeito) | ✔ TTL no store | ✔ 409 + valid:false |
| TEST-003-008 crash/restart | ✔ 5 recovery | ✔ reidratação fiel | ✔ 404→Compensated |
| TEST-003-007 DECLINED | ✔ order→PaymentFailed | ✔ CAS transição | ✔ (compensa c/ release) |

## TESTS EXECUTED (final do M3)

```text
Storefront: 141/141 verdes — 17/17 projetos (suíte completa paralela)
  Domínios: Catalog 11 · Inventory 14 · Offers 22 · Orders 9 · Payments 9 · Checkout 18
  Application/Orchestrator+Reconciler: 17 · Checkout.Api fluxo: 4 · Payments.Api webhook: 6
  APIs: Catalog.Api 6 · Inventory.Api 2 · SimulatedPsp 4
  Integração REAL (replica set): Catalog 3 · Inventory 5 · Offers 5 · Checkout.Infra 4
  Pipeline REAL: Kafka→PG 2 (dedup + deltas)
Control: 8 PASS · Contracts: 8 PASS + buf lint PASS
Vulnerabilidades: zero (dotnet list package --vulnerable, M1; sem novas deps de risco)
PERFORMANCE: NOT_MEASURED (benchmark é o M9 — honesto, sem promessa)
```

## Fatos de engenharia do ciclo (14 bugs reais, cada um com diagnóstico e fix)

1. Reconciliador consultava pela checkout key (autorização é sob orderId) — a33086a
2. Compensação ausente no estado Submitted (OUT_OF_STOCK não compensava)
3. Intent-criado ≠ autorizado (saga avançava sem autorização real)
4. UNKNOWN sem transição do Order → ReconciliationRequired
5. Fake de estoque stateless (contenda contra estoque infinito)
6. Cache MSBuild corrompido por pkill -9 (binário velho servido como novo)
7. /quotes aceitava TTL 0 silenciosamente
8. Reidratação infiel da saga reconciliada (OrderPlaced vs Confirmed no doc) — RestoreReconciliationReference
9. ProviderReference do PSP não persistida na saga (INV-PAYMENT-001)
10. **11000 dentro de transação Mongo ABORTA a txn** (padrão catch-e-continua = loop de retry do driver, ~985 txnNumbers/2min) — read-first na mesma txn; resolve 2 falhas antigas da contenda
11. /quotes fire-and-forget no save (GET perdia a corrida sob carga paralela) — await
12. 11000 no dedup do webhook (mesmo padrão do #10) — sair via exceção que ABORTA
13. Declared DECLINED deixava order órfã em PendingPayment — PaymentFailed (TEST-003-007)
14. Webhook fora de ordem (captured antes de authorized) → 409 sem efeito e sem gravar dedup (TEST-003-004)

## NOT IMPLEMENTED / DEFERRED (honesto)

- **Playwright UX E2E** — critério do **M5** (vitrine/carrinho UI nem existem ainda; o acceptance.feature do M3 não exige browser). O E2E do M3 é HTTP host-a-host.
- Release físico de estoque via gRPC no cancelamento — SPEC-006 (port live é stub; release REAL com ledger provado em Inventory.IntegrationTests).
- Captura no caminho reconciliado/webhook (política de captura — SPEC-004).
- Reconciliador/recovery multi-instância com lock distribuído — SPEC-004.
- PSP real (SPEC-004+); stores Cart/Quote no host de produção (rotas dev operacionais).
- CI remoto (git push — ação do proprietário).

## Infraestrutura real utilizada

Podman rootless via API: mongo rs0 (`atlas-base-mongo-1`), pg recriado CONFORME o compose
canônico (`atlas-base-pg` — o antigo tinha env divergente, causa do 28P01), kafka KRaft
:29092. Hosts dev como background tasks persistentes (eliminou a instabilidade que travava
o ciclo anterior). Runbook: `atlas-storefront-dotnet/deploy/compose/README-podman.md`.

## EVIDENCE INDEX (EVD-003-*)

001 domínios+PSP+saga · 002 checkout real (T01/T02) · 003 reconciler unit · 004 reconciliation real
(1ª passada) · 005 cart/quote stores · **006 reconciliador e2e estável + fidelidade de reidratação** ·
**007 COM-08 e2e** · **008 webhook T05 e2e + convergência webhook×reconciliador** ·
**009 recovery TEST-003-008 + branch compensa e2e**

## NEXT EXECUTABLE TASK

**SPEC-004 — Admin hardening (M4)**: four-eyes já provado no M2; falta escopo ABAC fino
(warehouse/seller), auditoria completa das operações de risco, lag CQRS visível no Control,
e self-approval negado E2E na UI. Paralelo possível: SPEC-005 (Storefront UX M5) quando o
proprietário priorizar.
