# ATLAS COMMERCE .NET — M4 GATE REPORT

```text
DATE_UTC: 2026-11-02
REPOS / COMMITS:
  atlas-platform-contracts-dotnet — 8d379b2 (GetStockVersion RPC) → bfa5430 (realm basic scope)
  atlas-storefront-dotnet        — 420ff4c (AdjustStock transacional versionado) + workers multi-db
  atlas-control-dotnet           — 6db256e → d22a761 → 722b442 (fechamento)
  atlas-workspace-dotnet (coord.) — (este commit: EVD-004-001/002 + relatórios)
MILESTONE: M4 / SPEC-004 — Admin hardening (four-eyes, scopes ABAC, auditoria imutável, lag CQRS)
STATUS: **PASS** (gate M4; pendência externa única: git push → CI)
```

## Gate M4 (contrato/roadmap) — resposta item a item

| Critério | Evidência | Status |
|---|---|---|
| **Self-approval negado e auditado** (gate-chave) | E2E REAL com JWT Keycloak: admin tenta aprovar o próprio request → **409 DENIED_SELF_APPROVAL**, registro seq 14 na cadeia de hash (EVD-004-001) | **PASS** |
| Four-eyes Request→Approve(distinto)→Dispatch→Executed | E2E REAL: request 201 → decide 200 + token v2 → dispatch gRPC **Executed positionVersion=3**; owner REVALIDA assinatura/exp/action/subject | **PASS** |
| Grant revogado → 403 na próxima chamada | E2E REAL: revoke → request imediato → **403 DENIED_NO_GRANT** (revalidação no request, no decide E no dispatch — token aprovado fica inutilizável) | **PASS** |
| Auditoria imutável com hash encadeado + verificador | AuditChainStore SHA256(prev\|seq\|payload); **verificador como TESTE xUnit** — tampering retroativo detectado com brokenAtSeq exato (AuditChainTests 3/3); live: valid=true count=24+ | **PASS** |
| Escopos ABAC dinâmicos com expiração | Grant(subject,action,warehouse,ttl) — efeito imediato em 3 pontos de revalidação; token v2 com jti de uso único (nonce queimado antes do dispatch) | **PASS** |
| Lag CQRS visível | `/modules/inventory-operations/cqrs-lag`: ownerHeadVersion=3 × projectedSourceVersion=3, **lag=0 REAL** (gRPC GetStockVersion × PG view com evento AdjustedV1 versionado) | **PASS** |
| Operação de risco exige aprovação NO OWNER | AdjustStock gRPC rejeita sem approval_token v2 (PermissionDenied) — antes do M4 aceitava qualquer um; testes gRPC 2/2 | **PASS** |
| Listagens server-side (req.5) | audit com page/pageSize/action/decision; approvals paginado; positions sobre a view PG com filtro | **PASS** |
| Relatórios operacionais (req.6) | `/modules/ops/overview`: outbox pendente/idade POR OWNER DB, sagas por estado (conciliação), DLQ N/A até M6 | **PASS** |
| Exportação de trilha (req.3/DoD) | `/modules/audit/export?format=csv\|json` + runbook de revogação de emergência (`docs/runbook-revogacao-emergencia.md`) | **PASS** |
| ADRs de dependências fechadas | ADR-0012 ACCEPTED (MediatR/MassTransit/FluentAssertions NOT_ADOPTED — licenças); ADR-0010 ACCEPTED (Aspire REJECTED — Compose/Podman/Helm provados) | **PASS** |

## TESTS EXECUTED (M4)

```text
Contracts: 8/8 + buf lint PASS (RPC GetStockVersion aditivo)
Control: 13/13 — Architecture 7 (nova regra módulos→kernel) · Host.Tests 3 · AuditChain 3
Storefront: 141/141 (17 projetos) · ProjectionTests 2/2 (multi-db)
E2E REAL: 10 passos four-eyes com JWT Keycloak (EVD-004-001) + smoke dos endpoints req.5/6 (EVD-004-002)
PERFORMANCE: NOT_MEASURED (M9) · Vulnerabilidades: zero novas deps
```

## Bugs reais da fatia (5)

1. **Dispatcher único do workers apontava atlas_catalog** — outbox de inventory NUNCA despachado em live (M2 só provou inline); fix: um dispatcher por owner db
2. AdjustStock aceitava ajuste SEM approval_token (owner não revalidava)
3. Client atlas-services sem escopo `basic` → token SEM sub → grants com subject vazio (500)
4. StockApprovalToken.Issue sem base64 no body (ponto do "inventory.adjust" quebrava o split) — pego pelo teste gRPC
5. CA1305 warning-as-error quebrava builds silenciosamente → binário velho servido 2× na mesma sessão (lição: checar timestamp do DLL após rebuild, sempre)

## NOT IMPLEMENTED / DEFERRED (honesto)

- Nonce uso-único enforce no CONTROL (owner valida assinatura/exp mas não consulta nonces — SPEC-004+ com IAM real; TTL 90s mitiga)
- Reconciliador/recovery single-instância (lock distribuído futuro)
- Captura automática no caminho reconciliado (política de captura — SPEC-006+)
- Export CSV limitado a 200 registros por chamada (job assíncrono p/ volumes maiores)
- CI remoto (git push — ação do proprietário)

## EVIDENCE INDEX

EVD-004-001 four-eyes E2E Keycloak + ABAC + chain + lag · EVD-004-002 fechamento req.3/5/6 (este commit)

## NEXT EXECUTABLE TASK

**M5/SPEC-005 — Storefront UX, busca, i18n + Playwright** (spec DRAFT→READY; carrinho/checkout UI é a fatia nova crítica). FASE 1 do plano aprovado.
