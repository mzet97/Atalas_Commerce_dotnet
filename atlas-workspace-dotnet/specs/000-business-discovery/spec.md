# SPEC-000 — spec.md (Business Discovery)

**Problema:** modelar integralmente o negócio do Atlas Commerce (e-commerce internacional 1P, mercados BR/US/UE, idiomas pt-BR/en-US/es-ES) com ownership único por agregado, invariantes verificáveis e casos de uso com fluxos alternativos — antes de qualquer arquitetura de componentes ou código.

**Atores:** ver blueprint #01 §3 (14 personas: Visitante, Cliente, Atendente, Gestor de catálogo, Gestor de preços, Operador de estoque, Operador de fulfillment, Analista financeiro, Auditor, Superadmin IAM, Vendedor 3P fase 3, Operadora de pagamento, Transportadora, Agente AI).

**Jornadas centrais:** Compra (descoberta→…→pós-venda); Operação (cadastro→…→auditoria); Assistente (intenção→…→consentimento→checkout normal). Ver blueprint #01 §4.

## 1. Bounded contexts e ownership (write owner único)

| Contexto | Agregados | Comandos chave | Eventos | Write owner (.NET) |
|---|---|---|---|---|
| Identity & Access | IdentityReference, MemberGrant, Policy | Grant/Revoke | AccessChanged | Keycloak + Control IAM (metadados) |
| Product Information (PIM) | Product, Variant/SKU, Category, Media, Attribute | CreateProduct, PublishProduct | ProductPublished, ProductUpdated | Catalog Service |
| Merchandising | Offer, PriceList, Promotion, Coupon | PublishOffer, UpdatePrice, ApplyPromotion | OfferPublished, PriceChanged | Offers Service |
| Inventory | StockPosition, StockMovement, Reservation, Warehouse | ReceiveStock, ReserveStock, ConfirmReservation, ReleaseReservation | StockAdjusted, StockReserved, ReservationExpired | Inventory Service |
| Discovery | SearchDocument, Facets | SearchProducts, Suggest | SearchObserved | Search/Discovery (read owner) |
| Basket | Cart, CartLine | AddItem, RemoveItem | CartUpdated, CartAbandoned | Cart Service |
| Checkout | CheckoutSession, PriceQuote | QuoteOrder, SubmitCheckout | CheckoutSubmitted, CheckoutExpired | Checkout Service |
| Order Management | Order, OrderItem, OrderStatusHistory | PlaceOrder, ConfirmOrder, CancelOrder | OrderPlaced, OrderConfirmed, OrderCancelled | Orders Service |
| Payments | PaymentIntent, PaymentAttempt, Capture, Refund | Authorize, Capture, Refund | PaymentAuthorized/Failed/Captured, Refunded | Payments Service |
| Fulfillment | FulfillmentOrder, Package, Shipment, TrackingEvent | StartPicking, Ship, ConfirmDelivery | ShipmentCreated, Dispatched, Delivered | Fulfillment Service |
| Returns | ReturnRequest, ReturnItem, RMA | RequestReturn, InspectReturn, ApproveRefund | ReturnApproved, RefundRequested | Returns Service |
| Reviews | Review, ModerationDecision | PostReview, ModerateReview | ReviewApproved | Reviews Service |
| Customer Profile | Customer, Address, Preferences, Consent | UpdateProfile, ChangeConsent | CustomerProfileChanged | Customer Service |
| Communications | Notification, Template, DeliveryAttempt | SendNotification | NotificationSent/Failed | Notification Service |
| Shopping Assistant | Conversation, ToolInvocation, Recommendation, Consent | Ask, Compare, ProposeCart | AssistantRecommendationViewed | AI Service (MAF) — **nunca owner de negócio transacional** |
| Admin Control Plane | Approval, AdminCase, AuditRecord, JobRequest | ApproveChange, RequestAdjustment, AssignCase | AdminApprovalGranted, AdminActionAudited | Control Modular Monolith (só processos admin) |
| Analytics | SalesProjection, FunnelMetric, DemandProjection | RebuildProjection, QueryMetric | — | Consumers/read models |

**Regra de ownership (não negociável):** o Control mantém apenas entidades administrativas (approvals/audit/cases) no próprio MongoDB; toda escrita de negócio é comando autenticado ao owner, que revalida políticas (ADR-0001, INV-SEC-001).

## 2. Agregados e limites transacionais

