# Dependency Matrix — atlas-control-dotnet

Baseline **2026-10-28**. CVEs bloqueiam build (NU1902 → error).

| Pacote | Versão | Notas |
|---|---|---|
| .NET SDK/runtime | 10.0.403 / 10.0.14 | LTS |
| Microsoft.AspNetCore.OpenApi | 10.0.14 | template net10 |
| OpenTelemetry.Extensions.Hosting | 1.20.1 | 1.12.0 rejeitada por CVE (ver storefront) |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.14 | testes de host |
| xunit / runner / TestSdk / coverlet | 2.9.3 / 3.1.1 / 17.14.1 / 6.0.4 | idem demais repos |

## Verificação 2026-10-28 (TASK-001-002)

- `dotnet list package --vulnerable --include-transitive` → **nenhum pacote vulnerável** (todos os projetos).
