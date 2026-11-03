# EVD-001-008 — TASK-001-009: OTel→LGTM com trace cross-process Control→Catalog

**Data UTC:** 2026-10-29 · **Resultado:** PASS (6 traces cross-process) · **Commits:** storefront `0a6377f`, control `a4a9572`

## Setup

- `grafana/otel-lgtm` (Podman, standalone; ports 14317→4317 gRPC, 4318 http, 3000 Grafana, 3200 Tempo).
- Apps com `OpenTelemetry` + `AddOtlpExporter()` (`OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`, endpoint 4318) e fontes nativas (`Microsoft.AspNetCore`, `System.Net.Http`).
- Cross-process real: módulo `CatalogOperations` do Control chama o Catalog owner via `HttpClientFactory` (propagação W3C traceparent automática).

## Saídas reais

```text
health: cat=200 ctl=200
cross: {"module":"catalog-operations","catalogOwner":{"reachable":true,"upstream":"{\"service\":\"Atlas.Catalog.Api\"..."}}

# Tempo (GET /api/search, janela 15min):
traces na janela (5min): 4 → root Atlas.Control.Host: 2 | root Atlas.Catalog.Api: 2

# PROVA DEFINITIVA — TraceQL && (spans dos DOIS serviços no MESMO trace):
q={ resource.service.name = "Atlas.Control.Host" } && { resource.service.name = "Atlas.Catalog.Api" }
→ traces cross-process: 6   (ex.: traceID a544be1cc0dfbdfb1f0235f57342ace7, root Atlas.Control.Host, 3 spans)
```

Cada trace: span server do Control → span client HTTP do Control → span server do Catalog (3 spans, mesma traceID).

## Correções reais do ciclo

- `AddSource("Atlas.Catalog")` sozinho não assinava as fontes nativas → adicionadas `Microsoft.AspNetCore`/`System.Net.Http`.
- gRPC (14317) não entregava (pasta/gRPC) → `http/protobuf` em 4318.
- Busca do Tempo exige janela `start/end` explícita (default não cobria ingestão recente).
- 4317 do host já ocupado por processo alheio → OTLP gRPC do LGTM mapeado em 14317.

## Acesso

Grafana: http://127.0.0.1:3000 (admin/admin — LGTM dev). Datasource Tempo pré-configurado.
