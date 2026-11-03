# ADR-0002 — MongoDB como command store + PostgreSQL como read model (CQRS poliglota)

**Status:** PROPOSED · **Data:** 2026-10-27 · **Absorve:** blueprint ADR-002

## Contexto
O blueprint exige CQRS com persitência poliglota como exercício deliberado de portfólio; cargas de leitura (busca, vitrine, admin) e de escrita (reserva, pedido) têm formatos diferentes. Exceção conhecida: bancos internos de infraestrutura (Keycloak, telemetria) não recebem CQRS artificial.

## Alternativas
1. **MongoDB (replica set) write + PostgreSQL projeções read** (proposta do contrato).
2. PostgreSQL único read/write — tecnicamente válido, mas elimina o objetivo de portfólio e acopla schema de comando ao de consulta.
3. Event store dedicado (EventStoreDB) + Mongo read — agrega operação de mais um stateful sem benefício comprovado neste escopo.

## Decisão proposta
- Write model: coleções por serviço (`catalog.products`, `inventory.positions`… ver design.md da SPEC-000) com `version` para concorrência otimista/CAS; replica set obrigatório (transações + change streams).
- Read model: PostgreSQL com projeções denormalizadas por caso de uso (`catalog_product_search`, `available_offer_view`, `inventory_admin_view`…), `projection_offsets` e `processed_events`.
- Consistência eventual explícita; read-your-writes via `source_version` (estados `PUBLISHING/SYNCING`); decisões críticas sempre no owner (INV-CQRS-001).

## Trade-offs
(+) modelos otimizados por lado; projeções reconstruíveis; vitrine rápida. (−) dois stores para operar; lag de projeção visível; duplicação de dado derivado. Custo: dois stateful em Compose/K8s com backup próprio.

## Modos de falha / rollback
Falha: projeção atrasada/corrompida → reconstrução por replay (determinística por event_id+version). Rollback: read model pode ser recriado do zero; write model é a autoridade.

## Fontes
mestre §2.3, §5.4, §18.1; blueprint #04; `references/dotnet/00` (conversões CONFIRMED).
