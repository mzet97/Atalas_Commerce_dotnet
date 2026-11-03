# ATLAS COMMERCE .NET — M4 GATE REPORT (PARCIAL — HISTÓRICO)

> **SUPERSEDED** pelo relatório final `M4-gate-report.md` (2026-11-02 — gate **PASS**).

```text
DATE_UTC: 2026-11-02
REPOS / COMMITS:
  atlas-platform-contracts-dotnet — 8d379b2 (GetStockVersion RPC) + bfa5430 (realm basic scope)
  atlas-storefront-dotnet        — 420ff4c (AdjustStock transacional versionado) + workers multi-db
  atlas-control-dotnet           — 6db256e (Kernel+InventoryOperations+AuditChain) + d22a761 (policies/fallback)
  atlas-workspace-dotnet (coord.) — (este commit: EVD-004-001)
MILESTONE: M4 / SPEC-004 — Admin hardening (four-eyes, scopes ABAC, auditoria, lag CQRS)
STATUS: IN_PROGRESS — fatia core REAL completa (EVD-004-001); restam: relatório §16 final
```

## Gate M4 (critérios do contrato/roadmap) — resposta item a item

| Critério | Evidência | Status |
|---|---|---|
| **Self-approval negado e auditado** (gate-chave M4) | **E2E REAL com JWT Keycloak**: admin tenta aprovar o próprio request → 409 DENIED_SELF_APPROVAL na cadeia de hash (EVD-004-001 seq 14) | **PASS** |
| Four-eyes completo Request→Approve(distinto)→Dispatch→Executed | E2E REAL: 201→200+token v2→gRPC Executed positionVersion=3; owner REVALIDA token (assinatura/exp/action/subject) | **PASS** |
| Grant revogado → 403 na próxima chamada | E2E REAL: revoke → request imediato → 403 DENIED_NO_GRANT (revalidado no request, no approve E no dispatch) | **PASS** |
| Auditoria imutável com hash encadeado | AuditChainStore SHA256(prev\|seq\|payload) append-only; verificador: valid=true count=24; 403s de policy TAMBÉM na cadeia | **PASS** |
| Lag CQRS visível | /modules/inventory-operations/cqrs-lag: ownerHeadVersion=3 × projectedSourceVersion=3, lag=0 — head via gRPC GetStockVersion (RPC novo) × PG view (evento AdjustedV1 versionado) | **PASS** |
| Operação de risco exige aprovação no OWNER | AdjustStock gRPC REJEITA sem approval_token v2 (antes do M4 aceitava qualquer um!) — testes gRPC 2/2 | **PASS** |

## Bugs reais da fatia (4 — detalhes no EVD-004-001)

1. Dispatcher único do workers apontava atlas_catalog — outbox de inventory NUNCA despachado em live
2. AdjustStock aceitava ajuste sem approval_token
3. Escopo basic ausente no client atlas-services → token sem sub
4. StockApprovalToken.Issue sem base64 no body

## TESTS EXECUTED

```text
Contracts: 8/8 + buf lint PASS
Storefront: 141/141 (17 projetos) · ProjectionTests 2/2 com multi-db
Control: 10/10 (Architecture 7 — nova regra módulos→kernel; Host.Tests 3)
E2E REAL: 10 passos four-eyes com JWT Keycloak (tabela no EVD-004-001)
```

## NEXT EXECUTABLE TASK

1. Relatório M4 §16 final (após digest do proprietário OU mais fatias: exportação de trilha, dashboards/relatórios operacionais req.5/6)
2. Pendência externa: git push (CI)
