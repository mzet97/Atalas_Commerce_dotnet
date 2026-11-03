# SPEC-001 — traceability.md

| Requisito | Design/Contrato | Task | Test | EVD | Status |
|---|---|---|---|---|---|
| REQ-PLT-100 (3 repos) | design.md §Estrutura | TASK-001-003/005/006 | TEST-001-001 | EVD-001-* (a criar) | TODO |
| REQ-PLT-101 (templates) | design.md §Templates | TASK-001-004 | TEST-001-005 | EVD-001-* | TODO |
| REQ-PLT-102 (svc+host) | design.md §Pontos | TASK-001-005/006 | TEST-001-002 | EVD-001-* | TODO |
| REQ-PLT-103 (Compose) | design.md §Compose | TASK-001-007 | smoke health | EVD-001-* | BLOCKED (docker) |
| REQ-PLT-104 (CI) | design.md §CI (GitHub Actions) | TASK-001-010 | TEST-001-001 | EVD-001-* | TODO |
| REQ-PLT-105 (contratos/buf) | ADR-0005; buf 1.75.0 | TASK-001-003 | `buf lint` no CI | EVD-000-003 | buf OK |
| REQ-PLT-106 (trace cross) | design.md §OTel | TASK-001-009 | TEST-001-004 | EVD-001-* | BLOCKED (docker) |
| REQ-PLT-107 (OIDC smoke) | design.md §Keycloak | TASK-001-008 | TEST-001-003 | EVD-001-* | BLOCKED (docker) |
| REQ-PLT-108 (dependency-matrix) | mestre §6.2 | TASK-001-002 | revisão de fontes | EVD-001-* | TODO |
| REQ-PLT-109 (frontends base) | design.md | TASK-001-011 | builds verde | EVD-001-* | TODO |
