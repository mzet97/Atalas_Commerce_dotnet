# SPEC-003 — spec.md

**Problema:** permitir compra real de ponta a ponta no ambiente local com saga persistida, sem oversell, sem pedido pago duplicado e sem perda silenciosa sob falhas (timeout PSP, webhook duplicado/fora de ordem, queda de worker).

**Atores:** cliente autenticado; PSP fake (sistema externo simulado); operador (leitura de estado).

## Requisitos (herdam REQ-CRT/CHK/ORD/PAY + INV-MONEY/ORDER/PAYMENT/CQRS/QUOTE)

1. **Cart:** add/remove/validar quantidade; guest→customer merge; estoque 0 não promete checkout.
2. **Quote:** cotação versionada com breakdown (itens, descontos, frete estimado, tributos MVP) e TTL (política ADR-0016); confirmação exige quote vigente; vencida → re-cotação com diff (COM-08).
3. **SubmitCheckout:** idempotente por `idempotency_key`; valida quote/endereço/elegibilidade; reserva no Inventory (idempotente); cria Order `PENDING_PAYMENT` com snapshot imutável (itens, offers, moeda, endereço); PaymentIntent com idempotência PSP; retorno inequívoco CONFIRMED/PENDING/REJECTED.
4. **PSP fake:** adapter HTTP determinístico com cenários `APPROVED/DECLINED/TIMEOUT/UNKNOWN/WEBHOOK_DUPLICATE/WEBHOOK_OUT_OF_ORDER/REFUND` selecionáveis por token de teste; webhooks assinados (HMAC) e deduplicados; ledger de tentativas.
5. **Saga (ADR-0006):** estados persistidos no Checkout Service (`CheckoutSaga` collection); inbox/dedup; compensações (liberar reserva; marcar PAYMENT_FAILED; refund se capturado); reconciliador periódico para UNKNOWN; reentrante após restart; retry com jitter/limites; circuit breaker no PSP.
6. **Orders:** máquina de estados (diagrama 08) idempotente com histórico; timeline consultável; leitura nunca recalcula totais de offers correntes.
7. **Projeções:** `customer_order_summary`, `order_detail_view`; pagamento/estoque confirmados pelos owners — nunca por leitura (INV-CQRS-001).

## Fluxos alternativos (first-class)

Estoque insuficiente → 409/requote; PSP timeout → `PAYMENT_PENDING` + reconciliação; recusa → liberar reserva + `PAYMENT_FAILED`; reserva expirada com pagamento aprovado → refund + incidente; evento duplicado → dedup; worker cai entre commit/ack → retomada idempotente; cancelamento durante fulfillment (política de janela).

## Dados

`cart.carts`, `checkout.sessions` (quote + idempotency_key + estado + expiração), `checkout.sagas` (estado da saga, tentativas, compensações), `orders.orders` (snapshot + reservation_refs + payment_ref), `payments.intents/attempts` (+webhooks recebidos).

## Critérios de aceite

`acceptance.feature` — UC003-T01..T05 obrigatórios; `oversell=0` parametrizado; `duplicate_paid_order=0`; mesmo idempotency key → mesmo resultado; restart/replay cobertos; E2E Playwright da compra com PSP fake.

## Definition of Done

Cenários PASS com EVD (logs de saga, webhooks, traces, vídeo); Money VO testado (property-based p/ arredondamento); runbook de reconciliação.
