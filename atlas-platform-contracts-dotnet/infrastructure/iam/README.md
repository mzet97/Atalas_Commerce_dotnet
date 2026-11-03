# IAM — Keycloak (draft)

Realm `atlas` (draft completo com clients/roles/MFA na TASK-001-007/008, exige Docker):
- clients: `storefront-next` (public, PKCE), `control-angular` (public, PKCE), `atlas-services` (confidential).
- roles: customer + perfis admin do glossário (catalog_editor, pricing_manager, inventory_operator, fulfillment_operator, finance_analyst, support_agent, auditor, iam_admin).
- MFA obrigatório para roles admin; tokens curtos; ADR-0007.

Execução/validação BLOCKED até Docker (EVD-000-003).
