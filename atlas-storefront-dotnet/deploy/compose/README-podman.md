# Runbook Podman — stack local do Atlas (paridade com docker-compose.yml)

Este host roda **Podman rootless** (sem binário visível no distrobox; via API REST). Fonte declarativa: `docker-compose.yml` (ADR-0010). Execução real deste ambiente (2026-10-29, EVD-001-006..008) usou a API do socket `unix:///run/user/1000/podman/podman.sock`.

## Por que Pod (rede compartilhada)

DNS inter-container indisponível (rootless sem aardvark) → containers no mesmo **pod** se falam por `127.0.0.1`. O LGTM roda standalone (só recebe do host).

## Subir (sequência canônica via curl)

```bash
S=/run/user/1000/podman/podman.sock
# 1) pod com portas 27017/5432/6379/8090
curl -X POST --unix-socket $S -H 'Content-Type: application/json' \
  -d '{"Name":"atlas-pod","PortMappings":[{"host_ip":"127.0.0.1","container_port":27017,"host_port":27017,"protocol":"tcp"},{"host_ip":"127.0.0.1","container_port":5432,"host_port":5432,"protocol":"tcp"},{"host_ip":"127.0.0.1","container_port":6379,"host_port":6379,"protocol":"tcp"},{"host_ip":"127.0.0.1","container_port":8080,"host_port":8090,"protocol":"tcp"}]}' \
  http://d/v4.0.0/libpod/pods/create
# 2) containers (corpo JSON completo no histórico EVD-001-006; mounts usam nomes OCI: type/source/destination/options)
#    atlas-mongo1 (mongo:7.0.14 --replSet rs0), atlas-pg (postgres:17), atlas-redis (redis:7.4.1), atlas-keycloak (kc 26.0.7 start-dev)
# 3) init RS (container efêmero no pod): mongosh --host 127.0.0.1 --eval 'rs.initiate({_id:"rs0",members:[{_id:0,host:"127.0.0.1:27017"}]})'
# 4) import realm (Admin REST — bind mount do arquivo é sombreado pelo volume do image):
TOK=$(curl -s -X POST http://127.0.0.1:8090/realms/master/protocol/openid-connect/token \
  -d client_id=admin-cli -d username=admin -d password=kc-admin-dev-only -d grant_type=password | jq -r .access_token)
curl -X POST -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' \
  -d @<repo-contracts>/infrastructure/iam/realm-atlas.json http://127.0.0.1:8090/admin/realms
# 5) LGTM standalone: ports 14317→4317, 4318, 3000, 3200 (4317 host ocupado)
```

## Derrubar / limpar

```bash
for c in atlas-keycloak atlas-redis atlas-pg atlas-mongo1; do curl -X DELETE --unix-socket $S http://d/v4.0.0/libpod/containers/$c?force=true\&v=true; done
curl -X DELETE --unix-socket $S http://d/v4.0.0/libpod/pods/atlas-pod?force=true
curl -X DELETE --unix-socket $S http://d/v4.0.0/libpod/containers/atlas-otel-lgtm?force=true\&v=true
```

## Apps com telemetria (M1)

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4318 OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf \
OTEL_SERVICE_NAME=Atlas.Catalog.Api dotnet run --project ... -- --urls http://127.0.0.1:5299
# Tempo query (janela explícita obrigatória):
curl -sG http://127.0.0.1:3200/api/search --data-urlencode 'q={}' --data-urlencode start=$(( $(date +%s)-900 )) --data-urlencode end=$(( $(date +%s)+5 ))
```

Notas: credenciais dev-only; conexão Mongo do host: `mongodb://127.0.0.1:27017/?directConnection=true` (RS single-node dev).
