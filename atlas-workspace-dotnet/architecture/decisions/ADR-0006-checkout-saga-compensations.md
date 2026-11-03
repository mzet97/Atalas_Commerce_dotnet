# ADR-0006 — Saga de checkout persistida com compensações

**Status:** PROPOSED · **Data:** 2026-10-27

## Contexto
Reserva (Inventory), pedido (Orders) e pagamento (PSP) pertencem a owners distintos; não existe transação ACID distribuída. A ordem exata de efeitos colaterais e a recuperação de falhas são decisões de design, não acidente.

## Alternativas
1. **Saga/Process Manager persistida no Checkout Service com estados explícitos, inbox/dedup, compensation, reentrância e reconciliação** (proposta).
2. Orquestração coreografada apenas por eventos — rastreabilidade do estado do fluxo dispersa; difícil reconciliar.
3. Transação de "melhor esforço" (save + publish + charge sem recovery) — proibida: perda silenciosa e cobrança duplicada.

## Decisão proposta
- Ordem de referência (UC-003): validar quote → reservar (idempotente) → criar Order `PENDING_PAYMENT` com snapshot → PaymentIntent (idempotente no PSP) → resultado final (síncrono autenticado OU webhook validado) → confirmar/compensar → evento durável.
- Estados intermediários first-class: PSP timeout/unknown = `PAYMENT_PENDING` + tarefa de reconciliação; webhook duplicado/fora de ordem = dedup + rejeição de transição inválida.
- Compensações explícitas: liberar reserva, marcar `PAYMENT_FAILED`, refund quando captura já ocorreu com reserva expirada (incidente registrado).
- Ledger de tentativas, retry com jitter e limites, circuit breaker no PSP, quarentena de mensagens irrecuperáveis; reconciliador periódico.

## Trade-offs
(+) sem perda silenciosa; duplicatas controladas; auditável. (−) complexidade de estados e testes de falha (T01–T05 obrigatórios no gate M3); latência de reconciliação em casos UNKNOWN.

## Modos de falha / rollback
Falha: worker cai entre commit e ack → retomada idempotente pelo inbox. Rollback de decisão: saga é interna ao Checkout Service; pode evoluir sem quebrar contratos externos.

## Fontes
mestre §4.1, §18.2; blueprint #03 UC-003; diagramas `10-sequence-checkout`, `08-state-order` (cópias em `architecture/diagrams/`).
