# Dependency Matrix — atlas-storefront-dotnet

Baseline **2026-10-28** (verificado via NuGet API + build local). CVEs bloqueiam build (NU1902 → error).

| Pacote | Versão | Notas |
|---|---|---|
| .NET SDK/runtime | 10.0.403 / 10.0.14 | LTS do contrato |
| Microsoft.AspNetCore.OpenApi | 10.0.14 | do template net10 |
| OpenTelemetry.Extensions.Hosting | 1.20.1 | 1.12.0 **rejeitada** por CVE (NU1902 — GHSA-g94r-2vxg-569j); AspNetCore instrumentation nativa do .NET 10 substitui o package legado |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.20.1 | idem CVE GHSA-4625-4j76-fww9 em 1.12.0 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.14 | testes de API in-memory |
| xunit / runner / TestSdk / coverlet | 2.9.3 / 3.1.1 / 17.14.1 / 6.0.4 | idem contracts |

Imagens Compose (dev): mongo:7.0.14, postgres:17.2, redis:7.4.1, keycloak:26.0.7 — validar digest no M8.

## Verificação 2026-10-28 (TASK-001-002)

- `dotnet list package --vulnerable --include-transitive` → **nenhum pacote vulnerável** (todos os projetos).

## Adição 2026-10-29 (SPEC-002)

- MongoDB.Driver **3.13.0** (write model dos owners). Overloads com sessão exigem `UpdateOptions` posicional; Guid requer representação explícita (`[BsonRepresentation(BsonType.String)]`); LINQ3 não traduz aritmética em filtros → update condicional via `$expr/$subtract/$gte` (documentado em MongoInventoryStore).
