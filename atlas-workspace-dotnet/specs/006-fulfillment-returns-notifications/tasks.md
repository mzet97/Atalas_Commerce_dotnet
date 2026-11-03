# SPEC-006 — tasks.md (DRAFT)

| ID | Tarefa | Aceite | Estado |
|---|---|---|---|
| TASK-006-001 | Carrier fake adapter (quote/label/tracking/POD determinísticos) | cenários reproduzíveis | TODO |
| TASK-006-002 | Fulfillment Service (state machine + multi-remessa) | transições + eventos corretos | TODO |
| TASK-006-003 | Returns Service (RMA + four-eyes + refund idempotente) | refund sem duplicação | TODO |
| TASK-006-004 | Notifications (bridge, templates i18n, ack/retry/DLQ) | poison message vai à DLQ e é replayável | TODO |
| TASK-006-005 | Timeline do cliente (order→delivery→return) | E2E com estados corretos | TODO |
| TASK-006-006 | Fault tests (broker restart, crash pós-ack parcial) | sem perda/duplicação | TODO |
| TASK-006-007 | Runbook DLQ/replay + relatório M6 | gate respondido com EVD | TODO |
