# EVD-001-006 — TASK-001-007: stack core+data executando (Podman rootless)

**Data UTC:** 2026-10-29 · **Resultado:** PASS · **Motor:** Podman rootless via API REST (`/run/user/1000/podman/podman.sock`, contexto: distrobox dev-fedora com `DOCKER_HOST` apontando ao socket — EVD-000-004)

## Topologia executada

- **Pod `atlas-pod`** (netns compartilhado; containers se falam por localhost — DNS inter-container indisponível no rootless sem aardvark):
  - `atlas-mongo1` (mongo:7.0.14) `--replSet rs0`; porta host 127.0.0.1:27017
  - `atlas-pg` (postgres:17) POSTGRES_USER=atlas/db atlas_read; 127.0.0.1:5432
  - `atlas-redis` (redis:7.4.1); 127.0.0.1:6379
  - `atlas-keycloak` (quay.io/keycloak/keycloak:26.0.7) start-dev; 127.0.0.1:8090→8080
- **Standalone** `atlas-otel-lgtm` (grafana/otel-lgtm:latest): 127.0.0.1:14317→4317 (OTLP gRPC), 4318 (OTLP http), 3000 (Grafana), 3200 (Tempo) — 4317 host já ocupado por outro processo local, por isso 14317.

## Verificações reais (saídas)

```text
$ curl --unix-socket .../podman.sock /v4.0.0/libpod/info       → host fc44, 24 CPUs, cgroupv2 ✓
$ pod network/porta: pod create 201; containers create 201/start 204

# MongoDB replica set (mongosh dentro do pod):
myState=1  (PRIMARY)   sessionWrite=ok     → transações de sessão viáveis (ADR-0002/0003)

# PostgreSQL (pg_isready no pod):   127.0.0.1:5432 - accepting connections
# Redis (redis-cli no pod):         PONG
# Keycloak 26.0.7: started in 6.863s (dev profile); import do realm via Admin REST API
  POST /admin/realms (com realm-atlas.json) → 201 → realms: ['atlas','master']
  discovery: issuer http://127.0.0.1:8090/realms/atlas | authorization_code ✓ | PKCE S256 ✓
  clients: storefront-next(public), control-angular(public), atlas-services(conf+svcacct)
  users: atlas-admin (requiredActions=['CONFIGURE_TOTP']), customer-smoke
```

## Notas de execução (fatos registrados)

- Bind mount do realm direto falhou (volume anônimo do image sombreia `/opt/keycloak/data`; e o serviço podman não vê o mount /var/mnt/4TB) → **import do realm via Admin REST API** (determinístico, versionado no repo contracts `infrastructure/iam/realm-atlas.json`).
- Portas 5432/6379/27017/8090 publicadas apenas em 127.0.0.1 (superfície local).
- Credenciais `*_dev_only` explícitas; nenhum segredo em Git além de valores de desenvolvimento nomeados como tal.

## Reprodução

Script canônico documentado em `atlas-storefront-dotnet/deploy/compose/README-podman.md` (fonte declarativa continua sendo o `docker-compose.yml`; paridade ADR-010).
