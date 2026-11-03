# Contracts Draft (Estágio A)

Rascunhos **declarativos** dos contratos wire, criados na fase de documentação completa. Serão promovidos ao repo `atlas-platform-contracts-dotnet` no M1 e passam a ser a fonte canônica com gates de CI (`buf lint/breaking`, OpenAPI diff).

## Conteúdo e validação (2026-10-28)

| Arquivo | Conteúdo | Validação |
|---|---|---|
| `proto/atlas/catalog/v1/catalog_owner.proto` | Control→Catalog (UpsertDraft, PublishProduct com approval_token) | `buf lint` PASS (buf 1.75.0) |
| `proto/atlas/inventory/v1/inventory_owner.proto` | ReceiveStock/AdjustStock/ReserveStock(CAS+idem)/Confirm/Release | `buf lint` PASS |
| `proto/atlas/offers/v1/offers_owner.proto` | PublishOffer (escopo único ativo) / GetActiveOffer | `buf lint` PASS |
| `openapi/gateway-storefront.yaml` | Borda BFF: busca, PDP, quote, checkout idempotente | parse YAML OK (OpenAPI 3.1) |
| `openapi/control-admin.yaml` | Borda Control: drafts, approvals four-eyes, inventário, auditoria | parse YAML OK |
| `asyncapi/atlas-events.yaml` | Tópicos `atlas.{context}.v1` (ADR-0018) | parse YAML OK (AsyncAPI 3.0) |
| `jsonschema/domain-event.schema.json` | Envelope INV-EVT-001 (event_id/type/version/correlation/traceparent) | JSON válido; regex event_type testada |
| `buf.yaml` | módulo `proto` com lint STANDARD + breaking FILE | usado na validação |

## Regras de evolução

- Mudanças breaking exigem versão nova (v2) + período de dupla publicação; `buf breaking` contra o commit anterior é gate de CI no M1+.
- `service` sufixo `Service`; request/response únicos por RPC (padrão buf STANDARD).
- Envelope de evento é imutável em v1; adição = `schema_version+1` compatível.
