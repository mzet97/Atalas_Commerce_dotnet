# ATLAS COMMERCE .NET — M3 GATE REPORT (PARCIAL — HISTÓRICO)

> **SUPERSEDED** pelo relatório final `M3-gate-report.md` (2026-11-02 — gate **PASS**).
> Mantido por rastreabilidade do ciclo (bugs 1..11; slices T03/COM-08).

```text
DATE_UTC: 2026-11-02
REPOS / COMMITS:
  atlas-storefront-dotnet — 3820cab → a641f69 → 783e175 → 5e27ee0 → ebdf026 → 81e218b
MILESTONE: M3 / SPEC-003 — Cart, Quote, Checkout Saga, Order, PSP Fake
STATUS: SUPERSEDED — ver M3-gate-report.md (PASS). Este documento cobre até o bug 11; bugs 12..14 no relatório final.
```

## Gate M3 (critérios do contrato §12) — resposta item a item

| Critério | Evidência | Status |
|---|---|---|
| Quote com TTL e breakdown (UC-002, INV-QUOTE-001) | PriceQuote.Issue + IsValidAt + DifferenceMinor — 6 testes unitários + 2 integração | **PASS** |
| Cart com validação (UC-002, INV-03) | Cart aggregate: add/remove/merge/limites — 7 testes | **PASS** (unit) |
| COM-08: quote expirada rejeita no checkout | **E2E REAL (EVD-003-007)**: TTL 1s → `valid:false` → 409 re-cotação obrigatória → **zero efeito colateral** (sem saga/reserva/order/intent) + 12/12 Application | **PASS** (unit + E2E real) |
| T01: contenda última unidade (CAS real no Mongo replica set) | 10 reservas concorrentes → 1 vencedor, oversell=0 | **PASS** (integração real, EVD-003-002) |
| T02: mesma idempotency key → mesmo resultado | E2E real via Checkout.Api: mesma key → mesmo checkoutId + orderId | **PASS** (E2E real, EVD-003-002) |
| T03: PSP UNKNOWN → PENDING → reconciliador → Confirmed | **E2E REAL estável (EVD-003-006)**: 202 Pending → ciclo do reconciliador (hosted service, 5s) → consulta PSP por orderId → AUTHORIZED → saga `Confirmed` + `providerReference` rastreável + leitura rehidratada FIEL | **PASS** (unit + E2E real) |
| T05: webhook duplicado e fora de ordem | **E2E REAL (EVD-003-008)**: webhook HMAC autenticado → `processed` → intent Authorized → reenvio do MESMO webhook → `duplicate` sem re-efeito; convergência webhook×reconciliador idempotente + 5/5 testes integração (replica set real) | **PASS** (unit + integração + E2E real) |
| Restart/recovery: saga crashada retoma sem executar etapa 2× (TEST-003-008) | **E2E + unit (EVD-003-009)**: recovery varre estados intermediários — crash pós-autorização → Confirmed; sem registro no PSP → Compensated; branch "não pago → compensa" provado E2E (TIMEOUT→404→Compensated) | **PASS** (unit 5/5 + E2E real) |
| DECLINED → compensação completa (reserva liberada) | 12/12 saga orquestrada | **PASS** (unit) |
| Fluxo feliz CONFIRMED com pedido e pagamento persistidos | 4/4 Checkout.Api flow + orchestrator | **PASS** (E2E real) |
| Out_of_stock rejeita antes de criar pedido | 12/12 saga | **PASS** (unit) |

## TESTS EXECUTED (acumulado M3)

```text
Suíte completa do storefront (dotnet test sln, paralela): 140/140 verdes, 17/17 projetos
  Catalog.Domain 11 · Inventory.Domain 14 · Offers.Domain 22 · Orders.Domain 9
  Payments.Domain 9 · Checkout.Domain 18 · Checkout.Application 12 · Catalog.Api 6
  Catalog.Api.Tests (fluxo) 4 · Inventory.Api 2 · SimulatedPsp 4 · Payments.Api (webhook) 5
  Checkout.Application 17 (incl. 5 recovery TEST-003-008)
Integração real (replica set): Catalog 3 · Inventory 5 · Offers 5 · Checkout.Infrastructure 4
E2E pipeline (Kafka→PG): 2 (pg recriado conforme compose: atlas/atlas_dev_only/atlas_read)
E2E infra: kafka KRaft em 127.0.0.1:29092 (advertised localhost — reutilizado da sessão anterior)
Control: 8 PASS · Contracts: 8 PASS + buf lint PASS
TOTAL: ~156 testes verdes (storefront 140 + control 8 + contracts 8)
```

