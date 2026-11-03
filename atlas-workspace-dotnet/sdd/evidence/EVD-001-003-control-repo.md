# EVD-001-003 — Repo atlas-control-dotnet (fundação M1)

**Data UTC:** 2026-10-28 · **Commit:** `fddef9a` (branch main) · **Resultado:** PASS (escopo M1 sem Docker)

## Comandos executados e saídas

```text
$ dotnet build Atlas.Control.slnx
dotnet build: 7 projects, 0 errors, 0 warnings

$ dotnet test tests/Atlas.Control.Architecture.Tests/...   Passed! 5/5  (20 ms)
$ dotnet test tests/Atlas.Control.Host.Tests/...           Passed! 2/2 (227 ms)

# Smoke em runtime:
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5215/health   → 200
$ curl -s http://127.0.0.1:5215/modules/catalog-operations/status
  {"module":"catalog-operations","milestone":"M1","ownerCommands":"pending SPEC-002"}
```

## Testes de arquitetura por reflection (ADR-0019 — fallback até validar NetArchTest)

1. Módulos não referenciam uns aos outros (DAG) — PASS
2. Módulos não dependem do Host — PASS
3. Nenhum assembly do Control referencia MongoDB.Driver / Confluent.Kafka / StackExchange.Redis — PASS (regra será refinada na SPEC-004 quando o Control ganhar seu próprio Mongo de approvals/audit, com justificativa registrada)

## Correções reais durante a execução

CA1716 (namespace `...Module.` e parâmetro `template` reservados) → namespace `Atlas.Control.Modules.Contracts`, parâmetros `route`; ambiguidade de `Results` resolvida com alias; módulos classlib ganharam `FrameworkReference Microsoft.AspNetCore.App` (presentation-layer do monólito).

## Limitações

- Angular base (TASK-001-011) pendente; comando gRPC aos owners chega na SPEC-002.
