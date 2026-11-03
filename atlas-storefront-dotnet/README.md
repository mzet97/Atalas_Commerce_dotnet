# Atlas Storefront (.NET)

Repositório **A** do Atlas Commerce — loja internacional: Next.js (futuro, M1-TASK-001-011) + microserviços ASP.NET Core 10 / C# 14 + assistente MAF. Serviços nascem por fatia vertical (ADR-0013) — hoje apenas o que tem caso de uso real.

## Estrutura atual (M1)

- `src/Services/Catalog/Atlas.Catalog.Domain/` — domínio puro (VOs `ProductId`/`SkuId`/`Locale`/`LocalizedText`, agregado `ProductDraft` com transições mínimas do UC-005).
- `src/Services/Catalog/Atlas.Catalog.Api/` — Minimal API com `/health`, OpenAPI e OTel (traces nativos do ASP.NET Core 10).
- `tests/` — xUnit: 11 testes de domínio + 3 de API (14 PASS em 2026-10-28).
- `deploy/compose/` — stack local `core`+`data` (execução requer Docker — hoje ausente no host, EVD-000-003).
- `.github/workflows/ci.yml` — GitHub Actions (build/test/format/SBOM).

## Comandos

```bash
dotnet build Atlas.Storefront.slnx          # 0 warnings/errors
dotnet test tests/Atlas.Catalog.Domain.Tests/Atlas.Catalog.Domain.Tests.csproj
dotnet test tests/Atlas.Catalog.Api.Tests/Atlas.Catalog.Api.Tests.csproj
dotnet run --project src/Services/Catalog/Atlas.Catalog.Api   # /health, /openapi/v1.json
```

## Próximos passos (SPEC-002)

MongoDB + outbox transacional, gRPC owner (`PublishProduct`), projeções PG — ver `../atlas-workspace-dotnet/specs/002-catalog-offers-inventory/`.

## Regras locais

- Domain não referencia ASP.NET/Mongo/EF/MAF (guardado por teste de arquitetura a partir do M1-repo-control).
- CPM (`Directory.Packages.props`); versões com CVE bloqueiam build (NU1902 → error — já pegou OTel 1.12.0 e foi corrigido para 1.20.1).
