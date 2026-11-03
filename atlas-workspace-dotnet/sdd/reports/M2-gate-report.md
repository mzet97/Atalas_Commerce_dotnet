# ATLAS COMMERCE .NET — M2 GATE REPORT

```text
DATE_UTC: 2026-10-29
REPOS / COMMITS:
  atlas-platform-contracts-dotnet — 00a1f1f (SDK gRPC + proto aditivo)
  atlas-storefront-dotnet          — …→ e621d5a (fatias M2 + E2E)
  atlas-control-dotnet             — 36a3c3d → 0f419c6 (four-eyes)
  atlas-workspace-dotnet (coord.)  — (este commit)
MILESTONE: M2 / SPEC-002 — Catálogo + Offers + Inventory (1º vertical slice comercial)
STATUS: **PASS** (gate M2; único item pendente externo: git push p/ CI remoto)
```

## Gate M2 (critérios do contrato §12) — resposta item a item

| Critério | Evidência | Status |
|---|---|---|
| Produto cadastrado por operador permitido e aparece na loja pelos CAMINHOS REAIS | E2E REAL: rota dev→owner→four-eyes(Control JWT+HMAC)→gRPC h2c→Published→outbox→dispatcher→Kafka→projector→PG→BFF→**vitrine Next exibindo "Produto Smoke M2"** (EVD-002-006) | **PASS** |
| Operador sem escopo falha com **403 auditado** | Control: policy por role (JWT Keycloak real), middleware de auditoria ANTES do UseAuthorization; 403 (sem role) e 409 (self-approval) auditados — EVD-002-005 | **PASS** (escopo por warehouse específico refinado na SPEC-004) |
| Evento repetido não duplica view | processed_events dedup transacional + stale guard: EVD-002-003 (teste E2E de reentrega) e EVD-002-004 (3 views) | **PASS** |
| 2 requisições concorrentes pela última unidade não vendem mais que 1 | **UC003-T01 com CAS real**: no Mongo (10-way, EVD-002-002) e via gRPC (Inventory.Api, EVD-002-005); contenda de ofertas 8-way→1 (EVD-002-004... 003) | **PASS** |
| Evidência com Mongo, Kafka, PG, UI, trace, testes | EVD-002-001..006; traces Control→Catalog no Tempo (M1); smoke com evidência por etapa | **PASS** |

## TESTS EXECUTED (acumulado)

```text
Storefront: 74 testes (domínio+API) + 13 integração Mongo + 1 E2E pipeline + 1 E2E views = 76 PASS
  · gRPC in-memory: Catalog 4 + Inventory 2
Control: 8 PASS (5 arquitetura + 3 four-eyes c/ Keycloak real)
Contracts: 8 PASS + buf lint PASS
E2E gate: smoke cross-process (DISPATCH Executed + outbox 1 evento + produto Published)
         + vitrine exibindo produto com título real
Vuln scan: zero (todos os repos)
```

## Fatos de engenharia do ciclo (todos com diagnóstico real)

1. **Kestrel cleartext = HTTP/1.1** → channel gRPC pendura; fix: endpoint h2c dedicado (5290).
2. **NU1109** (CPM pinning) impedia o REBUILD completo — o binário antigo continuava publicando sem título; clean+build expôs e fix Hosting 10.0.14.
3. **launchSettings vs Listen manual vs --urls**: prioridades resolvem-se com `--no-launch-profile` + Listen explícito.
4. **[FromBody] explícito** em DTOs de módulo — binding silenciosamente vazio no smoke (product_id="" no token).
5. **UseAuthorization short-circuita o Forbid** → auditoria de 403 deve ser middleware ANTERIOR.
6. **MapInboundClaims=false** — mapeamento default anonimizava os atores.
7. Forbid→403 do Grafana na porta 3000 confundiu o smoke da vitrine → Next na 3001.

## NOT IMPLEMENTED / DEFERRED (honesto)

- Contenda via BFF/vitrine (o CAS provado no owner; exposição UI do estoque é M3+).
- Escopo ABAC fino por warehouse na policy (403 atual é por ROLE; refinamento SPEC-004).
- CI remoto (push dos repos — ação do proprietário); Debezium (bridge C# em uso, ADR-0003).
- available_offer_view/inventory_admin_view sem consumidores de UI ainda (o Control lerá na SPEC-004).

## NEXT EXECUTABLE TASK

SPEC-003 (Cart/Quote/Checkout Saga/Order/PSP Fake) — já com base sólida: owners gRPC, outbox→Kafka→PG operacionais e reserva autoritativa provada.