## Infraestrutura real desta rodada (2026-11-02)

- `atlas-base-pg-1` antigo foi recriado como `atlas-base-pg` **conforme o compose canônico**
  (`POSTGRES_USER=atlas / atlas_dev_only / atlas_read`; volume `atlas-base_pg-data` fresh —
  o container anterior tinha env divergente `atlas-pg-dev-only`, causa do 28P01 nos
  ProjectionTests). Commit do runbook não requerido (API Podman, ver README-podman.md).
- Hosts dev como **background tasks do harness** (`run_in_background`) — vivem entre tool calls;
  eliminada a instabilidade que bloqueava o E2E na execução anterior.

## Bugs reais pegos pelo E2E (11 totais, cada um com fix e commit)

1. Reconciliador consultava pela checkout key mas a autorização foi registrada sob o orderId (fix: consulta por orderId — a33086a)
2. Compensação não incluída no estado Submitted (OUT_OF_STOCK não marcava Compensated)
3. Intent-criado ≠ autorizado (criação do intent avançava a saga sem autorização real)
4. UNKNOWN sem transição do Order para ReconciliationRequired
5. Fake de estoque stateless (contenda testava contra estoque infinito)
6. Cache incremental MSBuild corrompido por pkill -9 dotnet (escondia erros reais)
7. Rota /quotes aceitava TTL 0 silenciosamente
8. **Reidratação infiel da saga reconciliada** (ToDomain): `MarkConfirmed` exige `PaymentAuthorized`; saga reconciliada vai Pending→Confirmed direto e TODA leitura rehidratada reportava `OrderPlaced` enquanto o doc estava `Confirmed`. Fix: `RestoreReconciliationReference` (reidratação explícita) + `ProviderReference` persistido na saga (EVD-003-006; regressão 4/4 MongoSagaStoreTests)
9. **Referência do PSP não persistida na saga** (INV-PAYMENT-001: ref só em order/intent) — agora persistida + reidratada + exposta na rota de diagnóstico
10. **11000 dentro de transação ABORTA a transação** (`MongoInventoryStore`): o padrão insert+catch(11000)+continuar envenenava a txn — todo comando seguinte falhava e o retry do driver entrava em loop (~985 txnNumbers, 2min até desistir). Fix: **read-first na mesma txn** (retorna Duplicate do vencedor) + catch externo aguarda visibilidade do vencedor (o 11000 dispara contra key em voo). Inventory.IntegrationTests 5/5 em **1s** (antes 2 falhas/4min) — commit `783e175`
11. **`/quotes` com save fire-and-forget** (`SaveQuoteAsync` não aguardado): o GET subsequente perdia a corrida sob carga (teste de fluxo falhou na suíte paralela; regra própria "sem fire-and-forget em request handler" violada). Fix: await antes de responder — commit `5e27ee0`
12. **11000 no dedup do webhook (mesmo padrão do #10, pego no 1º build)**: retornar `Duplicate` normalmente após 11000 na coleção de dedup = commit falha = retry do driver em loop (teste T05 pendurou 1m40s e pegou o bug). Fix: duplicado sai via EXCEÇÃO que aborta a txn; rejeição (intent ausente/estado divergente) também aborta para NÃO gravar dedup sem efeito — commit `ebdf026`

## NEXT EXECUTABLE TASK

1. Relatório M3 gate §16 FINAL (critérios do contrato todos com evidência real;
   Playwright UX E2E é critério do M5 — acceptance.feature do M3 não exige browser)
2. Pós-gate: rota dev de leitura de order (diagnóstico) e SPEC-004

## Pendências externas

- `git push` para GitHub Actions (CI remoto)
