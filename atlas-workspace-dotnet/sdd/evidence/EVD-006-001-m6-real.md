# EVD-006-001 — M6 REAL: notificações quorum+DLQ, fulfillment state machine, refund four-eyes

**Data UTC:** 2026-11-03 · **Commits:** storefront `e6a8556`/`4de70b1`/`8efb96c` · control `89b4b50` · **Infra:** RabbitMQ 4.0 (quorum, :5672), Mongo replica set, PSP fake, Keycloak

## 1. Notificações (req.3) — testes integração REAL 3/3 (RabbitMQ+Mongo)

| Teste | Prova |
|---|---|
| dedup concorrente | 8 workers disputam notification_key → **exatamente 1 envia** (índice único) |
| poison→DLQ | veneno: tentativas 1,2 → fila retry (TTL 3s, backoff) → 3ª tentativa → **notifications.dlq** |
| dedup persistente | restart do worker → claim repetida → False (nada re-envia) |

## 2. Fulfillment (req.1/4) — smoke E2E real :5294

```text
start  → Picking  tracking=ATLAS-4C23EDA3 (determinístico {orderId:N8})
advance Packed → Shipped → Delivered            (caminho linear ok)
Shipped repetido → 409 conflito de versão (CAS) ← ajuste fino p/ no-op: SPEC-006+
Picking após Shipped → 409 transição inválida   (req.4 fora de ordem rejeitado)
cancel pós-expedição → 409 fora da janela        (req.4 janela)
```

## 3. Refund four-eyes (req.2, REQ-RET-001) — E2E real cross-repo

```text
POST /admin/refunds SEM token      → 403 approval_token rejeitado: ausente (auditado)
POST Control /token (approver=requester) → 409 four-eyes violado (req p/ 409,审计)
POST Control /token (approver≠requester) → approvalToken HMAC {sub,order_id,amount_minor,exp}
POST Payments /admin/refunds com token   → 200 {"status":"refunded","intentState":"Refunded"}
POST Payments MESMA idempotencyKey       → 200 already-processed (T02 refund)
intent não capturado → 409 (nunca refunda sem CAPTURED — coerência INV-PAYMENT-001)
```

## 4. Bugs reais do ciclo (4)

1. RabbitMQ.Client 6→7 (API async/IChannel) — código já escrito em v7
2. DLQ declarada mas NUNCA BINDADA ao exchange (dead letters sumiam) — teste DLQ pegou
3. Control.RefundApprovalToken com using fragmentado (JsonSerialization) — arquivo reescrito
4. índice unique sobre _id no Mongo = especificação inválida (Fulfillment)

## Limitações honestas

- NotificationsWorker consome atlas.orders.v1 (Kafka) mas o E2E Kafka→Rabbit completo fica
  como smoke manual (os 3 aceites do gate cobertos por testes integração direta)
- refund idempotência in-memory (store persistente: SPEC-006+); PSP fake sempre REFUNDED
- fulfillment sem projection para view (timeline do cliente via API direta no M6)
