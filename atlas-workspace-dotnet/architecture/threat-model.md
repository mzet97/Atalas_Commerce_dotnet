# Atlas Commerce .NET — Modelo de Ameaças (draft M0)

**Status:** DRAFT (M0) — consolidação das ameaças citadas no contrato; refinado por contexto nos milestones respectivos. Estrutura STRIDE + ameaças específicas de IA.

## 1. Superfície e confiança

- Confiança zero entre componentes: UI nunca é perímetro (INV-SEC-001); owners revalidam tudo.
- Fronteiras: browser→Gateway/BFF; Angular→Control Host; serviços↔serviços (gRPC mTLS/identity); owners↔Mongo; CDC↔Kafka; workers↔PG; webhooks PSP→Payments; modelo LLM↔assistant (dados untrusted).
- Ativos: dinheiro (intents/captures/refunds), estoque (positions/reservations), PII (cliente, endereços, sessões), identidade (tokens/grants), integridade de eventos e auditoria.

## 2. STRIDE por contexto (resumo)

| Contexto | Ameaças principais | Controles planejados |
|---|---|---|
| Checkout/Orders | Negar serviço em reserva; replay de SubmitCheckout; oversell por corrida | Reserva CAS idempotente (INV-STOCK-001/003); rate limit; backpressure |
| Payments | Webhook forjado/replay; timeout tratado como sucesso; refund fraudulento | Assinatura + dedup + reconciliação (INV-PAYMENT-001); four-eyes p/ refund (ADM) |
| Inventory | Ajuste não autorizado; redução abaixo do disponível; ledger apagado | ABAC por warehouse + four-eyes + append-only (INV-STOCK-002) |
| Catalog/Offers | Publicação sem aprovação; preço alterado retroativamente; injeção em campos de texto | Workflow de aprovação; snapshot imutável de Order (INV-ORDER-001); sanitização/untrusted |
| IAM | Escalada de privilégio; grant permanente; session hijack | MFA admins; grants com expiração; deny-by-default; auditoria |
| Admin/Control | Self-approval; trilha adulterada; comando forjado ao owner | requested_by != approved_by (INV-ADMIN-001); hash before/after; gRPC autenticado |
| Analytics/Projections | Exfiltração via projeção; envenenamento por eventos duplicados | Acesso por contrato; dedup transacional (INV-EVT-001) |
| Comunicação | Dual write perdido; ordem invertida de eventos | Outbox transacional; inbox por aggregate_version (ADR-0003) |

## 3. Ameaças específicas de IA/MAF (contexto Shopping Assistant)

| Ameaça | Vetor | Controle |
|---|---|---|
| Prompt injection em descrições/reviews/links | conteúdo do catálogo tratado como instrução | outputs de tool = untrusted; guardrails em código; sem tools de mutação (ADR-0009/0015) |
| Bypass de consentimento | "o usuário disse sim no chat" | consent artifact com nonce/TTL/hash (AI-003) |
| Alucinação de preço/estoque | modelo inventa valores | grounding por IDs de owner + quoted_at/TTL (REQ-AI-004) |
| Vazamento cross-session | dois clientes simultâneos | isolamento de sessão/grants (REQ-AI-006, AI-005) |
| Exfiltração de PII/tokens ao provider | prompt contendo dados sensíveis | minimização, redação, provider local preferido (INV-PII-001) |
| Tool abusa/exaustão de custo | loops de tool, requests caros | quotas tokens/custo/tempo/recursão; circuit breaker (ADR-0015) |
| MCP externo malicioso | ferramenta desconhecida | sem MCP por padrão; allowlist obrigatória |

## 4. OWASP API Top 10 — cobertura planejada

BOLA/IDOR (autorização por objeto em todos os endpoints), autenticação quebrada (OIDC validado issuer/audience/expiry), autorização excessiva (least privilege + ABAC), recursos ilimitados (rate limit + quotas de payload), mass assignment (DTOs explícitos, nunca bind de entidade), SSRF (allowlist de egress), misconfig (configs versionadas, segredos fora do Git), injeção (validação de schema), gestão de inventário de API (OpenAPI versionado), uso inseguro de terceiros (SBOM, scanning).

## 5. Pendências (para milestones futuros)

- Threat model detalhado por serviço (M2+), com diagramas de fluxo de dados.
- Cenários de ataque automatizados nos testes de segurança (M3/M7): webhooks forjados, injection, cross-session, oversell.
- Revisão legal LGPD/GDPR antes de produção — **sem alegação de conformidade** (SEC-007).
