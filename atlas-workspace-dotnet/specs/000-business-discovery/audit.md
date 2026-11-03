# SPEC-000 — Auditoria (audit)

Ver `sdd/audit.md` (auditoria global) e `sdd/source-manifest.md` (32 fontes, hashes, classificações). Resumo específico desta spec:

1. **Fontes lidas integralmente:** blueprint + 8 docs discovery + 12 diagramas originais + 5 docs `references/dotnet` + 3 diagramas .NET (18 .md no total; integridade 32/32 — EVD-000-001).
2. **Statements de negócio** (contexts, agregados, invariantes, UC-001..011, NFRs): CONFIRMED, independentes de stack.
3. **Statements de tecnologia Python** (FastAPI, uv, Copier, LangChain/LangGraph, pytest): LEGACY/NOT_APPLICABLE_TO_DOTNET — conversões em `design.md` §1.
4. **Divergências resolvidas:** D-1 taxonomia de invariantes (→ `sdd/requirements.md` §1); D-2 stack LEGACY (→ `design.md` §1); D-4 rótulos de diagramas (→ `architecture/diagrams/README.md`).
5. **Lacunas conhecidas:** coleções Mongo de Returns/Reviews/Communications/Analytics sem detalhamento no blueprint (OPEN-01) — detalhar em specs M6+; não bloqueia M0–M5.
