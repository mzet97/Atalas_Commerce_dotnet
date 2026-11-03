# SPEC-000 — Business Discovery

**Milestone:** M0 · **Status:** IN_PROGRESS (documentação completa; gates verificados no `sdd/reports/M0-report.md`) · **Owner:** coordenação (workspace)

## Como ler

| Arquivo | Papel |
|---|---|
| `spec.md` | O problema de negócio: visão, contexts, ownership, agregados, invariantes, casos de uso, escopo |
| `design.md` | O desenho técnico alvo .NET: conversões LEGACY→.NET, CQRS, contratos, comunicação |
| `tasks.md` | TASK-000-* com comando de aceite |
| `acceptance.feature` | Cenários Given/When/Then críticos (vinculados a TEST-000-*) |
| `audit.md` | Auditoria das fontes desta spec |
| `traceability.md` | REQ → diagrama → contrato → teste → evidência |
| `evidence.md` | Evidências reais desta spec (EVD-000-*) |
| `risks.md` | Riscos específicos da modelagem |

Fontes primárias: `atlas-commerce-blueprint.md` (consolida 8 docs), `atlas-commerce-discovery/docs/01..08`, `references/dotnet/*`. Precedência definida em `sdd/source-manifest.md` e `README` do workspace.

**Sem implementação de software nesta spec** — M0 é modelagem e contrato (mestre §17).
