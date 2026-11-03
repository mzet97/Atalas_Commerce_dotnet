# ADR-0007 — Keycloak OIDC com RBAC + ABAC e política de aprovação (four-eyes)

**Status:** PROPOSED · **Data:** 2026-10-27

## Contexto
Operação multi-mercado/depósito exige autorização por ação **e** escopo de recurso; ações de risco financeiro exigem segregação de funções. On-prem, sem dependência de IAM de nuvem.

## Alternativas
1. **Keycloak self-hosted (OIDC Auth Code + PKCE), roles + resource policies, MFA para admins, four-eyes nos owners** (proposta do contrato).
2. IAM de nuvem (Azure AD/Entra) — rejeitado: operação on-prem obrigatória sem Azure.
3. Autenticação caseira — proibida: superfície de ataque e auditoria insuficientes para o escopo.

## Decisão proposta
- Perfis canônicos: customer, catalog_editor, pricing_manager, inventory_operator, fulfillment_operator, finance_analyst, support_agent, auditor, iam_admin (+seller_operator fase 3).
- Ações escopadas (`catalog:publish`, `inventory:adjust`, `payments:refund`…) por market/warehouse/seller; grants com expiração e justificativa.
- IAM valida identidade/grants; **o domínio owner revalida** ownership, estado, limite e abrangência (INV-SEC-001).
- Four-eyes `requested_by != approved_by` para risco (INV-ADMIN-001); auditoria append-only; BFF/sessão server-side quando justificado; Keycloak com persistência própria (exceção CQRS, blueprint #06 §5).

## Trade-offs
(+) padrão de mercado, self-hosted, auditoria forte. (−) operação de um stateful adicional; modelagem ABAC exige disciplina de policies versionadas.

## Modos de falha / rollback
Falha: Keycloak down → login indisponível, APIs negam por padrão (deny-by-default), sem bypass. Rollback: configuração de realms versionada em infra repo.

## Fontes
mestre §7; discovery 08 §Identidade; blueprint #06 §4.
