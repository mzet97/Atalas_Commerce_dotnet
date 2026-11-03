# SPEC-002 — Catálogo + Offers + Inventory (1º vertical slice comercial)

**Milestone:** M2 · **Status:** READY (refinada em 2026-10-28; implementação iniciada pela fatia de domínio — as partes com Mongo/Kafka/CDC aguardam Docker do proprietário; Mongo replica set é pré-condição do outbox transacional ADR-0003) · **Repos:** atlas-storefront-dotnet (owners) + atlas-control-dotnet (módulos operacionais) + contracts.

Jornada: operador publica produto/oferta e recebe estoque via Control Angular → Control modules → comandos gRPC autenticados → Catalog/Offers/Inventory owners (MongoDB + outbox) → CDC/Kafka → projection workers → PostgreSQL → Redis → vitrine Next.js pt-BR.

Arquivos: `spec.md` · `design.md` · `tasks.md` · `acceptance.feature` · `traceability.md` · `evidence.md` · `risks.md` · `audit.md`.
