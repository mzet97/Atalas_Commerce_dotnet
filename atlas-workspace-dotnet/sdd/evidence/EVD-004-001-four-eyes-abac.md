# EVD-004-001 — Four-eyes E2E com identidades Keycloak reais + grants ABAC + trilha encadeada + lag CQRS

**Data UTC:** 2026-11-02 · **Commits:** contracts `bfa5430` · storefront `420ff4c`+workers · control `6db256e`+`d22a761` · **Base:** Keycloak 26.0.7 :8090 (realm atlas reimportado), Control :5099, Inventory :5291/5292, workers (dispatch→Kafka→PG) · **Resultado:** PASS

## Arquitetura da fatia (SPEC-004 req.1–4)

- **Kernel compartilhado** (`Atlas.Control.Modules.Kernel`, DAG módulos→kernel preservado com
  NOVA regra de arquitetura): `Grant` (ABAC com expiração/revogação), `OperationApproval`
  (v2), `StockTokenIssuer` (HMAC espelho do owner), `AuditChainStore` (hash encadeado).
- **Owner REVALIDA** (INV-SEC-001): `StockApprovalToken` v2 `{sub, action, subject, exp, jti}`
  validado no `AdjustStock` gRPC — **antes do M4 qualquer um ajustava estoque sem aprovação**.
- **Fluxo**: request (grant do operador) → decide (self-approval=409 + grant do approver) →
  dispatch (grant AINDA ativo + nonce queimado ANTES + owner revalida token) → Executed/Failed.

## Fluxo real executado (identidades Keycloak reais — password grant client atlas-services)

| # | Passo | Resultado | Auditoria (seq) |
|---|---|---|---|
| 1 | request SEM grant (inv-op) | **403** audited | DENIED_NO_GRANT |
| 2 | grant inv:adjust wh W (atlas-admin→inv-op) | 201 | iam.grant.created |
| 3 | request COM grant | 201 approvalId | CREATED (seq 15) |
| 4 | **self-approval** (atlas-admin decide o próprio request) | **409** | DENIED_SELF_APPROVAL (seq 14) |
| 5 | approve distinto | 200 + approval_token v2 | APPROVED (seq 16) |
| 6 | **dispatch** → gRPC AdjustStock | **200 Executed, positionVersion=3** | EXECUTED (seq 17) |
| 7 | dispatch de novo | **409** | DENIED_INVALID_STATE (seq 18) |
| 8 | revoke grant (iam_admin) | 200 revoked | REVOKED (seq 22) |
| 9 | request pós-revogação | **403** — **revogação reflete na próxima chamada** | DENIED_NO_GRANT (seq 23) |
| 10 | 403 de policy (middleware) também entra na cadeia | — | http.denied (seq 24) |

**Trilha encadeada**: `GET /modules/audit/verify` → `{"valid":true,"count":24}` — re-hash
SHA256(prev|seq|payload) de cada registro + encadeamento prev→hash verificados.

**Lag CQRS real** (`/modules/inventory-operations/cqrs-lag`):
```json
{"ownerHeadVersion":3,"projectedSourceVersion":3,"projectedAgeSeconds":81,"lag":0}
```
owner head via gRPC `GetStockVersion` (NOVO RPC, buf PASS) × source_version projetada (PG) —
eventos Stock.AdjustedV1 agora carregam a VERSION da posição (outbox transacional) e o
projector aplica GREATEST(@ver).

## Bugs reais pegos na fatia (4)

1. **Dispatcher único do workers apontava atlas_catalog** — o outbox de inventory (db próprio)
   NUNCA foi despachado em modo live (M2 provou projeção de inventory apenas em teste inline).
   Diagnóstico: pendentes=11 no Mongo × despachados=0 → `db=atlas_catalog` no log. Fix: um
   dispatcher por owner db.
2. **AdjustStock sem approval_token** (owner aceitava ajuste de QUALQUER chamada gRPC).
3. **Escopo `basic` ausente** no client atlas-services do realm → access token SEM `sub` →
   grants com subject vazio (500). Fix no realm vivo + realm-atlas.json + fallback
   `preferred_username` no módulo.
4. StockApprovalToken.Issue esqueceu o base64 do body (JSON com "inventory.adjust" continha
   um ponto → "formato (3 partes)") — pego pelo teste gRPC.

## Testes automatizados

- Contracts 8/8 + buf lint (RPC GetStockVersion aditivo)
- Inventory.Api gRPC 2/2: sem token→PermissionDenied, token válido aplica, subject errado→negado, domínio FailedPrecondition preservado, head version exposto
- Control: Architecture 7/7 (nova regra kernel) · Host.Tests 3/3 (four-eyes M2 na cadeia única)

## Limitações honestas

- Nonce uso-único enforcement no CONTROL; o owner valida assinatura/exp/action/subject mas não
  consulta nonces (SPEC-004+ com IAM real). Token TTL 90s mitigam replay.
- Password grant usado APENAS para o E2E (client atlas-services dev); UI real usa PKCE (M1 provado).
- Dispatch single-instância (lock distribuído na SPEC-004+).
