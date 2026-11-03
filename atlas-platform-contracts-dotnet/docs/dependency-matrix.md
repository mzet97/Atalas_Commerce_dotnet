# Dependency Matrix — atlas-platform-contracts-dotnet

Baseline verificado em **2026-10-28** (fonte: NuGet/SDK local). Regra: revalidar em cada bootstrap e antes de releases; sem `latest` em produção.

| Pacote/Ferramenta | Versão | TFM | Licença | Fonte | CVE conhecida | Razão |
|---|---|---|---|---|---|---|
| .NET SDK | 10.0.403 (runtime 10.0.14) | net10.0 | MIT | dotnet.microsoft.com (EVM-4DJKY5EKY4) | — | LTS do contrato |
| xunit | 2.9.3 | net10.0 | Apache-2.0 | nuget.org/packages/xunit | — | runner de testes padrão |
| xunit.runner.visualstudio | 3.1.1 | net10.0 | MIT (liksom antes — revalidar em publish) | nuget.org | — | integração vstest/`dotnet test` |
| Microsoft.NET.Test.Sdk | 17.14.1 | net10.0 | MIT | nuget.org | — | harness |
| coverlet.collector | 6.0.4 | net10.0 | MIT | nuget.org | — | cobertura |
| buf CLI | 1.75.0 | — | Apache-2.0 | github.com/bufbuild/buf | — | lint/breaking de proto |

## Pendências de verificação (M1+)

- `dotnet list package --vulnerable` em CI quando cache de NuGet de vulnerabilidades disponível.
- Licenças confirmadas pelos metadados do NuGet na instalação; revalidar formalmente antes de publish público.
- SBOM gerado no CI (cyclonedx) a cada build.

## Verificação 2026-10-28 (TASK-001-002)

- `dotnet list package --vulnerable --include-transitive` → **nenhum pacote vulnerável** (todos os projetos).
- **Templates testados desde pasta vazia** (TASK-001-004): os 4 (`atlas-microservice`, `atlas-module`, `atlas-worker`, `atlas-maf-agent`) geram e buildam; o microservice também roda seus testes gerados (2/2). Etapa replicada no CI.
- **Smoke de restore do MAF** (via template `atlas-maf-agent`): `Microsoft.Agents.AI 1.26.0` + `Microsoft.Agents.AI.Abstractions 1.26.0` + `Microsoft.Extensions.AI 10.10.0/10.10.1` restauram e compilam em **net10.0** (evidência parcial da ADR-0008; smoke de API/execução permanece no M7).
