# SDD — Rastreabilidade Global

Formato: requisito → spec/design → task → teste → evidência → status. Detalhe por spec em `specs/*/traceability.md`. Tabela-resumo (atualizada a cada milestone):

| Requisito | SPEC/Design | TASK | TEST | EVD | Status |
|---|---|---|---|---|---|
| Integridade das fontes (auditoria) | SPEC-000/audit | TASK-000-001 | — (verificação direta) | EVD-000-001 | PASS |
| Contrato mestre versionado | sdd/prompt.md | TASK-000-002 | — | EVD-000-001 | PASS |
| Baseline de ambiente | SPEC-000/audit §2 | TASK-000-003 | — | EVD-000-002 | PASS |
| Glossário/linguagem ubíqua | SPEC-000/spec §0 | TASK-000-004 | revisão cruzada | EVD-000-001 | PASS |
| Registro de requisitos + INV reconciliadas | SPEC-000/spec §3 | TASK-000-005 | mapeamento alias completo | EVD-000-001 | PASS |
| 15 ADRs obrigatórias (§19) | architecture/decisions | TASK-000-006 | cobertura §19 | — | PASS (PROPOSED) |
| Diagramas editáveis (§3.3) | architecture/diagrams | TASK-000-007/008 | sintaxe revisada manualmente | — | PASS (doc) |
| Threat model draft | architecture/threat-model | TASK-000-009 | STRIDE por contexto | — | PASS (draft) |
| Cenários críticos ≥5 | SPEC-000/acceptance | TASK-000-010 | TEST-000-001..012 (a automatizar M2–M7) | — | PASS (contrato) |
| SPEC-001 proposta | specs/001 | TASK-000-011 | rascunho com gates M1 | — | PASS (PROPOSTO) |
| Roadmap/progresso | sdd/roadmap+progress | TASK-000-012 | M0–M11 ↔ SPECs | — | PASS |
| INV-STOCK-001/003 (oversell=0) | SPEC-000/design §5; ADR-0002 | (M2) TEST-000-001/002 | SPEC-002 | pendente (M2) |
| INV-PAYMENT-001 (idempotência financeira) | ADR-0006; diag. 12/17 | (M3) TEST-000-005/006 | SPEC-003 | pendente (M3) |
| INV-ADMIN-001 (four-eyes) | ADR-0007; diag. 14 | (M4) TEST-000-007 | SPEC-004 | pendente (M4) |
| INV-AI-001 (boundary de consentimento) | ADR-0008/0009/0015 | (M7) TEST-000-009/010 | SPEC-007 | pendente (M7) |
| INV-EVT-001 (projeção idempotente) | ADR-0003; diag. 18 | (M2) TEST-000-011 | SPEC-002 | pendente (M2) |
| NFR-* (SLOs/benchmarks) | roadmap; audit | (M8/M9) | SPEC-008/009 | NOT_MEASURED |
| Estágio A: SPEC-001 READY | specs/001 (9 arquivos) | TASK-000-011 | revisão + buf lint | EVD-000-003 | PASS (doc) |
| Estágio A: SPEC-002..011 DRAFT | specs/002..011 (9 arquivos cada; 010 esqueleto DEFERRED) | — | checagem de consistência (relatório Estágio A) | — | PASS (doc) |
| Estágio A: ADR-0016..0019 | architecture/decisions | — | cobertura de lacunas | — | PROPOSED |
| Estágio A: contracts-draft (7 artefatos) | contracts-draft/ | — | `buf lint` PASS + YAML/JSON parse | EVD Estágio A | PASS |

**Regra:** linha só muda de status com EVD real; "PASS (contrato)/(doc)" refere-se a artefatos de documentação verificados, jamais a software.
