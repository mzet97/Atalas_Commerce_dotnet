# SDD — Registro de Requisitos (canônico)

IDs **imutáveis** após publicação; deprecar, nunca renumerar. Fontes: prompt mestre §2/§3.2/§7/§9, blueprint #02/#03/#05/#08, discovery 01–08. Status inicial: `TODO` (rastreabilidade em `traceability.md`).

## 1. Invariantes canônicas (INV-*)

A taxonomia do **prompt mestre §3.2 é canônica**; os IDs do blueprint (`INV-001..012`) tornam-se alias. Divergência D-1 da auditoria resolvida aqui.

### Reconciliação blueprint → canônico

| Blueprint (alias) | Canônico | Tema |
|---|---|---|
| INV-001 | INV-STOCK-001 | disponibilidade nunca negativa |
| INV-002 | INV-STOCK-003 | reserva idempotente |
| INV-003 | INV-MONEY-001 | dinheiro sem float |
| INV-004 | INV-QUOTE-001 | confirmação exige quote vigente |
| INV-005 | INV-ORDER-002 | confirmação exige pagamento+reserva; sem duplo avanço |
| INV-006 | INV-PAYMENT-001 | estado final por PSP+reconciliação |
| INV-007 | INV-SEC-001 | autorização no backend |
| INV-008 | INV-I18N-001 | preço/moeda autoridade do Offer/Quote |
| INV-009 | INV-AI-001 | IA sem mutação autônoma |
| INV-010 | INV-EVT-001 | envelope de evento + projeções idempotentes |
| INV-011 | INV-PII-001 | minimização de PII |
| INV-012 | INV-ADMIN-001 | four-eyes |

### Enunciados canônicos

| ID | Enunciado verificável | Fonte primária |
|---|---|---|
| **INV-STOCK-001** | `on_hand >= 0`, `reserved >= 0`, `available = on_hand − reserved − safety_stock`; reserva condicionada e atômica; oversell = 0 sob concorrência no mesmo SKU (backorder exige política separada provada) | mestre §3.2 |
| **INV-STOCK-002** | Ajuste/recebimento/liberação sempre registra motivo, ator, `correlation_id` e ledger; TTL **não** é a única forma de devolver reserva | mestre §3.2 |
| **INV-STOCK-003** | Reprocessar `ReserveStock` com a mesma idempotency key retorna a **mesma** reserva, nunca duplica redução | blueprint INV-002 |
| **INV-ORDER-001** | Pedido armazena snapshot imutável (itens, preços, descontos, tributos, moeda, endereço, seller); catálogo não reescreve histórico | mestre §3.2 |
| **INV-ORDER-002** | Transições de estado explícitas, idempotentes e com histórico; eventos duplicados/reordenados não criam duas compras | mestre §3.2 |
| **INV-MONEY-001** | Dinheiro em unidade menor + moeda explícita + regra de arredondamento por moeda; `float/double` proibidos em valor comercial | mestre §3.2 |
| **INV-PAYMENT-001** | Idempotency key no checkout/PSP; webhook autenticado e deduplicado; autorização/captura/refund reconciliados com PSP; PAN/CVV jamais persistidos; PSP fake não aparenta gateway real | mestre §3.2 |
| **INV-CQRS-001** | Comando valida no owner com estado autoritativo; leitura defasada (PG/Redis) jamais confirma pagamento, cotação vencida ou última unidade | mestre §3.2 |
| **INV-SEC-001** | Autorização no backend por subject, role, ação, tenant/seller, market, warehouse, limite e estado; UI não é mecanismo de segurança | mestre §3.2 |
| **INV-ADMIN-001** | `requested_by != approved_by` para risco; aprovação auditável e revalidada no owner antes do comando | mestre §3.2 |
| **INV-AI-001** | LLM só recomenda, consulta e propõe carrinho; sem compra autônoma, captura, estorno, preço, estoque ou IAM | mestre §3.2 |
| **INV-QUOTE-001** | Confirmação exige quote vigente com versão/assinatura conhecida, oferta ativa, estoque autorizado, moeda consistente; vencida exige re-cotação | blueprint INV-004 |
| **INV-EVT-001** | `event_id` único; envelope completo (ver glossário); projeções idempotentes e reconstruíveis; ordem preservada por aggregate key | blueprint INV-010 |
| **INV-PII-001** | PII mínima em logs/eventos/traces; criptografia; mecanismos de exclusão/anonimização; retenção conforme requisito legal | blueprint INV-011 |
| **INV-I18N-001** | Preço/moeda são autoridade de Offer/Quote; UI não converte preço autoritativo com FX aproximada (estimativa apenas indicativa) | blueprint INV-008 |

