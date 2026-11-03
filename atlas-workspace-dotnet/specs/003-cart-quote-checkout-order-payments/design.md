# SPEC-003 — design.md

ADRs: 0006 (saga), 0002/0003 (CQRS/outbox), 0012 (sem libs de mensageria — workers próprios).

## Componentes

- **Cart Service**: aggregate `Cart`/`CartLine` (guest_id/customer_id, merge no login); APIs REST via Gateway; `CartUpdated` (analytics p/ `funnel_metrics_hourly`).
- **Checkout Service**: `QuoteService` (Strategy: pricing/frete/tributo v1); `CheckoutSession`; **saga persistida** — máquina `SUBMITTED → RESERVED → ORDER_PLACED → AUTHORIZING → (CONFIRMED | COMPENSATING | PENDING_RECONCILIATION)`; cada transição grava evento + inbox; idempotência por chave composta (checkout_id, etapa).
- **Orders Service**: aggregate `Order` (VOs `Money`, `OfferSnapshot`, `OrderLine`); state machine do diagrama 08; `OrderTransition` append-only.
- **Payments Service**: `PaymentIntent/Attempt`; **PSP fake** ASP.NET Core próprio (`simulated-external` profile) com rota `/authorize`, `/capture`, `/refund` e `/webhook` assinado HMAC; cenário selecionado por `test_scenario` no token; jitter/latência configurável; nunca usado como "gateway real".
- **Workers**: `Reconciliation` (varre `PENDING_RECONCILIATION`, consulta PSP, avança/compensa); projeções de pedidos.

## Interações

```text
Gateway → Checkout: SubmitCheckout(idempotency_key, quote_id)
Checkout → Inventory(gRPC): ReserveStock [CAS]
Checkout → Orders(gRPC): PlaceOrder(snapshot, reservation_refs)
Checkout → Payments(gRPC): Authorize(intent_id, idem_key)
Payments → PSP fake(HTTP+HMAC): authorize → sync/webhook
Payments --evento--> outbox→Kafka → Checkout saga avança → Confirm/Compensate
```

## Money e arredondamento

`Money(long AmountMinor, Currency)` com `exponent` por moeda; arredondamento banker's rounding por padrão; FsCheck para propriedades (soma de linhas+impostos−descontos = total, comutatividade).

## Falhas e compensações (matriz)

| Falha | Estado | Ação |
|---|---|---|
| Estoque insuficiente | REJECTED | 409 + requote hint |
| PSP DECLINED | PAYMENT_FAILED | ReleaseReservation + audit |
| PSP TIMEOUT/UNKNOWN | PAYMENT_PENDING | reconciliador (poll PSP) → confirm/refund |
| Webhook duplicado | — | dedup por event_id PSP |
| Webhook fora de ordem | — | rejeita transição inválida, aguarda |
| Worker cai pós-commit | — | inbox retoma; saga reentrante |
| Reserva expirada + pago | incidente | refund + caso de conciliação |

## Testes

Unit (VOs, state machines, saga transitions), Integration (Testcontainers: Mongo rs, Kafka, PSP fake), Fault injection (kill worker entre commit/ack — harness docker-only), Contract (proto checkout↔owners), E2E Playwright (compra cenários APPROVED/DECLINED/TIMEOUT), Property-based (Money).

## Observabilidade

Span por etapa da saga com `checkout_id`/`order_id` em atributos (não em labels); métricas `saga_state_duration`, `payment_unknown_total`, `duplicate_webhook_total`, `compensation_total`; alerta para `PENDING_RECONCILIATION` idoso.
