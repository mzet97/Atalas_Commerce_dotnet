# Atlas Platform Contracts (.NET)

Repositório **C** do Atlas Commerce — contratos wire versionados (Protobuf/OpenAPI/AsyncAPI/JSON Schema), SDKs finos de contrato e infraestrutura compartilhada. **Não é serviço de negócio e não compartilha modelos de domínio** entre bounded contexts (ADR-0001/0013).

## Estrutura

- `contracts/proto/atlas/{catalog,inventory,offers}/v1` — contratos gRPC dos owners (buf lint STANDARD + breaking FILE).
- `contracts/openapi/` — APIs de borda (Gateway/BFF e Control Admin).
- `contracts/asyncapi/` + `contracts/jsonschema/` — eventos de domínio (envelope INV-EVT-001).
- `src/Atlas.Contracts.Events/` — SDK fino do envelope de evento em C# (record imutável + validação espelhando o JSON Schema).
- `tests/` — testes xUnit do SDK (8 testes).
- `infrastructure/iam/` — realm Keycloak (draft).
- `.github/workflows/ci.yml` — CI (GitHub Actions): build, test, format, buf lint/breaking, SBOM.

## Comandos

```bash
dotnet build Atlas.Contracts.slnx
dotnet test tests/Atlas.Contracts.Events.Tests/Atlas.Contracts.Events.Tests.csproj
buf lint                      # requer buf CLI (~/.local/bin/buf)
```

## Regras de evolução

- Breaking changes: nova versão (v2) + dupla publicação; `buf breaking --against` no CI.
- Envelope de evento imutável em v1 (adições = `schema_version+1` compatível).
- Pacotes publicados por release com versionamento semântico; sem acoplamento implícito ao workspace de coordenação (`../atlas-workspace-dotnet`).

## Estado (M1 em andamento)

- ✅ Contratos draft promovidos e validados (`buf lint` PASS com buf 1.75.0; YAML/JSON parse OK).
- ✅ SDK do envelope com 8 testes PASS (net10.0).
- ⛔ Templates `dotnet new` (aspnet-microservice etc.) — TASK-001-004 pendente.
- 📄 Spec fonte: `../atlas-workspace-dotnet/specs/001-platform-foundation/`.
