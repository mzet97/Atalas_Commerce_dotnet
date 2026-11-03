# SPEC-001 — design.md (Platform Foundation)

Decisões amarradas: ADR-0002/0003 (dados/CDC no local), ADR-0005 (proto/Buf — buf 1.75.0 instalado), ADR-0010 (Compose obrigatório; Aspire opcional e **não adotado no M1**), ADR-0012 (sem libs condicionais), ADR-0013 (composição mínima), ADR-0014 (gateway adiado para M2). **Decisão do proprietário 2026-10-28: CI = GitHub Actions** (workflow YAML por repo; runners `ubuntu-latest`; cache de NuGet/pnpm; gates de build/test/format/lint/SAST-SBOM).

## Estrutura por repo (materializar no M1)

```text
atlas-storefront-dotnet/          Atlas.Storefront.slnx
  apps/storefront-next/  src/Gateway/  src/Services/Catalog/{Api,Application,Domain,Infrastructure}/
  src/Workers/  src/BuildingBlocks/  tests/{Unit,Integration,Contract,Functional,Performance,Chaos}/
  deploy/{compose,helm,skaffold,aspire}/  docs/ architecture/ specs/ sdd/ .github/workflows/
atlas-control-dotnet/             Atlas.Control.slnx
  apps/control-angular/  src/Atlas.Control.Host/  src/Modules/{CatalogOperations,Approvals,Audit}/
  tests/{Architecture,Unit,Integration,Contract,E2E}/  deploy/...  docs/ ... .github/workflows/
atlas-platform-contracts-dotnet/  Atlas.Contracts.slnx
  contracts/{proto,openapi,asyncapi,jsonschema}/  templates/{aspnet-microservice,aspnet-modular-monolith,dotnet-worker,maf-agent}/
  infrastructure/{compose,iam,telemetry,helm,monitoring}/  tools/ tests/ ci/ .github/workflows/
```

## Pontos técnicos do M1

- **global.json** `{"sdk":{"version":"10.0.403","rollForward":"latestPatch"}}`; TFM `net10.0`; C# 14; nullable enable; `.slnx` (suportado pelo SDK 10).
- **CPM**: `Directory.Packages.props` central; pacotes M1 mínimos: `Microsoft.Extensions.Hosting`, `AspNetCore.HealthChecks`, `OpenTelemetry.Extensions.Hosting/+Instrumentation.AspNetCore/+Instrumentation.Http`, `MongoDB.Driver`, `Npgsql`/`EFCore` só quando usados; xUnit + `Microsoft.AspNetCore.Mvc.Testing` para testes.
- **Compose `core`+`data`**: mongo1/2/3 (`--replSet rs0` init), postgres:18-pinned, keycloak (realm export JSON versionado em `infrastructure/iam/`), redis:7; healthchecks; rede dedicada. Arquivos escritos no M1; execução depende de Docker (EVD-000-003).
- **Keycloak realm**: clients `storefront-next` (public, PKCE), `control-angular` (public, PKCE), `atlas-services` (confidential p/ machine); roles do glossário; MFA obrigatório para roles admin; export/import por `kc.sh import`.
- **OTel**: SDK nos hosts → OTLP → `grafana/otel-lgtm` (dev) — produção segregada no M8. Trace cross-process: propagação W3C via Activity.
- **CI GitHub Actions**: matrix dotnet 10 + node 24/pnpm; jobs `build` (`dotnet build -warnaserror --locked-mode`), `test` (`dotnet test` + coverage), `format`, `lint`/`buf-lint` (repo C), `sbom` (`cyclonedx`), Dependabot config. Workflows versionados `.github/workflows/ci.yml`.
- **Templates `dotnet new`**: pastas `.template.config/template.json` no repo C; instalação testada gerando projeto em temp e buildando.

## Referências

`C4-02-container.mmd`, `20-deployment-compose.mmd`, ADRs 0001–0015 (+0016+ se aplicável), `references/dotnet/01`.
