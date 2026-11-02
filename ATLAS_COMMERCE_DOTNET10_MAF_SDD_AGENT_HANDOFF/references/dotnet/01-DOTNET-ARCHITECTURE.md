# ASP.NET Core 10 Architecture — bounded contexts, modules and runtime

## 1. Two systems and three repositories

The external Storefront is service-based; the internal administrative Control is deployed as one modular ASP.NET Core host. Each can release and scale independently. The third repository publishes **wire contracts** (.proto, OpenAPI, AsyncAPI) and templates/infra; it is not a service or a common domain assembly.

The **single write owner** per aggregate is non-negotiable. Catalog/Offer/Inventory services own MongoDB writes; Control's admin catalog module authenticates/authorizes the operator and calls an authenticated owner API. The owner rechecks business policies and scope. Control owns its own approval/audit/workflow database only. Storefront read projections in PostgreSQL are not used to decide last-unit reservations.

## 2. Layers and boundaries

```
Api / GrpcEndpoints / Consumer      -> Application use cases
Application handlers               -> Domain + ports/interfaces
Infrastructure adapters            -> MongoDB, Postgres, Kafka, RabbitMQ,
                                      Redis, gRPC, OIDC, PSP
Domain                             -> pure C# 14, no frameworks
Composition root                   -> DI, options, startup validation, OTel
```

In Control, modules are isolated assemblies or namespaces with *in-process* contracts and architecture tests. Their dependency graph must be a DAG. No module calls private repositories belonging to a different module. Each module can own Mongo aggregates, PG reports and events, but cross-system writes must reach the microservice owner.

## 3. .NET 10 implementation decisions

- ASP.NET Core 10, C# 14, `.slnx`, `global.json`, Directory.Build.props, Directory.Packages.props; baseline versions verified in tools, not assumed.
- Explicit command/query handlers; MediatR optional after license and value evaluation. `Result<T>` and domain errors mapped to versioned API ProblemDetails.
- Domain VOs `Money`, `Currency`, `ProductId`, `SkuId`, `OfferId`, `WarehouseId`, `ReservationId`, `OrderId`, `PaymentIntentId`, dates via TimeProvider. No binary/float currency.
- MongoDB.Driver write aggregates; transactionally persistent outbox; optimistic concurrency/CAS. Mongo replica set mandatory for transactional demo.
- C# projections update PostgreSQL via Npgsql/Dapper/EF Core 10 where justified; `processed_events` and view mutation same PostgreSQL transaction.
- Kafka by `Confluent.Kafka` consumer/producer workers and CDC bridge; RabbitMQ tasks using compatible .NET client and durable queues. Consider outbox-to-broker topology explicitly rather than publishing to both independently.
- gRPC `.proto` schema-first; avoid `code-first grpc` as the only contract source because wire compatibility and future polyglot use are portfolio goals.
- Redis cache-aside and rate limit; not an authoritative stock or financial store.
- Keycloak roles and resource policies tested at the owner; privileged operations require 4-eyes approval and auditable intent/decision.
- Resilience: cancellation, bounded backpressure, `HttpClientFactory` and resilience middleware, deadlines, retries only when idempotent, structured logging, OTel.

## 4. Business process details to model

**Catalog:** Product is a PIM identity; SKU is sellable variant; Offer is seller/market-specific commercial proposition. Localized text belongs to content identity. Inventory maintains positions per warehouse/SKU. 1P is first release; 3P is a modeled extension, not an early feature.

**Checkout:** quote snapshot and expiry → reserve stock with at-most-one for last SKU → authorize payment with fake PSP → persist order state → confirm/compensate. The exact order of side effects and recovery is an ADR because a remote PSP has no shared transaction. Webhook duplicates, out-of-order signals, timeout-as-UNKNOWN and reconciliation are first-class states.

**Order:** durable immutable line snapshots, explicit state machine, idempotency across retries, shipping and returns with separate owners/flow policies. **Do not calculate totals from current offers** when reading a completed order.

## 5. Observability

Trace web→ASP.NET Core→gRPC→Mongo→Outbox→Kafka→PG→UI, preserving correlation and causation metadata. Dashboard Grafana: conversion, quote p99, reservations, oversell guard, outbox age, projection lag, payments UNKNOWN, Kafka consumer lag, Rabbit DLQ, Redis miss, AI tool calls and denials. Minimize PII and avoid high-cardinality labels (customer/order IDs).

## 6. On-premises orchestration

Compose profiles `core,data,messaging,observability,ai,simulated-external`; Skaffold + Helm K8s for orchestration. Aspire only for developer composition and a supplementary dashboard; no hosted cloud requirement. Gradually test HPA/KEDA, PV, backups, rollback and restored working checkout, with commands and logs. Do not call single-pod StatefulSet HA.

## 7. Architecture tests

Test that domains cannot reference ASP.NET Core/EF/Mongo/MAF, Control cannot import owner DB adapters, services have no shared write `DbContext`/Mongo client configuration, external API contracts come from approved package/version, and no assembly dependency cycles exist.
