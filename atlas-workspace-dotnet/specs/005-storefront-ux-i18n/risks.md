# SPEC-005 — risks.md

| Risco | P | I | Mitigação |
|---|---|---|---|
| CWV degradar com facetas pesadas | Média | Médio | índices GIN + cache + paginação server-side; orçamento no CI |
| Traduções incompletas quebrarem jornadas | Média | Médio | fallback explícito + CAT-01 automatizado |
| A11y regredir com novos componentes | Média | Médio | axe no CI + revisão em PR |
| URL por locale×market explodir sitemap | Baixa | Médio | sitemap paginado por market |
