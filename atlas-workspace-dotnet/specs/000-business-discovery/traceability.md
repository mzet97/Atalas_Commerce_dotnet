# SPEC-000 — traceability.md

REQ → diagrama/contrato → TASK → TEST → EVD. Estado inicial M0: código = N/A (sem implementação). Testes automatizados nascem nos milestones indicados.

| Requisito | Design/Contrato | Task | Test | EVD | Milestone |
|---|---|---|---|---|---|
| INV-STOCK-001 (oversell=0) | agregado Inventory + CAS; diag. 07/13/16 | TASK-000-005 | TEST-000-001 | EVD-000-001 | M2 |
| INV-STOCK-002 (ledger) | StockMovement append-only | TASK-000-005 | TEST-M2 (a definir em SPEC-002) | EVD-000-001 | M2 |
| INV-STOCK-003 (reserva idempotente) | índice único idempotency; diag. 16 | TASK-000-005 | TEST-000-002 | EVD-000-001 | M2 |
| INV-ORDER-001/002 (snapshot + estados) | agregado Order; diag. 06/08 | TASK-000-005 | TEST-M3 (SPEC-003) | EVD-000-001 | M3 |
| INV-MONEY-001 | VO Money (requirements §1) | TASK-000-004/005 | TEST-M3 (SPEC-003) | EVD-000-001 | M3 |
| INV-PAYMENT-001 | ADR-0006; diag. 12/17; PSP fake | TASK-000-006 | TEST-000-005/006 | EVD-000-001 | M3 |
| INV-CQRS-001 | ADR-0002/0003; diag. 09/16 | TASK-000-006 | TEST-000-003 (revalidação) | EVD-000-001 | M2/M3 |
| INV-SEC-001 | ADR-0007 | TASK-000-006 | TEST-000-008 | EVD-000-001 | M2/M4 |
| INV-ADMIN-001 (four-eyes) | ADR-0001/0007; diag. 14 | TASK-000-006 | TEST-000-007 | EVD-000-001 | M4 |
| INV-AI-001 | ADR-0008/0009/0015; diag. 19 | TASK-000-006 | TEST-000-009/010 | EVD-000-001 | M7 |
| INV-QUOTE-001 | agregado PriceQuote (UC-002/003) | TASK-000-005 | TEST-M3 (COM-08) | EVD-000-001 | M3 |
| INV-EVT-001 (envelope + projeção idempotente) | ADR-0003; diag. 09/18 | TASK-000-006 | TEST-000-011 | EVD-000-001 | M2 |
| INV-PII-001 | ADR-0015; threat-model §3 | TASK-000-009 | TEST-M7 | EVD-000-001 | M7 |
| INV-I18N-001 | requirements §1; discovery 08 | TASK-000-005 | TEST-M5 (CAT-01) | EVD-000-001 | M5 |
| REQ-CAT-002/003 (publicação) | ADR-0001; diag. 15 | TASK-000-008 | TEST-000-003/004 | EVD-000-001 | M2 |
| REQ-ADM-001/004 (ownership + lag visível) | ADR-0001/0002 | TASK-000-006 | TEST-000-012 | EVD-000-001 | M2/M4 |
| REQ-PLT-001/002 (outbox/inbox) | ADR-0003; diag. 18 | TASK-000-006 | TEST-000-011 | EVD-000-001 | M2 |
| REQ-AI-002..008 | ADR-0008/0009/0015; diag. 19 | TASK-000-006 | TEST-000-009/010 (+M7) | EVD-000-001 | M7 |
| NFR-* / SEC-* / SLOs | roadmap gates; audit | TASK-000-012 | TEST-M8/M9 | EVD-000-002 | M5/M8/M9 |

Regras: IDs imutáveis; novos TEST/EVD criados nas specs dos respectivos milestones (SPEC-002..011); toda linha ganha commit quando o teste existir de verdade.
