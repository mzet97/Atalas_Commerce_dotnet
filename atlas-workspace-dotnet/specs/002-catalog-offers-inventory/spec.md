# SPEC-002 — spec.md

**Problema:** primeira fatia comercial de ponta a ponta: cadastrar/publicar produto, publicar oferta e receber/ajustar estoque pelo Admin, com ownership único, CQRS consistente e vitrine lendo projeção — sem duplicar escrita nem oversell.

**Atores:** catalog_editor, pricing_manager, inventory_operator, approver (four-eyes), cliente/visitante (leitura).

## Requisitos funcionais (herdam REQ-CAT/OFF/INV/ADM de `sdd/requirements.md`)

1. **Catálogo:** CreateProduct (rascunho com LocaleText pt-BR/en-US/es-ES, atributos por categoria, mídia, SKUs imutáveis) → submissão → aprovação four-eyes no Control → `PublishProduct` gRPC ao Catalog owner → `ProductPublished` (outbox) → projeção `catalog_product_search`. Publicar não cria Offer.
2. **Ofertas:** PublishOffer (sku, seller=1P, market, canal, moeda, price_minor, validade) com índice único de escopo; mudança acima de limite exige four-eyes; `OfferPublished` → `available_offer_view`.
3. **Inventário:** ReceiveStock/AdjustStock com motivo/ator/correlation e ledger append-only (`inventory.movements`); posição `warehouse+sku` com CAS por `version`; ReserveStock/Release/Confirm conforme INV-STOCK-001..003; ajuste acima de limite com aprovação.
4. **Control:** módulos CatalogOperations/PricingOperations/InventoryOperations autenticam (ABAC por market/warehouse), persistem approval/audit no Mongo próprio e comandam owners; sem acesso às coleções deles.
5. **Projeções/leitura:** workers idempotentes (`processed_events` na mesma transação PG); Redis cache-aside com invalidação por evento; Next.js lê via Gateway/BFF com estados `PUBLISHING/SYNCING` até `source_version`.
6. **Autorização:** operator sem escopo → 403 + auditoria da tentativa (ADM-05); toda mutação backend-side.

## Dados

Coleções (blueprint #04 §3): `catalog.products`, `offers.offers`, `inventory.positions/movements/reservations`. Projeções M2: `catalog_product_search`, `available_offer_view`, `inventory_admin_view`, `projection_offsets`, `processed_events`. VOs C#: `ProductId/SkuId/OfferId/WarehouseId/MarketId/Money`.

## Eventos

`ProductPublished`, `ProductUpdated`, `OfferPublished`, `PriceChanged`, `StockAdjusted`, `StockReserved` — envelope INV-EVT-001; tópicos `atlas.catalog.v1`, `atlas.offers.v1`, `atlas.inventory.v1` (key=aggregate_id).

## Fluxos alternativos críticos

Reprovação com motivo; versão concorrente (CAS falha → re-leitura e retry orientado); evento duplicado (inbox pula); CDC down (outbox acumula, retoma); projeção fora de ordem (upsert condicional por version); contenda pela última unidade (exatamente 1 reserva — UC003-T01 antecipada no nível de Inventory).

## Fora de escopo

Cart/checkout de compra (M3), PSP, fulfillment. Reserva existe como capacidade do Inventory, exercitada por testes.

## Critérios de aceite

`acceptance.feature` — destaque: publicação real ponta a ponta; 403 auditado; evento repetido não duplica projeção; corrida pela última unidade = no máx. 1 venda futura; lag de projeção observável.

## Definition of Done

Cenários PASS com EVD (Mongo/Kafka/PG/UI/trace/testes); testes de arquitetura verdes; contratos publicados no repo C; runbook de replay de projeção.