1. `Product` — identidade/lifecycle; nunca contém estoque ou preço corrente autoritativo.
2. `Offer` — seller+sku+market+canal+vigência; máx. 1 ativa por chave de escopo.
3. `StockPosition` — warehouse+sku; on_hand/reserved/safety_stock/version; operação condicional atômica + ledger.
4. `Reservation` — qty, expiração, idempotency_key, checkout/order, status; confirmada não expira.
5. `Order` — snapshot imutável; compensações, nunca reescrita.
6. `PaymentIntent` — provider, idempotency_key, tentativas, capture/refund, status; dedup de webhooks; sem PAN/CVV.
7. `Approval` — request, proposed_changes, requestor/approver distintos, before/after hashes, proof, reason.

## 3. Invariantes

15 invariantes canônicas com enunciados e alias — ver `sdd/requirements.md` §1 (INV-STOCK-001/002/003, INV-ORDER-001/002, INV-MONEY-001, INV-PAYMENT-001, INV-CQRS-001, INV-SEC-001, INV-ADMIN-001, INV-AI-001, INV-QUOTE-001, INV-EVT-001, INV-PII-001, INV-I18N-001).

**Conflito-modelo de referência** (blueprint #02 §4): dois checkouts disputando a última unidade observam `available=1` em PG atrasado; apenas a operação condicional no Inventory (Mongo, CAS por version) decide; PG/Redis/Kafka nunca decidem disponibilidade (diagrama 16).

## 4. Casos de uso (resumo; detalhes com alternativas no blueprint #03)

UC-001 Pesquisar/comparar · UC-002 Carrinho/cotação · **UC-003 Checkout (P0, cenários T01–T05)** · UC-004 Rastrear/cancelar/devolver · **UC-005 Publicação de produto (P0)** · **UC-006 Ajuste de inventário (P0)** · UC-007 Oferta/preço/promoção · UC-008 Monitorar/reprocessar · UC-009 Assistente IA · UC-010 Gestão de acesso · UC-011 Financeiro/conciliação.

Catálogo de testes por domínio (CAT-01, PRC-02, INV-03, ORD-04, ADM-05, AI-06, OPS-07, COM-08) mapeado em `acceptance.feature` e `traceability.md`.

## 5. Escopo e fases

- **MVP (1P):** IAM OIDC RBAC/ABAC; produtos/SKUs/ofertas/disponibilidade; busca; carrinho; checkout; pedido; estoque reservado; pagamento fake; eventos + projeções PG; admin de produtos/estoque; localização; observabilidade; testes.
- **Fase 2:** PSP sandbox real, cupons/impostos por região, múltiplos armazéns, fretes/rastreio, reembolso, avaliações, catálogo multi-idioma, agente IA read-only + carrinho condicionado.
- **Fase 3 (M10, DEFERRED):** marketplace 3P, splits, antifraude, busca dedicada, recomendação, streaming, multi-região.
- **Fora de escopo:** emissão fiscal completa, conta bancária da plataforma, armazenar PAN/CVV, prometer milhões de pedidos/s, compras autônomas por LLM.

## 6. Requisitos

Registro completo com IDs em `sdd/requirements.md` (REQ por contexto, NFR, SEC, AI). Esta spec referencia; não duplica.

## 7. Dados (conceitual)

- **ER conceituais:** diagramas 03 (catálogo), 04 (pedido/financeiro), 05 (admin), 19 (assistente IA).
- **Write model Mongo:** coleções por serviço (`catalog.products`, `offers.offers`, `inventory.positions/movements/reservations`, `orders.orders`, `payments.intents`, `checkout.sessions`, `control.approvals`) com outbox transacional — detalhe em blueprint #04 §3 e `design.md` §3.
- **Read model PG:** 10 projeções (`catalog_product_search`, `available_offer_view`, `customer_order_summary`, `order_detail_view`, `inventory_admin_view`, `payments_reconciliation_view`, `admin_audit_search`, `funnel_metrics_hourly`, `projection_offsets`, `processed_events`).

## 8. Fora de escopo desta spec

Qualquer decisão de código, scaffold, container ou deploy (M0 = modelagem). Decisões técnicas ficam nas ADRs (PROPOSED) e no `design.md`.

## 9. Definition of Done (SPEC-000)

1. Fontes auditadas com hashes (EVD-000-001). 2. Glossário e invariantes reconciliadas. 3. Contexts/ownership/agregados/UC documentados. 4. Diagramas editáveis consistentes (15 cópias + 12 novos). 5. Cenários críticos ≥5 em Gherkin. 6. ADRs obrigatórias PROPOSED. 7. Matriz de rastreabilidade inicial. 8. Relatório M0 honesto (sem PASS de teste inexistente).
