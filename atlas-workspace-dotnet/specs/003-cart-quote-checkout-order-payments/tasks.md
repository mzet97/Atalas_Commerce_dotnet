# SPEC-003 — tasks.md (DRAFT)

| ID | Tarefa | Pré-cond | Aceite | Estado |
|---|---|---|---|---|
| TASK-003-001 | Money VO + property-based (FsCheck) + arredondamento por moeda | M2 | propriedades verdes (soma/associação/rounding) | TODO |
| TASK-003-002 | Cart Service (aggregate, APIs, merge guest) | 001 | testes unit+integration verdes | TODO |
| TASK-003-003 | QuoteService (Strategy pricing/frete/tributo v1, TTL, versão) | 001 | quote vencida → requote com diff (COM-08) | TODO |
| TASK-003-004 | PSP fake (cenários determinísticos + HMAC + webhook) | — | cada cenário reproduzível por token; assinatura validada | TODO |
| TASK-003-005 | Payments Service (intents/attempts, dedup webhook) | 004 | webhook duplicado/fora de ordem não avança 2x | TODO |
| TASK-003-006 | Orders Service (snapshot + state machine idempotente) | 001 | transições duplicadas rejeitadas | TODO |
| TASK-003-007 | Checkout saga persistida + compensações + reconciliador | 002-006 | T01..T05 PASS em integration | TODO |
| TASK-003-008 | Projeções de pedidos + funil | 007 | meus pedidos/detail corretos pós-eventos | TODO |
| TASK-003-009 | E2E Playwright compra (APPROVED/DECLINED/TIMEOUT) | 007-008 | vídeos/traces anexados | TODO |
| TASK-003-010 | Fault injection (kill worker, restart) + relatório M3 | docker | sem perda silenciosa; relatório §16 | TODO (docker p/ harness) |
