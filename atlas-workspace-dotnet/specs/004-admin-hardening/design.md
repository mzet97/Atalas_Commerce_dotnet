# SPEC-004 — design.md

- **Approvals module (Control):** aggregate `Approval` (máquina do diagrama 14), `ApprovalDecision` com approver obrigatoriamente ≠ requester (invariante de domínio, não só UI); comando ao owner só em `Dispatching` com `approval_id` assinado; owner valida aprovação (chamada de verificação ao Control ou token assinado — decisão de execução: token assinado curto com payload hash, verificável offline).
- **Audit module:** coleção `control.audit` append-only; cada doc: `prev_hash`, `self_hash = H(prev||payload)`; worker de verificação reconstrói a cadeia (teste contínuo); escrita na mesma transação do approval/action quando aplicável.
- **IAM integration:** Keycloak roles + `ScopeGrant` materializados em policy server do Control com TTL curto; owners fazem callout ou validam JWT claims + token de escopo assinado; revogação → propagação < TTL.
- **Dashboards Angular:** fontes = APIs de leitura dos owners/projeções (nunca tabelas internas diretas); componente `<freshness sourceVersion projectedAt>` padronizado.
- **Contratos novos:** `proto/admin_audit_export`, `proto/approval_token` (formato assinado).

## Testes
Unit (invariante ≠ requester, cadeia de hash), Integration (revogação → 403 com policy server), E2E (fluxo four-eyes completo), Segurança (escalada tentada → negada+auditada).
