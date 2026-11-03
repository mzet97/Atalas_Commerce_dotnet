# ATLAS COMMERCE .NET — M1 GATE REPORT (FINAL)

```text
DATE_UTC: 2026-10-29
REPOS / COMMITS:
  atlas-platform-contracts-dotnet  — ad48b1a → b7e1062
  atlas-storefront-dotnet           — 99cfb96 → 0a6377f (+ b0a5367 fatia SPEC-002)
  atlas-control-dotnet              — fddef9a → a4a9572
  atlas-workspace-dotnet (coord.)   — … → (este commit)
MILESTONE: M1 / SPEC-001 — Platform Foundation
STATUS: **PASS** (gate M1 fechado; única pendência externa: push dos repos ao GitHub p/ CI remoto — TASK-001-010 PARCIAL por natureza)
```

## Gate M1 (critérios do contrato §12) — resposta item a item

| Critério | Evidência | Status |
|---|---|---|
| Ao menos 1 microserviço ASP.NET Core 10 com build/test/health válidos | Catalog Api: 14/14 testes; `/health` 200 (in-memory + runtime) — EVD-001-002 | **PASS** |
| Host modular admin com build/test/health válidos | Control Host: 7/7 (5 arquitetura); `/health` 200 — EVD-001-003 | **PASS** |
| dotnet CLI e templates reproduzíveis | 4/4 templates gerados desde pasta vazia com build+test verdes; CI replica — EVD-001-005 | **PASS** |
| Apps iniciam por scripts documentados | `dotnet run` + runbook `deploy/compose/README-podman.md` | **PASS** |
| Auth real de cliente/admin com smoke | PKCE S256 end-to-end + client_credentials + JWKS + MFA required-action (Keycloak 26.0.7 real) — EVD-001-007 | **PASS** |
| Testcontainers/Compose | Stack executando via Podman (paridade declarativa compose): mongo **replica set PRIMARY** c/ session-write, pg accepting, redis PONG, keycloak + realm importado — EVD-001-006 | **PASS** (motor: Podman rootless — EVD-000-004) |
| Primeiro trace cross-process | **6 traces** com spans `Atlas.Control.Host` && `Atlas.Catalog.Api` no mesmo traceID (Tempo/LGTM) — EVD-001-008 | **PASS** |
| CI verde | Workflows commitados nos 3 repos; execução requer `git push` (remoto é ação do proprietário) | **PARCIAL (externo)** |
| Rollback/rebuild determinístico | lockfiles/CPM; rebuild local verde em todos os ciclos (0 err/0 warn) | **PASS** |

## TESTS EXECUTED (acumulado M1)

```text
31 testes xUnit PASS (8 contracts + 14 storefront + 7 control + 2 template gerado)
[+ SPEC-002 antecipada: +36 testes de domínio (50 total no storefront)]
4 templates dotnet new: geração+build+test PASS · buf lint PASS · vuln scan: zero nos 3 repos
Stack: mongo rs myState=1/sessionWrite=ok · pg accepting · redis PONG · keycloak realm atlas (201)
OIDC: PKCE S256 token (azp=storefront-next) · svc-account · JWKS kid ✓ · MFA required-action
OTel: 6 traces cross-process (TraceQL &&) · Grafana/Tempo/OTLP http/protobuf
```

## Divagações resolvidas no ciclo (fatos registrados)

1. "Docker no host" → na verdade **Podman rootless** acessível por socket (distrobox) — EVD-000-004.
2. DNS inter-container ausente → pod com netns compartilhado.
3. Import de realm por bind mount sombreado → Admin REST API (201).
4. 4317 ocupado → LGTM gRPC em 14317; apps exportam via **http/protobuf 4318**.
5. `AddSource` sem fontes nativas não exportava spans de ASP.NET/HttpClient → corrigido.

## NOT IMPLEMENTED / DEFERRED (honesto)

- **CI remoto executando**: exige `git push` dos 3 repos (remoto/owner). Workflows prontos.
- Playwright browsers (download futuro); MAF API smoke (M7 — restore já validado).
- Helm/K8s (M8); PSP fake (M3).

## NEXT EXECUTABLE TASK

**SPEC-002 em progresso** (domínio 50 testes PASS — EVD-002-001): próxima fatia = persistência Mongo (replica set já disponível!) com outbox transacional + gRPC owners + projeções PG → gate M2 (contenda última unidade real no Mongo, evento→projeção idempotente, 403 sem escopo).
