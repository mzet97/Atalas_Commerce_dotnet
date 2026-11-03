# ATLAS COMMERCE .NET 10 — M0 REPORT

```text
DATE_UTC: 2026-10-27 (execução) · re-verificação de integridade em 2026-10-28T02:12:01Z (relógio do host)
REPOS / BRANCH / COMMITS:
  atlas-workspace-dotnet (repo auxiliar de coordenação) — branch main — commits registrados em sdd/progress.md
  Repos de produto: NO_REPO (criação é gate M1, por contrato)
MILESTONE: M0 / SPEC-000 — Recon, auditoria e modelagem de negócio
STATUS: PASS (documentação/modelagem — ver escopo e limites abaixo)
```

## SOURCES AUDITED

- **Documentos/diagramas lidos integralmente:** 18 `.md` + 15 fontes de diagrama (12 do discovery + 3 `references/dotnet`).
- **Integridade:** 32/32 arquivos OK contra `SOURCE_MANIFEST_SHA256.json` (EVD-000-001; re-executado ao final — handoff intocado).
- **Classificação:** negócio CONFIRMED; stack Python LEGACY/NOT_APPLICABLE_TO_DOTNET (D-2); versões = snapshot a revalidar (D-3). Divergências D-1..D-4 registradas e tratadas (`sdd/audit.md`, `source-manifest.md`).

## BUSINESS / ADR DECISIONS

- **Confirmadas (herdadas do contrato):** 2 produtos + repo de contratos; ownership único por agregado; Mongo write/PG read; outbox→CDC→Kafka; Rabbit tasks; gRPC schema-first; Keycloak RBAC+ABAC four-eyes; MAF C#; i18n pt-BR/en-US/es-ES; 1P no MVP.
- **Resolvidas no M0:** reconciliação das taxonomias de invariantes (15 INV-* canônicas + alias blueprint); numeração/absorção das ADRs do blueprint na série ADR-0001..0015.
- **Propostas (PROPOSED, aguardam decisão/evidência):** ADR-0001..0015 (15 arquivos em `architecture/decisions/`). Nenhuma marcada ACCEPTED sem aceite.
- **Abertas:** ver `sdd/decisions-open.md` (inclui OPEN-01..06 e pendências de ambiente).

## IMPLEMENTED (artefatos de documentação — não software)

- `atlas-workspace-dotnet/` com `sdd/` completo (10 docs + reports + evidence), `specs/000-business-discovery/` (9 arquivos), `specs/001-platform-foundation/` (rascunho PROPOSTO, 4 arquivos), `architecture/` (15 ADRs PROPOSED, 27 diagramas editáveis + README de diffs, threat-model draft, runbooks placeholder).
- Registro canônico: 15 invariantes + ~60 REQ por contexto + NFR/SEC/AI com IDs imutáveis.
- 12 cenários Gherkin críticos (TEST-000-001..012) cobrindo os 5 cenários críticos exigidos e mais.

## NOT IMPLEMENTED / DEFERRED

- Qualquer scaffold/código/containers/repos de produto (proibido em M0 por contrato §17).
- Coleções Mongo detalhadas de Returns/Reviews/Communications/Analytics (OPEN-01 → specs M6+).
- Validação automatizada de sintaxe Mermaid/PlantUML (sem renderizador no ambiente — revisão manual executada; instalar `mmdc`/plantuml como melhoria do M1).
- Docker/buf/kubectl/helm/skaffold/aspire não instalados (TASK-001-001).

## TESTS EXECUTED

```text
Verificação de integridade SHA-256 (python3, script em EVD-000-001) -> PASS (32/32, exit 0)
Coleta de baseline de ambiente (comandos --version) -> PASS (coleta; ausências registradas)
Testes de software -> NONE (M0 é modelagem; não há código por design)
```

## EVIDENCE

- EVD-000-001 — integridade das fontes (saída integral: `sdd/evidence/integrity-run-2026-10-27.txt`)
- EVD-000-002 — baseline de ambiente (saída integral)
- Commits do repo de coordenação: ver `git log` (branch main).

## SECURITY / RELIABILITY / CQRS

- Verificados **em modelagem**: invariantes INV-* mapeadas a donos/tests/milestones; ameaças STRIDE + AI no threat-model draft; boundary de consentimento da IA especificado (ADR-0009); anti-padrões proibidos documentados (dual write, UI como perímetro, preço por FX de UI).
- **Permanecem riscos (não implementados):** tudo o que exige código — oversell real, idempotência real de webhook, four-eyes real, isolamento de sessão MAF. Riscos monitorados em `sdd/risks.md`.

## PERFORMANCE

`NOT_MEASURED` — nenhuma medição realizada; SLOs permanecem PROPOSED; benchmarks L0–L4 serão gates congelados antes dos testes (M9 e por rotas críticas M2/M3).

## REGRESSIONS / RISKS

- Nenhuma regressão (nada pré-existente foi alterado; handoff re-hashado OK).
- Riscos principais: R-01 (volatilidade MAF), R-03 (Debezium/replica set), R-06 (prompt injection), R-10 (ambiente sem docker/buf) — matriz em `sdd/risks.md`.

## NEXT EXECUTABLE TASK

`TASK-001-001` (READY): instalar/verificar `docker` + `docker compose` e `buf`; aceite = `docker compose version` e `buf --version` com exit 0 (EVD-001-001). Sequência M1 em `specs/001-platform-foundation/tasks.md`.

## CHECKLIST §21 DO CONTRATO (item a item)

1. **Diagramas permanecem editáveis?** Sim — 27 fontes `.mmd`/`.puml` em `architecture/diagrams` (15 cópias + 12 novos), diffs registrados, originais intactos.
2. **Escolhas Python demovidas a LEGACY?** Sim — classificação em audit/source-manifest; tabela de conversões aplicada no design.md §1.
3. **Arquivos .NET são a interpretação prevalente?** Sim — precedência documentada (mestre > references/dotnet > discovery negócio > LEGACY).
4. **Número exato de documentos/diagramas lidos:** 18 documentos `.md` + 15 diagramas (32 arquivos auditados, 32/32 verificados).
5. **SHA de cada fonte:** manifesto original conferido; tabela-resumo em `sdd/source-manifest.md`; hashes individuais no `SOURCE_MANIFEST_SHA256.json` (EVD-000-001).
6. **Decisões confirmadas/abertas:** seção acima + `decisions-open.md`.
7. **Arquitetura atual vs pretendida:** atual = `NO_REPO` (nenhum software); pretendida = `specs/000/design.md` + ADRs + C4.
8. **Risco de licença:** MassTransit/MediatR/FluentAssertions não adotados; baseline sem elas (ADR-0012); verificação formal no M1.
9. **Baseline .NET 10 SDK:** presente — SDK 10.0.403 (coincide com o snapshot do contrato); SDK 8.0.427 adicional listado.
10. **Pacote MAF verificado:** baseline documental `Microsoft.Agents.AI`/`Workflows` 1.26.0 (NuGet 2026-10-23); **restore/smoke não executado ainda** (será a primeira prova no M1/M7 — nunca antes de pinar).
11. **First vertical slice em Gherkin:** `acceptance.feature` Features 1–2 + TEST-000-012 (Admin→Control→owners→projeção→Next) e demais cenários.
12. **TASK pronta com comando esperado:** TASK-001-001 (acima).

**Declaração de honestidade:** nada neste relatório afirma software implementado, testado, escalável ou seguro. `PASS` refere-se exclusivamente aos artefatos de auditoria/modelagem do M0, verificáveis pelos EVDs citados.
