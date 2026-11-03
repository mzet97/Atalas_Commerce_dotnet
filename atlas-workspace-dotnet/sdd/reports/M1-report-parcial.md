# ATLAS COMMERCE .NET — M1 REPORT (PARCIAL)

```text
DATE_UTC: 2026-10-28
REPOS / COMMITS:
  atlas-platform-contracts-dotnet  — main — ad48b1a (PASS no escopo sem Docker)
  atlas-storefront-dotnet           — main — 99cfb96 (PASS no escopo sem Docker)
  atlas-control-dotnet              — main — fddef9a (PASS no escopo sem Docker)
  atlas-workspace-dotnet (coord.)   — main — commits M0/Estágio A anteriores + este ciclo
MILESTONE: M1 / SPEC-001 — Platform Foundation
STATUS: IN_PROGRESS (parcial; gates de container BLOCKED por Docker ausente no host)
```

## IMPLEMENTED (verificado)

- **3 repositórios independentes** com `.slnx`, `global.json` (SDK 10.0.403), CPM, `.editorconfig`, CI GitHub Actions (build/test/format/SBOM; buf lint/breaking no contracts), dependency-matrix inicial, READMEs.
- **Contracts:** contratos wire promovidos e validados (`buf lint` PASS, buf 1.75.0); SDK C# do envelope de evento — build 0 erros, **8/8 testes PASS**.
- **Storefront:** Catalog Domain puro (VOs + ProductDraft com invariantes mínimas) + Minimal API (health/OpenAPI/OTel nativo) — 5 projetos, 0 erros/0 warnings, **14/14 testes PASS**, smoke runtime `/health` HTTP 200.
- **Control:** monólito modular (contrato in-process + 2 módulos + host) — 7 projetos, 0 erros, **7/7 testes PASS** (5 de arquitetura por reflection: DAG de módulos, sem dependência do host, sem infra dos owners), smoke runtime 200.
- **Qualidade agindo de verdade:** NU1902 como erro **bloqueou CVE real** (OpenTelemetry 1.12.0 → 1.20.1 após consulta à API do NuGet); CA1716/CA1707 corrigidos (nada suprimido globalmente).

## TESTS EXECUTED (reais, com exit code 0)

```text
dotnet build  ×3 repos (0 errors / 0 warnings)
dotnet test   contracts 8/8 · storefront 14/14 · control 7/7    → 29 testes PASS
buf lint      contracts (buf 1.75.0) → PASS
curl /health  control:5215=200 · catalog:5219=200 (runtime smoke)
```

## NOT IMPLEMENTED / BLOCKED (honesto)

| Item | Estado | Motivo |
|---|---|---|
| TASK-001-007 Compose core+data (executar) + 008 OIDC + 009 OTel-collector/trace cross | **BLOCKED** | Docker ausente no host (verificado; diverge da informação do proprietário — EVD-000-003). Arquivos de Compose e realm draft já escritos. |
| TASK-001-010 CI verde no GitHub | PARCIAL | workflows commitados; execução requer remoto (push) pelo proprietário |
| TASK-001-011 Next.js/Angular base | TODO | próxima tarefa sem Docker |
| TASK-001-004 templates dotnet new customizados | TODO | sequência após frontends base |

## EVIDENCE

- EVD-001-001 (contracts: build/test/buf + commit ad48b1a) · EVD-001-002 (storefront: build/test/smoke + CVE OTel + commit 99cfb96) · EVD-001-003 (control: build/test/arquitetura + commit fddef9a) · EVD-000-003 (buf OK; Docker AUSENTE).

## SECURITY / RELIABILITY

- CVE real detectada e corrigida por gate de build; sem segredos em Git (compose usa credenciais `*_dev_only` explícitas de dev); testes de arquitetura ativos desde o primeiro dia no Control.

## NEXT EXECUTABLE TASK (ordem)

1. **Proprietário:** instalar Docker (ou apontar o host correto) → desbloqueia 007/008/009 e Testcontainers.
2. TASK-001-011 (apps base Next/Angular) → TASK-001-004 (templates) → TASK-001-002 (matriz completa) → M1 gate final.
3. Após gate M1: SPEC-002 (refinar DRAFT→READY e iniciar fatia Catalog/Offers/Inventory).
