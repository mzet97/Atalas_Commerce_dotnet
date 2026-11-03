# EVD-003-001 — SPEC-003 (parcial): domínios + PSP fake + saga orquestrada

**Data UTC:** 2026-10-29 · **Commits:** `f18994e`, `27074a1`, `200e138` · **Resultado:** PASS (parcial — gate M3 ainda IN_PROGRESS)

## Testes executados (saídas reais)

```text
Orders.Domain        9/9   (snapshot imutável, confirmação idempotente, reconciliação, histórico)
Payments.Domain      9/9   (webhook dedup por id, capture pós-authorize, Unknown→reconcilia,
                            refund só de captured, HMAC do webhook validável e anti-tamper)
Checkout.Domain      7/7   (ordem dos passos, idempotência por passo, compensações)
SimulatedPsp         4/4   (APPROVED→capture, DECLINED 402, TIMEOUT 503, webhook HMAC,
                            identidade SIMULADA)
Checkout.Application 6/6   (T01 contenda estoque compartilhado→1 confirmado;
                            T02 idempotency key→mesmo resultado, sem re-reserva;
                            T03 UNKNOWN→Pending/ReconciliationRequired;
                            DECLINED→compensação (reserva liberada);
                            sem saldo→rejeição pré-pedido; fluxo feliz completo)
TOTAL: 35 testes novos; 115 testes no repo
```

## Fatos reais do ciclo (bugs pegos pelos testes)

1. Saga sem compensação em `Submitted` (OUT_OF_STOCK não marcava Compensated) — estado adicionado às transições válidas.
2. `MarkPaymentAuthorized("intent-created")` avançava a saga sem autorização real — criação do intent ≠ autorização.
3. UNKNOWN não marcava o Order como ReconciliationRequired — faltava a transição no aggregate.
4. Fake de estoque era stateless (não decrementava) — a contenda testava contra estoque infinito. Fix: decremento pós-CAS.
5. PSP TIMEOUT retorna 503 sem body — helper de teste tolerante a body vazio.

## Limitações honestas (restam para o gate M3)

- Reconciliador de UNKNOWN (loop que resolve PendingReconciliation) — não implementado.
- Webhook END-TO-END (Payments Service recebendo o HMAC do PSP via HTTP) — assinatura provada isoladamente.
- Stores Mongo reais de orders/payments/saga + host Checkout.Api.
- Quote (cotação com TTL) e Cart — pendentes.
- E2E Playwright completo — M3 gate.
