# SPEC-001 — spec.md (Platform Foundation)

**Problema:** estabelecer a fundação verificável dos três repositórios com CI (GitHub Actions — decisão do proprietário 2026-10-28), Compose mínimo, IAM e observabilidade, para que o primeiro slice comercial (M2) nasça sobre base reproduzível.

**Atores:** desenvolvedores/agentes; CI; operação local.

## Requisitos

- **REQ-PLT-100** — Criar `atlas-storefront-dotnet`, `atlas-control-dotnet`, `atlas-platform-contracts-dotnet` somente se inexistentes; cada um com README, `.slnx`, `global.json` (SDK 10.0.403), `Directory.Build.props` (nullable enable, warnings, analyzers), `Directory.Packages.props` (CPM), `.editorconfig`, pipeline **GitHub Actions**, versionamento.
- **REQ-PLT-101** — Templates `dotnet new` customizados (`aspnet-microservice`, `modular-monolith-module`, `dotnet-worker`, `maf-agent`) instaláveis e testados desde pasta vazia em CI.
- **REQ-PLT-102** — Um microserviço real ASP.NET Core 10 (`Atlas.Catalog.Api` — placeholder de M2) com health checks + OTel; e `Atlas.Control.Host` modular com health; **nenhum serviço além** (ADR-0013).
- **REQ-PLT-103** — Compose profiles `core`+`data`: Mongo **replica set** (3 nós), PostgreSQL, Keycloak (realm inicial com clients storefront/control + MFA para admins), Redis; smoke de health documentado.
- **REQ-PLT-104** — CI por repo: build, test, `dotnet format --verify-no-changes`, lint, SAST/SBOM (Dependabot); rebuild determinístico (`--locked-mode` quando lockfile).
- **REQ-PLT-105** — Contratos: repo C com OpenAPI base, proto base, `buf lint`/`buf breaking` em CI (buf 1.75.0 instalado — EVD-000-003).
- **REQ-PLT-106** — OTel → collector → Grafana local (LGTM stack) com primeiro **trace cross-process** (Gateway/Catalog→Control) registrado em evidência.
- **REQ-PLT-107** — Smoke de auth OIDC cliente/admin (Authorization Code + PKCE; MFA admin) com script reproduzível.
- **REQ-PLT-108** — `docs/dependency-matrix.md` em cada repo: package, versão, fonte oficial, licença, CVE, TFM, razão — revalidação do snapshot 2026-10-27.
- **REQ-PLT-109** — Apps base Next.js (pnpm, TS strict, App Router) e Angular (strict, standalone) com lint/build verde e Playwright instalado.

## Não funcionais

Restore determinístico; imagens pinadas por digest quando existirem; zero segredo em Git; containers non-root com probes.

## Fluxos alternativos de setup

Docker ausente → gates de container `BLOCKED` com EVD (não simular). NuGet indisponível → cache lockado + limitação registrada.

## Fora de escopo

Serviços de negócio (M2+), MAF funcional (M7), K8s (M8), PSP fake (M3).

## Critérios de aceite (gate M1)

Ver `acceptance.feature`. Síntese: build/test/health válidos nos 3 repos; templates reproduzíveis; apps iniciam por scripts; OIDC smoke real; trace cross-process; CI verde; rebuild determinístico. Evidências EVD-001-*.

## Definition of Done

Requisitos com EVD; dependency-matrix publicada; ADRs do M1 definidas (CI em design.md); relatório M1 (formato §16).
