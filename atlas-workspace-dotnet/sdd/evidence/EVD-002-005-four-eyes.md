# EVD-002-005 — Four-eyes ponta a ponta: Control → token HMAC → owner, com 403/409 auditados

**Data UTC:** 2026-10-29 · **Commits:** storefront (ApprovalToken + owner validation), control `36a3c3d` · **Resultado:** PASS (3/3 Control + 6/6 Catalog.Api)

## Fluxo provado (teste de integração REAL — Keycloak 26.0.7 + MongoDB replica set)

| Passo | Ação | Resultado |
|---|---|---|
| Setup | Admin API cria clients de teste: atlas-editor-dev (roles catalog_editor+approver), atlas-approver-dev (approver), atlas-nobody-dev (sem roles); tokens client_credentials reais | ✓ |
| 1 | **editor** solicita approval (201) + audit CREATED | ✓ |
| 2 | **mesmo principal** decide → **409** self-approval + audit `DENIED_SELF_APPROVAL` | ✓ (domínio; INV-ADMIN-001) |
| 2b | principal **sem role** decide → **403** + audit `DENIED_403` (policy antes do domínio) | ✓ |
| 3 | **approver distinto** decide APPROVED → approval_token HMAC emitido | ✓ |
| 4 | principal sem role **dispatch** → **403** + audit | ✓ |
| 5 | approver **dispatch** → owner (fake no teste do Control; real provado nos 6 testes do Catalog) → **Executed** + audit EXECUTED | ✓ |

## Componentes novos

- **Control**: `Approval` aggregate (Hydrate/Create, invariantes de transição, `SelfApprovalException`), `ApprovalsStore` (Mongo **próprio** do Control: `control.approvals` + `control.audit` — ADR-0001), `ApprovalTokenIssuer` (HMAC-SHA256; mesma convenção do owner — compat cruzada garantida por ambos lados testados; consolidar no pacote de contratos no release), `ApprovalsModule` com **policies de role reais** (JWT Keycloak: `MapInboundClaims=false` + transform `realm_access.roles`→roles), middleware de **403 auditado** posicionado ANTES do `UseAuthorization` (o Forbid faz short-circuit e não chama next — causa raiz documentada), `GrpcCatalogOwnerDispatcher` (falha do owner → FAILED auditável, não crash).
- **Storefront (owner)**: `ApprovalToken` (Issue/Validate/TryValidate com HMAC + exp + product_id + expected_version) — o owner **revalida** a aprovação (nunca bypass do Control).

## Diagnósticos reais do ciclo (fatos registrados)

1. `$"{body}.{sig}"` com `sig` `byte[]` produzia literal `"System.Byte[]"` → token com 3 partes (achado por probe de roundtrip).
2. JSON sem `JsonPropertyName` → `exp` desserializava 0 → todo token "expirado".
3. JwtBearer default mapeia `sub`→NameIdentifier → `FindFirst("sub")`=null → **editor e approver viravam o mesmo principal** "anonymous" (self-approval falso). Fix: `MapInboundClaims=false`.
4. `UseAuthorization` **short-circuita no Forbid** (não chama next) → middleware de auditoria 403 precisou ficar ANTES dele (log em arquivo `/tmp/audit-mw.log` como evidência da ordem).
5. Índice parcial Mongo (só ofertas ativas) + corrida 8-way → 1 vencedor (ciclo anterior).
6. Keycloak Admin API: criação de service-account é assíncrona (retry); GET `/secret` retornou 404 mesmo com client existente → setup usa secret conhecido do próprio POST de criação.

## Testes

- Control: **5 arquitetura** (regra refinada: Mongo.Driver permitido APENAS p/ estado próprio; brokers/caches dos owners continuam banidos) + **3 four-eyes** = 8 PASS.
- Catalog.Api: **6 PASS** (incluindo token válido/alien/tamperado/expirado).
- Storefront total: **74 testes** (68 anteriores + 6).

## Limitações honestas

- Dispatch real do Control→gRPC→Catalog: metades provadas separadamente (Control com owner fake; owner com tokens reais). **Smoke cross-process completo (Control host → Catalog host rodando) pendente para o close do gate M2** — roteiro já definido (subir os 2 hosts + Keycloak e executar o fluxo por curl).
- 403 do middleware audita com `sub` do JWT; hash-chain da auditoria é SPEC-004.
