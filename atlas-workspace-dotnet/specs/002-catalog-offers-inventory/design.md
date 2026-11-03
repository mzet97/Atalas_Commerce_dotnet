# SPEC-002 — design.md

ADRs aplicáveis: 0001 (Admin→owner), 0002 (Mongo/PG), 0003 (outbox/CDC/Kafka), 0005 (gRPC/Buf), 0011 (EF Core/Dapper), 0013 (composição: só Catalog/Offers/Inventory + Gateway), 0014 (Gateway/BFF nasce aqui).

## Componentes M2

- **Catalog Service** (`src/Services/Catalog/`): Domain (`Product` aggregate, VO `LocalizedText`, policies de publicação); Application (`CreateProductCommand`, `PublishProductCommand` + handlers); Infrastructure (MongoDB.Driver, outbox, repositório aggregate-specific); Api (gRPC `CatalogOwner` + health/OTel).
- **Offers Service**: idem para `Offer` (índice único de escopo ativo; validade); pricing rules v1 (price_minor direto; promotions/cupons ficam para fase 2 — REQ mínimo).
- **Inventory Service**: `StockPosition` CAS; `StockMovement` append-only; `Reservation` (métodos reserve/confirm/release/expire com invariants); gRPC `InventoryOwner`.
- **Control modules**: `CatalogOperations`, `PricingOperations`, `InventoryOperations` (Presentation→Application→Domain próprio de approvals/audit) + `Approvals`, `Audit`; chamada gRPC aos owners com deadline/retry idempotente.
- **Gateway/BFF**: YARP reverse proxy + OpenAPI agregada (só leitura de projeções); rate limit.
- **Workers**: `Projections` (Confluent.Kafka consumer; upsert PG + inbox transacional); outbox→Kafka via Debezium (Compose) com fallback bridge C# se connector inviável no ambiente (decisão de execução registrada em ADR-0003).
- **Next.js**: home/categoria/PDP lendo projeções com estados loading/empty/stale; banner i18n pt-BR primeiro.

## Contratos-chave (drafts em `contracts-draft/`)

`proto/catalog_owner.proto` (PublishProduct, GetProduct), `proto/offers_owner.proto`, `proto/inventory_owner.proto` (ReceiveStock, AdjustStock, ReserveStock…); envelope `jsonschema/domain-event.schema.json`; AsyncAPI `asyncapi/atlas-events.yaml`.

## Persistência detalhada

Índices Mongo: `catalog.products {slug+market}` único condicional; `offers.offers {sku+seller+market+channel}` único para ativas; `inventory.positions {_id warehouse:sku}` + `version`; `inventory.movements {position_id, occurred_at}`; outbox `{aggregate_id, aggregate_version}` + processed flag. PG: GIN em `catalog_product_search.normalized_text`; `(market_id, category_id, status)`; `inventory_admin_view (warehouse_id, sku_id)`.

## Testes

Unit (VOs, policies, CAS lógica), Integration (Testcontainers Mongo rs + PG + Kafka; CDC/dispatch), Contract (Buf breaking N/N-1), Concorrência parametrizada (N threads → última unidade), E2E Playwright (admin publica → aparece na vitrine), Arquitetura (Domain sem frameworks; Control sem adapters owners).

## Observabilidade

Spans: http→gateway→owner→mongo→(cdc)→kafka→worker→pg; métricas: `outbox_age`, `projection_lag`, `consumer_lag`, `reservation_contention_total`; correlation id propagado.
