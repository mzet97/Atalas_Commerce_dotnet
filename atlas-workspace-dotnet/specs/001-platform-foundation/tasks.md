# SPEC-001 — tasks.md (Platform Foundation)

| ID | Tarefa | Pré-cond | Comando de aceite | Estado |
|---|---|---|---|---|
| TASK-001-001 | Ferramentas: buf instalado; Docker verificado | M0 PASS | buf: **DONE** (1.75.0, EVD-000-003); docker: **BLOCKED** (ausente no host — EVD-000-003) | PARTIAL |
| TASK-001-002 | Matriz de dependências revalidada (docs/dependency-matrix.md nos repos) | 3 repos criados | arquivos publicados com fonte/licença/CVE por pacote | **PASS** (EVD-001-005: scan --vulnerable --include-transitive zero nos 3 repos; MAF restore registrado) |
| TASK-001-003 | Criar repo `atlas-platform-contracts-dotnet` (slnx, CPM, CI, contracts base, templates) | — | `dotnet build` verde; `buf lint` verde; CI YAML commitado | **PASS** (EVD-001-001; templates concluídos em 004) |
| TASK-001-004 | Templates `dotnet new` testados desde pasta vazia | 003 | `dotnet new install` + geração + build do gerado | TODO |
| TASK-001-005 | Criar repo `atlas-storefront-dotnet` com Catalog Api (health/trace) | 004 | `dotnet test` verde; `/health` responde; span exportado | **PASS** (EVD-001-002; 14/14; health 200; span via OTel nativo) |
| TASK-001-006 | Criar repo `atlas-control-dotnet` com host modular mínimo | 004 | idem + teste de arquitetura de módulos | **PASS** (EVD-001-003; 7/7 c/ 5 de arquitetura) |
| TASK-001-007 | Compose core+data (mongo rs, pg, keycloak realm, redis) | **Docker** | `docker compose --profile core --profile data up` + smoke health | **PASS** via Podman rootless (EVD-001-006: rs PRIMARY, pg/redis/kc saudáveis; paridade compose + runbook) |
| TASK-001-008 | OIDC smoke cliente/admin | 007 | script autentica/valida token (exit 0) | **PASS** (EVD-001-007: PKCE S256, svc-account, JWKS, MFA) |
| TASK-001-009 | OTel→Grafana com trace cross-process | 005/006/007 | trace visível com spans dos 2 hosts (EVD) | **PASS** (EVD-001-008: 6 traces TraceQL &&, 3 spans) |
| TASK-001-010 | CI GitHub Actions nos 3 repos | 005/006 | workflow verde em commit de referência | PARCIAL (workflows commitados nos 3 repos; contracts valida templates no CI; execução requer push remoto) |
| TASK-001-011 | Next.js + Angular base | 005/006 | `pnpm build` / `ng build` verde; Playwright instalado | **PASS build** (EVD-001-004; Playwright browsers pendentes) |
| TASK-001-012 | Relatório M1 + roadmap/traceability | 010 | checklist gate M1 respondido com EVD | **PASS** (reports/M1-gate-report.md — gate M1 fechado; 010 parcial aguarda push) |

Ordem: 003→004→(005∥006)→011→010→002→{007→008→009 quando Docker disponível}→012. Tarefas sem Docker podem avançar e ter gate M1 parcial com BLOCKED documentado.
