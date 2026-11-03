# SPEC-004 — spec.md

**Problema:** operações administrativas de risco com segregação de deveres verificável, escopo ABAC revogável em tempo de execução e trilha de auditoria imutável — fechando o ciclo Admin→owner do ADR-0001.

**Atores:** operadores (todos os perfis do glossário), approver, auditor, iam_admin.

## Requisitos

1. **Four-eyes (INV-ADMIN-001):** fluxo `Request → Approve(distinto) → Dispatch → Executed/Failed` (diagrama 14) com justificativa, payload hash e revalidação no owner no momento do comando; revogação de aprovação pendente suportada.
2. **Escopos ABAC dinâmicos:** grants por market/warehouse/seller com expiração; revogação reflete na próxima chamada (owner consulta IAM/policy cache curto); `ADM-05` (403 + audit).
3. **Auditoria imutável (REQ-ADM-003):** `AdminAction` append-only com actor, subject, ação, escopo, before/after hash, motivo, correlation, resultado; hash encadeado (block-hash) por registro; exportação para auditor; redaction de PII.
4. **Dashboards (REQ-ADM-004):** métricas reais dos owners/projeções com `source_version`, `projected_at` e estados `PUBLISHING/SYNCING`; alerta de lag.
5. **Listagens:** paginação/filtro server-side sobre projeções (`inventory_admin_view`, `admin_audit_search`, `payments_reconciliation_view` leitura); comandos sempre revalidados no owner.
6. **Relatórios operacionais:** casos de conciliação, DLQ depth, outbox age — leitura de métricas/observabilidade.

## Fluxos alternativos

Approver = requester → negado + audit; aprovação revogada durante dispatch → owner revalida e falha se expirada; auditoria indisponível → comando bloqueado (fail-closed); export grande → job assíncrono RabbitMQ.

## Critérios de aceite

Self-approval negado+auditado; grant revogado → 403 na próxima chamada; toda mudança sensível gera registro encadeado verificável; UI exibe defasagem CQRS; auditor exporta trilha com hashes validáveis.

## Definition of Done

Cenários PASS com EVD; verificador de cadeia de hash de auditoria como teste; runbook de revogação de emergência.
