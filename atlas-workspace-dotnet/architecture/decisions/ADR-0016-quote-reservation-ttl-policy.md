# ADR-0016 — Política de TTL/expiração de Quote e Reservation

**Status:** PROPOSED · **Data:** 2026-10-28

## Contexto
A saga de checkout (ADR-0006) depende de janelas temporais: a quote precisa expirar para forçar re-cotação (INV-QUOTE-001) e a reserva precisa devolver saldo quando o checkout não avança (INV-STOCK-002) — sem que TTL seja a única forma de devolução.

## Alternativas
1. **TTL configurável por market com valores-padrão (quote ~10 min; reserva ~15 min) e job de domínio que materializa a transição `Expiring→Expired` como evento; TTL do Mongo apenas acelera limpeza física.** (proposta)
2. TTL puro do MongoDB sem transição de domínio — rejeitado: viola INV-STOCK-002 (TTL não é a única devolução) e impede evento `ReservationExpired`.
3. Sem expiração (reserva até ação explícita) — rejeitado: deadlock de estoque sob abandono de checkout.

## Decisão proposta
- `PriceQuote.expires_at = issued_at + TTL(market)` (default 10 min, configurável); confirmação valida expiração com clock do servidor (`TimeProvider`), nunca do cliente.
- `Reservation` tem `expires_at` (default 15 min); **worker de domínio** varre `Active` vencidas → transição explícita `Expired` + devolve `reserved` por CAS + evento; índice TTL Mongo apenas para limpeza física tardia.
- Valores por environment em configuração validada (Options + ValidateOnStart).

## Trade-offs / falhas / rollback
(+) semântica auditável e evento consumível; (−) custo do worker periódico. Falha: worker parado → reservas não expiram (monitorar idade máxima). Rollback: parâmetros por env, sem schema.
