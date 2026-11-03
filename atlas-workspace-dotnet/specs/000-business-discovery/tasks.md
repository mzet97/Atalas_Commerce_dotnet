# SPEC-000 — tasks.md

TASK-000-* · Estados SDD · M0 = documentação/modelagem (nenhum scaffold). Comando de aceite = verificação executável real.

| ID | Tarefa | Arquivos | Pré-cond. | Comando de aceite | Estado |
|---|---|---|---|---|---|
| TASK-000-001 | Inventariar workspace e classificar fontes | `sdd/audit.md`, `sdd/source-manifest.md` | — | verificar 32/32 hashes vs manifesto (EVD-000-001) | PASS |
| TASK-000-002 | Versionar contrato mestre no repo de coordenação | `sdd/prompt.md` | 001 | `sha256sum` confere corpo do original (header de provenança documentado) | PASS |
| TASK-000-003 | Coletar baseline de ambiente | `sdd/evidence/EVD-000-002` | — | comandos de versão executados com saída anexada | PASS |
| TASK-000-004 | Publicar glossário (linguagem ubíqua) | `sdd/glossary.md` | 001 | termos dos 8 docs mapeados; revisão cruzada com blueprint #01 §6 | PASS |
| TASK-000-005 | Reconciliar taxonomias de invariantes e publicar registro de requisitos | `sdd/requirements.md` | 004 | tabela alias blueprint↔canônica cobre INV-001..012 | PASS |
| TASK-000-006 | Autorar 15 ADRs PROPOSED | `architecture/decisions/ADR-0001..0015` | 005 | cada ADR ≥2 alternativas + trade-offs + rollback; cobre §19 do mestre | PASS |
| TASK-000-007 | Diagramas: cópias com diffs registrados | `architecture/diagrams/*` + README | 001 | diffs LEGACY registrados; originais intactos (re-hash) | PASS |
| TASK-000-008 | Diagramas: 12 novos da edição .NET | idem | 007 | cobrem §3.3 do mestre (states, sequences, C4, deployment, ER IA) | PASS |
| TASK-000-009 | Modelo de ameaças draft | `architecture/threat-model.md` | 008 | STRIDE por contexto + ameaças AI | PASS |
| TASK-000-010 | SPEC-000 completa (spec/design/tasks/acceptance/traceability/evidence/risks) | `specs/000-business-discovery/*` | 005–009 | 9 arquivos coerentes com fontes | PASS |
| TASK-000-011 | SPEC-001 proposta (rascunho) | `specs/001-platform-foundation/*` | 010 | drafts PROPOSTO com gates do M1 | PASS |
| TASK-000-012 | Roadmap, rastreabilidade e progresso | `sdd/{roadmap,traceability,progress}.md` | 010 | M0–M11 mapeados a SPECs; REQ→EVD ligados | PASS |
| TASK-000-013 | Evidências M0 + relatório final | `sdd/evidence/*`, `sdd/reports/M0-report.md` | 012 | checklist §21 respondido; sem alegação não verificada | PASS |
| TASK-000-014 | Validação final: handoff intocado + consistência | — | 013 | re-hash 32/32 do handoff; cruzamento INV↔REQ↔cenário | PASS |

Estimativas relativas e rollback: tarefas de documentação; rollback = git revert no repo de coordenação. Owner: agente implementador sob contrato `sdd/prompt.md`.
