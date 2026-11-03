# ATLAS COMMERCE .NET — M1 REPORT (final do ciclo sem Docker)

```text
DATE_UTC: 2026-10-28
REPOS / COMMITS:
  atlas-platform-contracts-dotnet  — ad48b1a → b7e1062
  atlas-storefront-dotnet           — 99cfb96 → 9611302
  atlas-control-dotnet              — fddef9a → 472ad6b
  atlas-workspace-dotnet (coord.)   — a66c70d → (este commit)
MILESTONE: M1 / SPEC-001 — Platform Foundation
STATUS: IN_PROGRESS — todo o escopo independente de Docker está PASS; gate M1 formal
        permanece aberto por 3 itens BLOCKED externos (Docker) e 1 dependente de push remoto.
```

## TASKs da SPEC-001 — estado final do ciclo

| Task | Estado | Evidência |
|---|---|---|
| 001 Ferramentas | PARTIAL | buf 1.75.0 PASS; **Docker AUSENTE** (EVD-000-003) |
| 002 Matriz de dependências | **PASS** | 3 matrizes + `dotnet list package --vulnerable --include-transitive` = zero em todos os projetos (EVD-001-005) |
| 003 Repo contracts | **PASS** | EVD-001-001 (build/test/buf lint) |
| 004 Templates dotnet new | **PASS** | 4/4 gerados desde pasta vazia com build verde; microservice testa 2/2; CI replica (EVD-001-005; commit b7e1062) |
| 005 Repo storefront (svc+health) | **PASS** | EVD-001-002 (14/14; health 200 runtime) |
| 006 Repo control (host modular) | **PASS** | EVD-001-003 (7/7 com arquitetura; health 200 runtime) |
| 007 Compose core+data | **BLOCKED** | arquivos escritos; execução exige Docker (ausente) |
| 008 OIDC smoke | **BLOCKED** | depende de 007 |
| 009 OTel collector + trace cross-process | **BLOCKED** | depende de 007 (services têm OTel SDK ativo; exportação pendente do collector) |
| 010 CI GitHub Actions | PARCIAL | workflows nos 3 repos (contratos inclui teste de templates); execução requer push remoto |
| 011 Frontends base | **PASS** | Next `pnpm build` exit 0; Angular bundle gerado 1.76s (EVD-001-004) |
| 012 Este relatório | **DONE** | — |

## TESTS EXECUTED (acumulado real do M1)

```text
dotnet build   3 repos + 4 templates gerados  → 0 errors / 0 warnings
dotnet test    29 (repos) + 2 (template gerado) = 31 PASS
dotnet new install ./templates + 4 gerações    → PASS
buf lint        (buf 1.75.0)                    → PASS
dotnet list package --vulnerable (3 repos)      → zero vulnerabilidades
curl /health    control+catalog (runtime)       → 200
pnpm build (Next) / npm run build (Angular)     → exit 0
```

## Bônus relevante

**Smoke de restore do MAF**: o template `atlas-maf-agent` restaura e compila `Microsoft.Agents.AI 1.26.0` (+ `Microsoft.Extensions.AI 10.10.x`) em net10.0 — primeira evidência concreta da ADR-0008. Smoke de **API/execução** permanece obrigatório no M7 antes de codar workflows.

## BLOCKED externo — ação necessária do proprietário

**Instalar Docker Engine + compose plugin neste host** (verificações 2026-10-27/28: binário ausente em PATH//usr/bin//usr/local//opt//snap — EVD-000-003). Desbloqueia TASK-001-007/008/009 e o gate formal M1 (Compose mongo-rs, Keycloak OIDC, OTel cross-process, Testcontainers). Também pendente: push dos 3 repos ao GitHub para o CI rodar (TASK-001-010).

## NEXT EXECUTABLE TASK

1. Proprietário: instalar Docker → eu executo 007→008→009 e fecho o gate M1.
2. Sem Docker: iniciar **SPEC-002 DRAFT→READY** e a fatia de código que não exige containers (Domain/Application dos owners Catalog/Offers/Inventory + protos de serviço + testes de domínio), deixando integração Mongo/Kafka para quando Docker existir.
