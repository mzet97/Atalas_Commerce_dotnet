# SPEC-002 — tasks.md (DRAFT — detalhar numeração final ao entrar em M2)

| ID | Tarefa | Pré-cond | Comando de aceite | Estado |
|---|---|---|---|---|
| TASK-002-001 | Contratos proto v1 (catalog/offers/inventory owners) + buf lint/breaking no CI | M1 | `buf lint && buf breaking --against previous` verde | TODO |
| TASK-002-002 | Catalog Service: aggregate Product + outbox + gRPC Publish/Get | 001 | testes unit+integration verdes; publicação persiste agregado+outbox na mesma transação | **PASS** (persistência EVD-002-003 + **gRPC** CatalogOwnerService pelo contrato com 4 testes in-memory; approval_token validado como presença — criptografia na fatia four-eyes) |
| TASK-002-003 | Offers Service: aggregate Offer + índice único escopo + eventos | 001 | idem; segunda oferta ativa no mesmo escopo é rejeitada | **PASS persistência** (commit f197a1b: índice único PARCIAL ativo, corrida 8-way→1, idempotência request_id) |
| TASK-002-004 | Inventory Service: StockPosition CAS + movements + Reservation | 001 | teste de concorrência N threads última unidade → 1 vencedor | **PASS** (EVD-002-002: CAS real no Mongo rs, 10-way UC003-T01, idempotência, outbox transacional) |
| TASK-002-005 | Control modules (Catalog/Pricing/Inventory Ops + Approvals + Audit) com gRPC aos owners | 002-004 | operador sem escopo → 403 + audit registrado | **PASS** (EVD-002-005: four-eyes real c/ Keycloak+JWT+HMAC; 403/409 auditados; smoke cross-process p/ close do gate) |
| TASK-002-006 | Outbox→CDC→Kafka operacional (Debezium ou bridge documentada) | M1 compose | evento outbox aparece no tópico; reenvio é idempotente | **PASS bridge** (EVD-002-003: OutboxDispatcher→Kafka KRaft Podman; ack + Published flag; reentrega/dedup provados) |
| TASK-002-007 | Projection workers → PG (catalog_product_search, available_offer_view, inventory_admin_view) + inbox | 006 | evento duplicado não duplica projeção; replay determinístico | **PASS** (EVD-002-003 + EVD-002-004: as 3 views M2 — catalog_product_search, available_offer_view, inventory_admin_view — todas com processed_events dedup transacional e stale guard) |
| TASK-002-008 | Gateway/BFF YARP + OpenAPI + rate limit | 007 | rota de leitura responde da projeção com source_version | **PASS parcial** (EVD-002-006: YARP + /api/catalog/products com source_version; rate limit na SPEC-003) |
| TASK-002-009 | Vitrine Next.js (home/categoria/PDP) + estados PUBLISHING/SYNCING | 008 | E2E admin publica → produto aparece com lag observável | **PASS parcial** (EVD-002-006: home SSR exibe produto publicado via BFF; categoria/PDP e estados SYNCING no M3/M5) |
| TASK-002-010 | Redis cache-aside + invalidação por evento | 008 | cache hit sem dado stale após PriceChanged | TODO |
| TASK-002-011 | Testes de arquitetura (boundaries) + relatório M2 | 005-009 | suite verde; relatório §16 com EVD | TODO |
