# Atlas Control (.NET)

Repositório **B** do Atlas Commerce — portal administrativo: Angular (futuro, TASK-001-011) + **monólito modular** ASP.NET Core 10 / C# 14. O Control **nunca escreve** nos bancos dos owners (ADR-0001): comanda por gRPC autenticado e possui estado próprio apenas para approvals/audit/workflow (SPEC-004).

## Estrutura (M1)

- `src/Atlas.Control.Module.Abstractions/` — contrato in-process de módulos (`IAtlasModule`, `IModuleEndpoints`).
- `src/Modules/Atlas.Control.Modules.{CatalogOperations,Approvals}/` — módulos (não referenciam uns aos outros; host compõe o DAG).
- `src/Atlas.Control.Host/` — host único com health/OpenAPI/OTel e composição de módulos.
- `tests/Atlas.Control.Architecture.Tests/` — **regras de fronteira por reflection** (ADR-0019): módulos independentes entre si, sem dependência do host, sem referência a infra dos owners (MongoDB.Driver/Confluent.Kafka/StackExchange.Redis banidos até SPEC-004 refinar com o Mongo próprio do Control).
- `tests/Atlas.Control.Host.Tests/` — health + composição de módulos.

## Comandos

```bash
dotnet build Atlas.Control.slnx                     # 7 projetos, 0 erros/warnings
dotnet test tests/Atlas.Control.Architecture.Tests/Atlas.Control.Architecture.Tests.csproj   # 5 PASS
dotnet test tests/Atlas.Control.Host.Tests/Atlas.Control.Host.Tests.csproj                   # 2 PASS
dotnet run --project src/Atlas.Control.Host        # /health, /modules/*/status
```

## Próximos (SPEC-002/004)

Comandos gRPC aos owners (`PublishProduct`, `AdjustStock`) com approval_token assinado; auditoria append-only com hash encadeado; policy server de escopos.
