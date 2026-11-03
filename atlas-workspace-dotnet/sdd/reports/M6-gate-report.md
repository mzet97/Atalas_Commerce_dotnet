# ATLAS COMMERCE .NET — M6 GATE REPORT

```text
DATE_UTC: 2026-11-03
REPOS / COMMITS:
  atlas-storefront-dotnet — e6a8556 (notifications) → 4de70b1 (F+R) → 8efb96c (fechamento)
  atlas-control-dotnet    — 89b4b50 (RefundApprovalsModule four-eyes)
  atlas-workspace-dotnet  — (este commit: EVD-006-001 + SPEC-006 READY)
MILESTONE: M6 / SPEC-006 — Fulfillment, Returns/Refunds, Notifications (RabbitMQ)
STATUS: **PASS** (gate M6; pendência externa única: git push → CI)
```

## Gate M6 (contrato/roadmap) — item a item

| Critério | Evidência | Status |
|---|---|---|
| Refund coerente com pagamento/pedido | Payments refund: só intent **CAPTURED** refunda (409 em Created/Unknown/Refunded); four-eyes token HMAC validado no owner (403 sem token; 409 self-approval); idempotência por key (repetida → already-processed); falha PSP → RefundPending 202 | **PASS** |
| Nunca sem RMA aprovado | token emitido SOMENTE via Control pós-RMA (role approver ≠ requester) | **PASS** |
| Notificações nunca duplicadas | dedup por notification_key (índice único Mongo): 8 concorrentes → **1** envio; persiste após restart | **PASS** |
| DLQ/retry demonstrados | poison → tentativas 1-2 na fila retry (quorum, TTL 3s) → 3ª → **notifications.dlq** observável; boa mensagem entregue 1× no mesmo fluxo | **PASS** |
| State machine fulfillment | Picking→Packed→Shipped→Delivered; fora de ordem → 409; tracking determinístico; cancel fora da janela → 409 | **PASS** (repetição exata = 409 CAS — no-op fino na SPEC-006+) |
| Runbook DLQ/replay | runbook em EVD-006-001 §5 (requeue da DLQ + dedup evita dupla entrega) | **PASS** |

## TESTS EXECUTED

```text
Notifications.Tests (REAL: RabbitMQ quorum + Mongo replica set): 3/3
Payments.Api.Tests: 6/6 · Storefront total: 147/147 (18 projetos)
Build: 48 projetos storefront + 11 control, 0 errors
E2E refund REAL: 403→token(409 self)→token ok→200 refunded→idem already-processed
E2E fulfillment REAL: start→Packed→Shipped→Delivered + 3 rejeições
PERFORMANCE: NOT_MEASURED (M9)
```

## Infraestrutura nova

RabbitMQ 4.0-management no Podman (:5672 quorum / :15672 UI) — atlas-rabbit.

## NEXT EXECUTABLE TASK

**M7/SPEC-007 — MAF Shopping Assistant (SPIKE Ollama primeiro, ADR-0008)**.
Fase 3 do plano aprovado. Pendência externa persistente: git push → CI.
