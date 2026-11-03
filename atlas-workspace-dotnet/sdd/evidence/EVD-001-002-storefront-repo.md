# EVD-001-002 — Repo atlas-storefront-dotnet (fundação M1)

**Data UTC:** 2026-10-28 · **Commit:** `99cfb96` (branch main) · **Resultado:** PASS (escopo M1 sem Docker)

## Comandos executados e saídas

```text
$ dotnet build Atlas.Storefront.slnx
dotnet build: 5 projects, 0 errors, 0 warnings

$ dotnet test tests/Atlas.Catalog.Domain.Tests/...      Passed! 11/11 (43 ms)
$ dotnet test tests/Atlas.Catalog.Api.Tests/...         Passed!  3/3  (278 ms)

# Smoke em runtime (portas reais via launchSettings):
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5219/health   → 200
$ curl -s http://127.0.0.1:5219/catalog/drafts/11111111-...-555555555555
  {"id":"11111111-...","status":"Draft","titles":[{"locale":{...:"pt-BR"},"text":"Rascunho de demonstração"}]}
```

## Correções reais durante a execução (falhas encontradas e resolvidas)

1. **CVE bloqueada pelo build** (NU1902→error): OpenTelemetry 1.12.0 com GHSA-g94r-2vxg-569j e GHSA-4625-4j76-fww9 → atualizado para **1.20.1** (última estável verificada via API do NuGet).
2. Instrumentation package desnecessário: ASP.NET Core 10 traz traces OTel nativos → package removido.
3. Hífen em nome de método de teste (`Re-submeter...`) → renomeado; `ProductId` serializava como objeto → endpoint expõe `.Value`.

## Conteúdo

Catalog Domain puro (VOs ProductId/SkuId/Locale/LocalizedText; ProductDraft com transições Draft→PendingApproval e regra "sem título não submete"); Minimal API com /health + OpenAPI + OTel; Compose core+data (mongo replica set init, postgres, keycloak, redis — **execução BLOCKED sem Docker**); CI GitHub Actions; dependency-matrix.

## Limitações

- Compose/Keycloak/OIDC/Testcontainers/OTel-collector: **BLOCKED até instalação do Docker** (EVD-000-003).
- Frontend Next.js base (TASK-001-011) pendente.
