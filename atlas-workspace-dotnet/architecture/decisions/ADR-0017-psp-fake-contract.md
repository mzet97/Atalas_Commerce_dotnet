# ADR-0017 — Contrato do PSP fake determinístico

**Status:** PROPOSED · **Data:** 2026-10-28

## Contexto
O M3 exige um PSP simulado para exercitar saga/webhooks sem gateway real; ele deve ser determinístico, assinado e inequivocamente simulado (INV-PAYMENT-001: "pagamento fake não pode aparentar gateway real").

## Alternativas
1. **Serviço ASP.NET Core isolado (Compose profile `simulated-external`) com API REST + webhooks HMAC-SHA256, cenário selecionado por campo `test_scenario` no token de pagamento.** (proposta)
2. Biblioteca in-process nos testes — rejeitada: não exercita HTTP/retry/assinatura/rede do Payments Service.
3. Gateway sandbox real de nuvem — fora de escopo M3 (custo/conta externa).

## Decisão proposta
- Endpoints: `POST /v1/authorizations`, `/v1/captures`, `/v1/refunds`; callback `POST /webhook` com header `X-Simulated-Signature: HMAC(body, shared_secret)`.
- Cenários por `test_scenario ∈ {APPROVED, DECLINED, TIMEOUT, UNKNOWN, WEBHOOK_DUPLICATE, WEBHOOK_OUT_OF_ORDER, REFUND}`; latência/injetáveis de falha configuráveis por header.
- Identidade visual/naming "SIMULATED-PSP"; resposta inclui `"simulated": true`; nunca usado fora de dev/CI; segredo via env.

## Trade-offs / rollback
(+) cobre toda a matriz de falhas do contrato; (−) manutenção de um serviço extra. Rollback: desativar profile; substituível por sandbox real em fase 2 mantendo o adapter (`IPspAdapter`).