## 2. Requisitos funcionais por contexto (REQ-*)

Formato: `REQ-{CTX}-NNN — [afirmativa verificável] — (UC/fonte)`.

### Catalog/PIM (CAT) — owner: Catalog Service
- REQ-CAT-001 — Criar/editar produto com traduções (locale), atributos por categoria, mídia e SKUs, em rascunho (UC-005)
- REQ-CAT-002 — Publicação exige aprovação por operador autorizado distinto e dispara `ProductPublished` (UC-005, INV-ADMIN-001)
- REQ-CAT-003 — Produto não publicado nunca aparece na busca, mesmo em projeção antiga (UC-001)
- REQ-CAT-004 — Publicar produto não cria Offer automaticamente (UC-005)
- REQ-CAT-005 — SKU é imutável após criação; variantes embedded com limites controlados (blueprint #04 §3)
- REQ-CAT-006 — Fallback explícito de locale (es-ES ausente → fallback sem quebrar checkout) (CAT-01)

### Merchandising/Offers (OFF) — owner: Offers/Pricing Service
- REQ-OFF-001 — Publicar offer por SKU+seller+market+canal+moeda com vigência; máx. 1 ativa por chave de escopo
- REQ-OFF-002 — Mudança de preço acima de limite exige four-eyes (UC-007, INV-ADMIN-001)
- REQ-OFF-003 — Nenhuma alteração retroativa altera Order confirmado (UC-007)
- REQ-OFF-004 — Cupom de uso único rejeita aplicação duplicada (PRC-02)

### Inventory (INV) — owner: Inventory Service
- REQ-INV-001 — ReceiveStock/AdjustStock com motivo, ator, escopo de warehouse e idempotência (UC-006)
- REQ-INV-002 — ReserveStock condicional atômico com CAS/version; rejeita sem saldo (INV-STOCK-001/003)
- REQ-INV-003 — ConfirmReservation/ReleaseReservation com transição de domínio explícita (TTL é otimização) (INV-STOCK-002)
- REQ-INV-004 — Operator sem escopo de warehouse recebe 403 + auditoria da tentativa (ADM-05)
- REQ-INV-005 — Ajuste acima de limite exige aprovação de operador distinto (UC-006)

### Discovery/Search (DSC) — owner: Search/Discovery (read)
- REQ-DSC-001 — Busca com paginação, facetas e sugestões a partir de `catalog_product_search` (UC-001)
- REQ-DSC-002 — Impressões/buscas observáveis sem PII (UC-001)

### Cart (CRT) — owner: Cart Service
- REQ-CRT-001 — Carrinho autenticado/guest com validação de quantidade e SKU comercializável (UC-002)
- REQ-CRT-002 — Estoque 0 pode exibir indisponível, mas não promete checkout (INV-03)
- REQ-CRT-003 — `ApplyApprovedCartChange` somente com consent artifact válido (subject, nonce, TTL, hash) (§9.3 mestre)

### Checkout/Quotes (CHK) — owner: Checkout Service
- REQ-CHK-001 — Quote versionada com TTL e breakdown; re-cotação obrigatória se vencida (UC-002, INV-QUOTE-001)
- REQ-CHK-002 — SubmitCheckout idempotente por `idempotency_key` retorna resultado coerente (UC-003)
- REQ-CHK-003 — Retorno inequívoco CONFIRMED/PENDING/REJECTED; timeout não afirma compra (UC-003)
- REQ-CHK-004 — Disponibilidade revalidada no Inventory autoritativo, não na projeção (UC003-T04, INV-CQRS-001)

### Orders (ORD) — owner: Order Service
- REQ-ORD-001 — PlaceOrder cria snapshot imutável em PENDING_PAYMENT referenciando reservas (UC-003, INV-ORDER-001)
- REQ-ORD-002 — Máquina de estados explícita (ver diagrama 08) com transições idempotentes (INV-ORDER-002)
- REQ-ORD-003 — Timeline rastreável; cancelamento dentro das regras com compensação (UC-004)
- REQ-ORD-004 — Leitura de pedido concluído nunca recalcula totais de offers correntes (references/dotnet/01 §4)

### Payments (PAY) — owner: Payment Service
- REQ-PAY-001 — PSP fake determinístico com cenários APPROVED/DECLINED/TIMEOUT/UNKNOWN/WEBHOOK_DUPLICATE/WEBHOOK_OUT_OF_ORDER/REFUND (mestre §4.1)
- REQ-PAY-002 — Timeout = estado UNKNOWN/PAYMENT_PENDING + reconciliação; nunca sucesso por inferência HTTP (INV-PAYMENT-001)
- REQ-PAY-003 — Webhook assinado/autenticado, deduplicado; reordenação rejeita transições inválidas (UC003-T05)
- REQ-PAY-004 — Estorno/captura geram registros auditáveis; conciliação diária 100% rastreável (UC-011)

### Fulfillment (FFM) / Returns (RET) / Notifications (NOT) — owners respectivos
- REQ-FFM-001 — Picking/packing/expedição com eventos de rastreio; multi-remessa suportada (UC-004)
- REQ-RET-001 — Return (RMA) e Refund são fluxos distintos com aprovação própria (UC-004)
- REQ-NOT-001 — Notificações via RabbitMQ com ack/retry/DLQ; nunca duplicadas em casos concorrentes (OPS-07)

### Customer (CST) / IAM
- REQ-CST-001 — Perfil, endereços, preferências e consentimento por cliente; PII com política de retenção (INV-PII-001)
- REQ-IAM-001 — Keycloak OIDC (Auth Code + PKCE); roles: customer, catalog_editor, pricing_manager, inventory_operator, fulfillment_operator, finance_analyst, support_agent, auditor, iam_admin (discovery 08)
- REQ-IAM-002 — Ações escopadas (`catalog:publish`, `offer:price:update`, `inventory:adjust`, `payments:refund`, `roles:grant`…) por market/warehouse/seller (discovery 08)
- REQ-IAM-003 — MFA obrigatório para operadores privilegiados; deny-by-default; mTLS/identity em gRPC/worker (mestre §7)

### Admin Control Plane (ADM) — owner: Control Modular Monolith
- REQ-ADM-001 — Control não escreve nos Mongo de Catalog/Offer/Inventory/Order/Payment; comanda owners via gRPC autenticado (mestre §3.1)
- REQ-ADM-002 — Control possui Mongo próprio apenas para approvals/cases/audit (ADR-0001)
- REQ-ADM-003 — Auditoria append-only lógica com actor, ação, escopo, before/after hash, motivo, correlation (mestre §7)
- REQ-ADM-004 — Dashboard exibe `source_version`/`projected_at` e estado PUBLISHING/SYNCING (mestre §4.2)

### AI Shopping Assistant (AI)
- REQ-AI-001 — Assistente em MAF C# (`Microsoft.Agents.AI` 1.26.0+), provider substituível via `IChatClient` (ADR-0008)
- REQ-AI-002 — Tools somente leitura: SearchCatalog, GetProductDetails, GetCurrentOffer, GetInventoryHint, CompareOffers, EstimateShipping, GetCustomerCart, ProposeCartChange (inerte) (mestre §9.3)
- REQ-AI-003 — Mutação de carrinho exclusivamente via consent artifact validado no servidor (ADR-0009, INV-AI-001)
- REQ-AI-004 — Grounding: somente IDs/preços/estoque retornados por owners com timestamp/TTL; nunca inventa valores (§9.7)
- REQ-AI-005 — Prompt injection em descrições/reviews não amplia escopo; guardrails em código, não só system prompt (§9.6)
- REQ-AI-006 — Sessões isoladas entre clientes (histórico, tokens, carrinho, permissões) (§9.7)
- REQ-AI-007 — Fallback para busca convencional quando modelo indisponível, sem afetar checkout/admin (§9.7)
- REQ-AI-008 — Métricas: tool calls, denial rate, tokens, latência p50/95/99, custo; traces sem prompts completos/PII (§9.6)

### Plataforma (PLT) — transversal
- REQ-PLT-001 — Aggregate + outbox na mesma transação Mongo; publicação única via CDC→Kafka; tasks Rabbit nascem de bridge idempotente (mestre §4.2)
- REQ-PLT-002 — Projeções com `processed_events` na mesma transação PG; replay/backfill determinístico (mestre §4.2)
- REQ-PLT-003 — Testes de arquitetura automatizados: Domain sem ASP.NET/Mongo/EF/MAF; Control sem adapters owners; sem ciclos (mestre §18.4)
- REQ-PLT-004 — Contratos: proto com Buf lint/breaking, OpenAPI versionado, envelope de evento versionado (mestre §4.2)
- REQ-PLT-005 — Observabilidade OTel ponta a ponta com correlation/trace propagation; sem labels de alta cardinalidade (mestre §10.3)

## 3. Requisitos não funcionais (NFR-*) — propostos, nunca medidos

- NFR-PERF-001 — Latência p95 catálogo cache-hit < 200 ms; busca uncached < 500 ms (dataset/hardware especificados)
- NFR-PERF-002 — Latência p95 aceite de checkout < 1,5 s (aceite PENDING, sem esperar PSP)
- NFR-PERF-003 — Projection lag p99 < 5 s em carga nominal
- NFR-REL-001 — Disponibilidade consulta catálogo 99,95%; aceite checkout 99,9% (30 dias)
- NFR-REL-002 — Pedidos duplicados por retry = 0 (fault injection, cada release); oversell não autorizado = 0 (testes concorrentes)
- NFR-REL-003 — Pagamentos 100% reconciliáveis diariamente; diferenças sinalizadas
- NFR-REL-004 — RTO 30 min / RPO 5 min (propostas; validar com restore drill)
- NFR-SCALE-001 — Benchmarks L0–L4 congelados ANTES do teste; resultados com ambiente/dataset/seed/percentis; nunca extrapolar linearmente (mestre §10.2)
- NFR-OBS-001 — Dashboards: checkout, estoque, outbox age, consumer lag, DLQ, PSP UNKNOWN, cache hit, AI tools/custo (references/dotnet/01 §5)

## 4. Segurança (SEC-*)

- SEC-001 — Proteção BOLA/IDOR em todos os endpoints por objeto; multi-tenant sem leakage
- SEC-002 — Webhooks PSP assinados/autenticados; replay e falsificação rejeitados
- SEC-003 — CSRF/XSS/SSRF/mass assignment/rate-limit/brute-force abordados por design (mestre §7)
- SEC-004 — Segredos fora do Git; rotação; nenhuma credencial admin permanente no frontend
- SEC-005 — Upload de mídia com scanning; supply chain scanning; SBOM
- SEC-006 — Prompt injection (descrições, reviews, links) tratada como entrada não confiável; sem exfiltração de PII/tokens ao modelo
- SEC-007 — LGPD/GDPR como requisitos de design; **não** alegar conformidade/certificação sem auditoria legal

## 5. IA (AI-*) — critérios de aceite M7

- AI-001 — Smoke real do MAF pinado em C# com provider aprovado (ou limitação registrada)
- AI-002 — Injeção em descrição de produto tentando checkout/preço/token → negação garantida por ausência de tools + policy
- AI-003 — Consent token usado para payload/subject diferente → rejeitado no servidor
- AI-004 — Provider timeout → fallback de busca dentro do budget de latência; checkout/admin intactos
- AI-005 — Dataset de avaliação versionado (pt-BR/en-US/es-ES): grounding, tool choice, injection success rate, approval bypass = 0
