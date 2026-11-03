# Runbook — Revogação de emergência de grants e aprovações (SPEC-004)

**Contexto:** vazamento de credencial, desligamento de operador, suspeita de abuso.
**Pré-requisitos:** identidade `iam_admin` no realm atlas (JWT válido); Control acessível; Mongo do Control disponível.

## 1. Revogar grant(s) do sujeito (efeito na PRÓXIMA chamada)

```bash
# listar grants ativos do sujeito (sub do Keycloak OU preferred_username)
curl -s "http://<control>/modules/iam/grants?subject=<SUB>" -H "Authorization: Bearer $IAM_ADMIN_JWT"

# revogar por id (CAS: só revoga se Active e não expirado)
curl -X DELETE "http://<control>/modules/iam/grants/<GRANT_ID>" -H "Authorization: Bearer $IAM_ADMIN_JWT"
```

**Propriedades:** a revogação é revalidada em TRÊS pontos — no request, no decide e no
dispatch (o token v2 emitido NÃO é aceito se o grant morreu entre a aprovação e o despacho).
Tokens em circulação têm TTL de 90s (`Approval:TokenTtlSeconds`) e nonce de uso único.

## 2. Bloquear operação JÁ aprovada e ainda não despachada

Não existe endpoint de cancelamento de approval no M4 (estado Approved aguarda dispatch
com revalidação de grant — o passo 1 já BLOQUEIA o dispatch: `DENIED_GRANT_REVOKED`).
Se for preciso marcar a approval explicitamente, usar o Mongo do Control (control.operation_approvals)
APENAS como medidas de emergência registradas no audit manual.

## 3. Emergência severa (todos os grants de uma action)

```javascript
// mongosh no Mongo do Control — DESLIGAR TODOS os grants de inventory:adjust de uma vez
db.getSiblingDB("<control-db>").control.grants.updateMany(
  { Action: "inventory:adjust", State: "Active" },
  { $set: { State: "Revoked", RevokedBy: "incident-<id>" } }
)
```
Registrar o incidente na trilha manualmente (append de um registro com o mesmo formato —
a cadeia permanece verificável porque cada registro é hash encadeado; NUNCA alterar registros
existentes — o verificador detecta: `GET /modules/audit/verify` → `brokenAtSeq`).

## 4. Verificar e exportar a trilha para o incidente

```bash
curl -s http://<control>/modules/audit/verify            # {"valid":true,"count":N}
curl -s "http://<control>/modules/audit?decision=DENIED&pageSize=200"
curl -s "http://<control>/modules/audit/export?format=csv" -o incident-<id>-trilha.csv
```

## 5. Pós-incidante

1. Emitir NOVOS grants com TTL curto para quem permanece autorizado.
2. Verificar cadeia (deve continuar `valid` — revogações são registros novos, não edições).
3. Revisar `pendingReconciliation` em `GET /modules/ops/overview` (operações UNKNOWN em voo).
4. Fechar incidente com postmortem (link na trilha via correlation_id do incidente).
