# EVD-004-002 — Fechamento M4: verificador de cadeia (teste), listagens server-side, ops overview, export, runbook

**Data UTC:** 2026-11-02 · **Commit:** control `722b442` · **Base:** Control :5099 (binários 19:07), Mongo rs0, PG view · **Resultado:** PASS

## Verificador de cadeia como TESTE (DoD da SPEC-004) — AuditChainTests 3/3

| Teste | Prova |
|---|---|
| Cadeia_append_e_verificação_válida | 5 appends → VerifyAsync valid=true, count=5 |
| **Tampering_no_registro_quebra_a_cadeia_com_brokenAtSeq** | UPDATE retroativo no Mongo (DENIED→EXECUTED no seq 2) → verify: **valid=false, brokenAtSeq=2, "hash não confere"** — mutação de auditoria É detectada |
| Cadeia_continua_após_recriar_o_store | nova instância do store encadeia no último hash (GENESIS não repete) |

## Listagens server-side (req.5) — smoke real

```text
GET /modules/audit?decision=DENIED&page=1&pageSize=5
  → total=13 | página 1 | seq 1,2,3… (filtro Regex case-insensitive + Skip/Limit)
GET /modules/approvals?page=1&pageSize=3 → total=6 (filtro por State opcional)
GET /modules/inventory-operations/positions?skuContains=m4 → total=1
  → fc1118007e0d4afda7fd… on_hand=-2 ver=3 (view PG, filtro LIKE + paginação)
```

## Visão operacional (req.6) — `/modules/ops/overview` smoke real

```json
{"outbox":{"catalog":{"pending":0},"offers":{"pending":0},
            "inventory":{"pending":0},"payments":{"pending":5}},
 "checkoutSagas":{"4":9,"8":1},   // por estado (int32 do doc)
 "pendingReconciliation":0,
 "dlqDepth":"N/A até M6 (notifications.dlq)"}
```
Leitura read-only de OBSERVABILIDADE — comandos de negócio seguem SEMPRE pelos owners.

## Exportação de trilha (req.3) — smoke real

`GET /modules/audit/export?format=csv` → CSV com header
`seq,occurredAtUtc,actorSub,action,decision,correlationId,prevHash,hash` e todos os
registros citados (formato JSON também suportado). Runbook de revogação de emergência
em `atlas-control-dotnet/docs/runbook-revogacao-emergencia.md`.

## Bug de processo documentado (5º do M4)

CA1305 (ToString sem IFormatProvider) falhou o build warning-as-error DUAS vezes sem
percepção imediata — o binário antigo continuou servindo (`:N` em string também não
formata; só Guid). Lição registrada: **após rebuild, checar timestamp do DLL** antes
de testar comportamento novo.
