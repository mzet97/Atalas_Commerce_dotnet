# EVD-003-008 — Webhook do PSP e2e (UC003-T05) + convergência webhook×reconciliador

**Data UTC:** 2026-11-02 · **Commits:** `ebdf026` (storefront) · **Base:** PSP :5399, Payments.Api :5398 (NOVO host), Checkout.Api :5297, Inventory :5291/5292 · **Resultado:** PASS

## Novo componente: Atlas.Payments.Api

Owner do PaymentIntent ganhou superfície HTTP real:
- `POST /webhooks/psp` — **HMAC-SHA256 hex do corpo bruto** obrigatório (comparação
  constant-time; 401 sem header ou assinatura falsa); payload snake_case do PSP fake
  (`{id, type, provider_reference, order_id}`).
- `GET /intents/by-order/{orderId}` — diagnóstico read-only (estado, ref, nº de webhooks
  processados na coleção de dedup).

`MongoPaymentsStore.ApplyWebhookAsync` — UMA transação: **dedup** (`payments.processed_webhooks`,
índice único por webhook id) + **transição CAS** (Unknown→Authorized / Authorized→Captured com
guard de estado) + **outbox** (`Payment.AuthorizedV1`/`Payment.CapturedV1`).

## Bug evitado 2× (padrão do fix do Inventory, EVD-003-006)

11000 **dentro de transação aborta a transação** — no primeiro build, o caminho do dedup
retornava `Duplicate` normalmente: commit falhava e o retry do driver entrava em loop (o teste
T05 pendurou 1m40s pegando o bug). Correções:
- duplicado → sai via **exceção** (`WebhookDuplicateException`) que aborta a txn;
- intent inexistente / estado divergente → também **aborta** (`WebhookRejectedException`) — dedup
  NÃO pode ficar gravado sem efeito aplicado (senão um reenvio legítimo seria engolido).

## Fluxo real executado (run t05-1793650833)

```text
POST /checkout (UNKNOWN)      → HTTP 202 Pending, orderId 0fedfc92-5e8b-…
GET  psp by-key/{orderId}     → AUTHORIZED pr_0fedfc92-5e8 (resposta autenticada)
GET  psp webhook-payload      → body assinado (HMAC secret compartilhado dev)
POST :5398 /webhooks/psp      → {"status":"processed"}   HTTP 200
GET  /intents/by-order        → state=Authorized, providerReference=pr_0fedfc92-5e8
POST MESMO webhook (T05)      → {"status":"duplicate"}   HTTP 200 — intent PERMANECE Authorized
[8 s ciclo do reconciliador]  → saga t05-…: state=Confirmed, providerReference=pr_0fedfc92-5e8
```

Convergência dos DOIS caminhos de resolução sem efeito duplo: o webhook autorizou o intent
(CAS Unknown→Authorized), e o reconciliador confirmou pedido+saga (o CAS do intent do
reconciliador vira no-op com guard — idempotente por construção).

## Testes automatizados (replica set real — Atlas.Payments.Api.Tests 5/5)

| Teste | Prova |
|---|---|
| authorized em Unknown | transição + ref persistida |
| **T05** mesmo webhook id | 1º `processed`, 2º `duplicate`, 1 registro de dedup, transição única |
| assinatura ausente/forged | 401 + intent inalterado |
| captured em Authorized → duplicado id DIFERENTE | `duplicate` idempotente (guard de estado) |
| order inexistente | 404 **sem gravar dedup** → retry posterior `processed` |

## Limitações

- Captura: webhook `payment.authorized` NÃO captura automaticamente no caminho reconciliado
  (política de captura na SPEC-004+; caminho sincrono captura por decisão do owner).
- PSP em memória: webhooks não sobrevivem a restart do PSP (dev only).
- Segredo em config de dev (env); produção via secret store (SPEC-004).
