# SPEC-000 — evidence.md

Evidências **reais** desta spec (comandos executados, saídas, ambiente). Sem alegação de teste de software — M0 é modelagem.

| EVD | Conteúdo | Comando | Resultado | Data (UTC) |
|---|---|---|---|---|
| EVD-000-001 | Integridade das 32 fontes vs `SOURCE_MANIFEST_SHA256.json` | ver `sdd/evidence/EVD-000-001-source-integrity.md` | PASS 32/32 | 2026-10-27 |
| EVD-000-002 | Baseline de ambiente (dotnet/node/pnpm/docker/…) | ver `sdd/evidence/EVD-000-002-environment-baseline.md` | PASS (coleta); docker/kubectl/helm/skaffold/buf/aspire AUSENTES | 2026-10-27 |

Notas de honestidade: nenhum build/teste de produto executado (não há código — por design do M0); Gherkin do `acceptance.feature` é **contrato futuro**, vinculado a TEST-000-* a automatizar nos milestones M2/M3/M4/M7; PERFORMANCE: `NOT_MEASURED`.
