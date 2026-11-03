# EVD-001-001 — Repo atlas-platform-contracts-dotnet (fundação M1)

**Data UTC:** 2026-10-28 · **Commit:** `ad48b1a` (branch main) · **Resultado:** PASS (escopo M1 sem Docker)

## Comandos executados e saídas

```text
$ dotnet build Atlas.Contracts.slnx
      0 Warning(s), 0 Error(s)   (parciais: corrigidos CS0260 partial no record c/ GeneratedRegex; cadeia de Directory.Build.props em tests/)

$ dotnet test tests/Atlas.Contracts.Events.Tests/Atlas.Contracts.Events.Tests.csproj
Test Run Successful. Total tests: 8  Passed: 8  (0.78s)

$ buf lint
(no output) PASS — buf 1.75.0; correções reais para conformidade STANDARD:
  protos movidos para contracts/proto/atlas/{ctx}/v1/; services com sufixo Service;
  request/response únicos por RPC (inventory)
```

## Conteúdo

- Contratos wire promovidos dos drafts (3 proto + 2 OpenAPI + AsyncAPI + envelope JSON Schema).
- `src/Atlas.Contracts.Events`: SDK C# do envelope (record imutável + validação espelhando o schema; 8 testes cobrindo padrão `Aggregate.ActionVn`, campos obrigatórios, opcionais).
- CI GitHub Actions (build/test/format/buf lint/breaking em PR/SBOM), CPM, global.json 10.0.403, .editorconfig, dependency-matrix.

## Limitações honestas

- `buf breaking` só roda com repositório remoto configurado (CI de PR).
- Templates `dotnet new` (TASK-001-004) pendentes.
- Realm Keycloak draft no README de iam (import real exige Docker).
