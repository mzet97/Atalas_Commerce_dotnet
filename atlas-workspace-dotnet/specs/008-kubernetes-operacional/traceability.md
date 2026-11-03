# SPEC-008 — traceability.md / evidence.md / risks.md / audit.md

## traceability
| Requisito | Design | Task | Test |
|---|---|---|---|
| Compose→Helm parity | §Charts | TASK-008-002 | helm lint/template CI |
| Políticas/segurança cluster | §Namespaces | TASK-008-003/006 | políticas aplicadas |
| Escalonamento | §KEDA/HPA | TASK-008-005 | eventos de escala |
| RTO/RPO (NFR-REL-004) | §Restore drill | TASK-008-007 | restore com pedido legível |
