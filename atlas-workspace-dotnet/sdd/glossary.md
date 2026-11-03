# SDD — Glossário (linguagem ubíqua)

Fontes: discovery 01 §6, blueprint #01/#02, prompt mestre §3. Termos de domínio em inglês nas APIs/código; definições em pt-BR. IDs imutáveis (UUIDv7/ULID); chaves naturais (GTIN, e-mail) nunca substituem IDs.

## Comércio / catálogo

| Termo | Definição | ID / tipo |
|---|---|---|
| **Product** | Item conceitual do catálogo (PIM), independente de vendedor; lifecycle próprio (rascunho→publicado) | `ProductId` |
| **Variant / SKU** | Variação comercial estocável (cor, tamanho); `SKU` diferencia unidades estocáveis | `SkuId` |
| **Offer** | Proposta de venda de um SKU para seller+market+canal+vigência; no máximo uma ativa por chave de escopo | `OfferId` |
| **PriceList / Promotion / Coupon** | Regras de preço e desconto por canal/mercado; cupom com política de uso único/quota | — |
| **Seller** | Vendedor; 1P = a própria plataforma no MVP; 3P é extensão modelada desde o início | `SellerId` |
| **Market** | Contexto comercial e regulatório geográfico (BR, US, UE) — ≠ locale ≠ moeda | `MarketId` |
| **Locale** | Idioma/formatação (`pt-BR`, `en-US`, `es-ES`) | — |
| **Currency** | Denominação monetária da oferta/quote (BRL/USD/EUR); exibição ≠ autoridade | — |

## Compra / transação

| Termo | Definição |
|---|---|
| **Cart / CartLine** | Carrinho do cliente (autenticado ou guest) com linhas SKU+quantidade |
| **Price Quote** | Cotação **congelada** com validade (TTL), breakdown de custos/impostos/descontos e assinatura/versão; confirmação exige quote vigente |
| **CheckoutSession** | Sessão de checkout com `idempotency_key`, estado e expiração explícita |
| **Reservation** | Alocação temporária de quantidade de estoque para um checkout; confirmada não expira por TTL |
| **Order** | Contrato comercial **imutável** (snapshot de itens, ofertas, moeda, impostos, endereço); mudanças geram compensações |
| **PaymentIntent / PaymentAttempt / Capture / Refund** | Intenção e tentativas de pagamento; captura e estorno são registros auditáveis, nunca edits in place |
| **Shipment** | Agrupamento físico de itens enviados; um pedido pode ter vários |
| **Return / Refund** | Fluxo logístico reverso (RMA) **distinto** da devolução financeira |

## Estoque

| Termo | Definição |
|---|---|
| **Warehouse** | Depósito com região e escopo operacional | 
| **StockPosition** | Chave `warehouse_id+sku_id` com `on_hand`, `reserved`, `safety_stock`, `version`; `available = on_hand − reserved − safety_stock` |
| **Stock Ledger / StockMovement** | Movimento auditável append-only com motivo, ator e `correlation_id` |
| **Oversell** | Venda além do disponível — proibido sob política sem backorder (INV-STOCK-001) |

## Arquitetura / plataforma

| Termo | Definição |
|---|---|
| **Bounded Context** | Fronteira de domínio com ownership único de escrita (tabela completa em `specs/000/spec.md`) |
| **Owner (write owner)** | Serviço único autorizado a persistir/mutar um agregado; todos os demais leem por projeção ou comandam por contrato |
| **Read Model / Projection** | Projeção PostgreSQL orientada a uma consulta, derivada de eventos; **não** é autoridade do comando |
| **Outbox** | Documento imutável salvo na mesma transação Mongo do agregado; CDC (Debezium) publica no Kafka |
| **Inbox / processed_events** | Deduplicação de consumo na mesma transação da projeção (event_id + aggregate_version) |
| **Saga / Process Manager** | Fluxo distribuído com estados persistidos, comandos idempotentes e compensações (checkout) |
| **Four-eyes (dual approval)** | `requested_by != approved_by` para operação de risco (INV-ADMIN-001) |
| **PSP** | Payment Service Provider (fake determinístico no MVP: APPROVED/DECLINED/TIMEOUT/UNKNOWN/WEBHOOK_DUPLICATE/WEBHOOK_OUT_OF_ORDER/REFUND) |
| **ACL (Anti-Corruption Layer)** | Camada que isola modelos externos/legados do domínio interno |
| **Money** | VO `(AmountMinor: long, Currency)` — unidade mínima por moeda; nunca float |
| **Consent Artifact** | Token de consentimento (subject, nonce, TTL, hash canônico do payload) que autoriza `ApplyApprovedCartChange` fora do LLM |
| **IChatClient** | Abstração `Microsoft.Extensions.AI` para o provedor de modelo (Ollama local, OpenAI-compatible, fake) |
| **MAF** | Microsoft Agent Framework (`Microsoft.Agents.AI` / `Microsoft.Agents.AI.Workflows`) — única stack de agentes permitida, em C# |
| **Projection lag** | Atraso entre versão do agregado e `source_version` projetado; exposto na UI admin com estado `PUBLISHING`/`SYNCING` |
| **PUBLISHING / SYNCING** | Estados de UI para read-your-writes enquanto a projeção não alcança `source_version` |

## Eventos de domínio (vocabulário canônico)

`ProductPublished`, `ProductUpdated`, `OfferPublished`, `PriceChanged`, `StockAdjusted`, `StockReserved`, `ReservationConfirmed`, `ReservationReleased`, `ReservationExpired`, `CheckoutSubmitted`, `CheckoutExpired`, `OrderPlaced`, `OrderConfirmed`, `OrderCancelled`, `PaymentAuthorized`, `PaymentCaptured`, `PaymentFailed`, `Refunded`, `ShipmentCreated`, `Dispatched`, `Delivered`, `ReturnApproved`, `RefundRequested`, `ReviewApproved`, `CustomerProfileChanged`, `NotificationSent/Failed`, `AdminApprovalGranted`, `AdminActionAudited`, `AccessChanged`.

Envelope obrigatório (INV-EVT-001): `event_id`, `event_type`, `aggregate_id`, `aggregate_version`, `occurred_at`, `schema_version`, `correlation_id`, `causation_id`, `tenant_id?`, `traceparent`.
