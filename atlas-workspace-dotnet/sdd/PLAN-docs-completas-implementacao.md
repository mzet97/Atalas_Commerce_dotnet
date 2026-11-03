# Plano de Execução — Documentação SDD Completa + Implementação em Código

**Data:** 2026-10-27 · **Base:** contrato `sdd/prompt.md` (§11–§12, §17) · **Estado M0:** PASS (commit `300193b`)

## Princípios

1. **Dois estágios, a pedido do proprietário:** (A) escrever **toda** a documentação SDD primeiro; (B) implementar em código por milestone. A instrução do proprietário tem a maior precedência (§1 do contrato).
2. **Docs antecipadas = `DRAFT`:** como o contrato exige specs refinadas just-in-time, as SPECs dos milestones futuros serão criadas completas agora com status `DRAFT/BACKLOG` e **refinadas para `READY`** ao entrar no milestone — evitando especulação fossilizada.
3. **`PASS` só com EVD real** em todo o Estágio B; Gherkin sem teste executado continua sendo contrato, não resultado.
4. **Nada de código no Estágio A** (exceto artefatos de documentação declarativos: YAML de OpenAPI draft, `.proto` draft, JSON Schema do envelope — são contratos escritos, não software).

---

## ESTÁGIO A — Documentação SDD completa (pré-código)

**Entrega:** ~100–120 arquivos novos; 1–2 sessões de trabalho.

### A1. SPEC-001 (Platform Foundation) → `READY`
Completar o rascunho: spec/design/tasks finais + `acceptance.feature` (M1 gate: build/test/health, auth real, trace cross-process, CI verde, rebuild determinístico) + traceability/evidence/risks.

### A2. SPEC-002..011 completas (10 specs × 9 arquivos padrão)
Criar em `specs/` com conteúdo estrutural completo (status `DRAFT`), cada uma cobrindo: problema/atores, requisitos ligados a REQ/INV existentes, comandos/queries/eventos/contratos/dados por agregado, fluxos alternativos, falhas/compensações, ameaças, cenários Gherkin com TEST-IDs, tasks com comando de aceite, riscos, rastreabilidade.

| SPEC | Milestone | Conteúdo nuclear (write owners entre parênteses) | Cenários-chave |
|---|---|---|---|
| SPEC-002 catalog-offers-inventory | M2 | Product/SKU/Offer/StockPosition/Reservation; publicação com aprovação; outbox→CDC→Kafka→PG→Redis→Next; escopos ABAC (Catalog, Offers, Inventory) | contenda última unidade; 403 sem escopo; evento repetido; PUBLISHING/SYNCING |
| SPEC-003 checkout-saga | M3 | Cart, Quote (TTL), CheckoutSession, saga Order/PSP fake, reconciliação (Checkout, Orders, Payments) | UC003-T01..T05; webhooks duplicados/fora de ordem; UNKNOWN |
| SPEC-004 admin-hardening | M4 | Four-eyes, revogação, auditoria imutável, dashboards com freshness (Control) | self-approval negado; escopo revogado no owner |
| SPEC-005 storefront-ux-i18n | M5 | Busca/facets PG, 3 idiomas/mercados, SEO/hreflang, a11y WCAG 2.2 AA, CWV (Storefront front) | CAT-01 fallback; E2E trilíngue; perda de conexão |
| SPEC-006 fulfillment-returns-notifications | M6 | Shipment/Tracking (adapter fake), RMA≠Refund, RabbitMQ ack/retry/DLQ (Fulfillment, Returns, Notifications) | refund coerente; DLQ/replay; notificação não duplicada |
| SPEC-007 maf-shopping-assistant | M7 | MAF workflows, tools read-only, consent artifact, grounding, eval dataset, fallback (AI Service) | injection não amplia; consent para payload diverso = reject; isolamento sessões |
| SPEC-008 k8s-operacional | M8 | Helm/Skaffold, HPA/KEDA, NetworkPolicy, secrets, backups/restore drill | rollout/rollback; restore verificado |
| SPEC-009 benchmark-escala | M9 | Planos L0–L4 com gates congeláveis, dataset/seed, metodologia soak/spike/chaos, custo | relatório reproduzível; limitações explícitas |
| SPEC-010 marketplace-3p | M10 | Sellers/tenancy/split — estrutura mínima `DEFERRED` | zero vazamento de tenant (quando ativar) |
| SPEC-011 portfolio-internacional | M11 | ADRs EN, trade-off essays, demo/READMEEN, matriz projetado×implementado×medido | clone→run→verify externo |

### A3. ADRs complementares (conforme lacunas encontradas ao detalhar specs)
Candidatas: ADR-0016 (TTL/expiração de Quote e Reservation — política fina da saga), ADR-0017 (contrato do PSP fake: cenários determinísticos + assinatura de webhook), ADR-0018 (taxonomia de tópicos Kafka e naming de eventos), ADR-0019 (estratégia de testes de arquitetura: NetArchTest vs reflection). Cada uma `PROPOSED`.

### A4. Contratos wire em forma de documentação (rascunhos declarativos)
- `contracts/openapi/` drafts: Gateway/BFF (produtos, busca, carrinho, checkout), Control Admin API.
- `contracts/proto/` drafts: admin→owner commands (PublishProduct, PublishOffer, AdjustStock), queries internas.
- `contracts/asyncapi/` + JSON Schema do envelope de evento (INV-EVT-001) e tópicos por contexto.
Aprovados formalmente no M1/M2 via Buf/OpenAPI gates; aqui são fonte de verdade documental.

### A5. Especificação do dataset de avaliação da IA (M7)
Golden scenarios pt-BR/en-US/es-ES (intenções, ambiguidade, injection), métricas (groundedness, tool choice, approval bypass=0) e formato versionado.

### A6. Planos de benchmark M9 (documentação prévia)
Gates L0–L4 com cargas/dataset/hardware a declarar; metodologia; templates de relatório (`results/<run_id>/`).

### A7. Consolidação
Atualizar `sdd/traceability.md` global com todas as TASK/TEST previstas; revisar `risks.md`/`decisions-open.md`; validação de consistência automatizada (script: INV↔REQ↔SPEC↔cenário↔task); commit final do Estágio A + relatório "Doc Wave".

### A8. Ponto de controle com o proprietário (recomendado)
ADRs que exigem decisão antes de código: uso de Aspire (ADR-0010), runner de CI (GitHub Actions vs local), orçamento/cloud para M7 (padrão: Ollama local, zero custo). Sem resposta, seguem os defaults documentados.

---

## ESTÁGIO B — Implementação em código (por milestone)

**Ciclo por milestone (obrigatório):** RECON → spec `READY` (refinar DRAFT) → tasks `READY` → TEST-FIRST nos riscos maiores → fatia vertical → VERIFY (unit/integration/contract/e2e conforme risco) → EVIDENCE (comandos, exit codes, traces) → relatório §16 → atualizar traceability/progress/ADRs.

### B0. Pré-requisitos de ambiente (antes de qualquer código)
Instalar/verificar: **Docker Engine + Compose** e **Buf** (bloqueiam M1); validar `dotnet --list-runtimes` (net10.0); Testcontainers exigirá Docker. (kubectl/helm/skaffold só no M8; aspire opcional.)

### B1. M1 — Platform Foundation (SPEC-001)
Criar os 3 repos (`atlas-storefront-dotnet`, `atlas-control-dotnet`, `atlas-platform-contracts-dotnet`) com `.slnx`, `global.json` (SDK 10.0.403), CPM, EditorConfig, CI (build/test/format/lint/SAST/SBOM); templates `dotnet new` testados; 1 serviço ASP.NET Core 10 real + host modular Control (health/trace); Compose `core`+`data` (Mongo replica set, PG, Keycloak realm, Redis); smoke OIDC cliente/admin; OTel→Grafana com trace cross-process; `dependency-matrix.md` revalidada. **Distribuir specs por repo** (cada repo recebe suas SPECs relevantes).
**Gate M1:** build/test/health válidos; auth real; CI verde; rebuild determinístico.

### B2. M2 — Catálogo + Offers + Inventory (SPEC-002) — 1º slice comercial
Owners com Domain/Application/Infrastructure/Api; agregados + outbox transacional; CDC→Kafka; projection workers idempotentes; PG views; Redis cache; Gateway/BFF (ADR-0014); Angular admin → Control → gRPC → owners; Next.js lendo projeções; testes de arquitetura.
**Gate M2:** publicação real ponta a ponta; 403 sem escopo auditado; evento repetido não duplica; corrida pela última unidade vende no máx. 1.

### B3. M3 — Cart/Quote/Checkout Saga/Order/PSP Fake (SPEC-003)
Money VO, state machines, saga persistida com inbox/dedup/compensação/reconciliador; PSP fake determinístico com webhooks assinados; E2E Playwright da compra.
**Gate M3:** `oversell=0`; `duplicate_paid_order=0`; T01–T05 PASS; restart/replay cobertos.

### B4. M4 — Admin hardening (SPEC-004) *(paralelo parcial com M5)*
Four-eyes, revogação, auditoria append-only, dashboards com `source_version`/lag.
**Gate M4:** self-approval negado+auditado; escopos revalidados no owner.

### B5. M5 — Storefront UX/i18n (SPEC-005)
Busca com facets (GIN), 3 idiomas, SEO/hreflang, a11y, CWV medidos; jornadas com estados loading/empty/error/offline.
**Gate M5:** E2E trilíngue; CWV/a11y com evidência e limitações.

### B6. M6 — Fulfillment/Returns/Notifications (SPEC-006)
Shipment/tracking via adapter fake; RMA e refund como fluxos distintos; RabbitMQ com ack/retry/DLQ.
**Gate M6:** refund coerente com pagamento/reserva; DLQ/replay provados.

### B7. M7 — Assistente MAF (SPEC-007)
`AI/ShoppingAssistant` completo; provider `IChatClient` (Ollama validado para tool calling); tools read-only; consent artifact; eval dataset executado; observabilidade de custo/latência; fallback.
**Gate M7:** smoke real MAF pinado; injection não amplia escopo; consent inviolável; fallback OK.

### B8. M8 — Kubernetes on-prem (SPEC-008)
Helm charts, Skaffold, HPA/KEDA, NetworkPolicy, secrets, backups; restore drill.
**Gate M8:** rollout/rollback; restore funcional com checkout verificado pós-restore.

### B9. M9 — Benchmark/falhas/custo (SPEC-009)
Executar planos L0–L4 com gates congelados; chaos/soak; relatório com percentis/erros/recursos/custo.
**Gate M9:** resultados reproduzíveis; nunca alegar milhões/s sem medição.

### B10. M11 — Portfólio internacional (SPEC-011)
ADRs/essays em inglês, READMEs dos 3 repos, demo/vídeos, matriz projetado×implementado×medido, postmortems honestos.
**Gate M11:** avaliador externo clona, sobe e verifica. (**M10 permanece DEFERRED.**)

### Critério de conclusão do Estágio B
Todos os gates M1–M9 e M11 com `PASS` + EVD datada e vinculada a commit; matriz de rastreabilidade REQ→SPEC→TASK→código→TEST→EVD completa; zero alegação não verificada.

---

## Dependências e ordem

```text
A1→A2→(A3∥A4∥A5∥A6)→A7→[A8 checkpoint proprietário]
B0 → B1(M1) → B2(M2) → B3(M3) → {B4(M4) ∥ parcial B5(M5)} → B6(M6) → B7(M7) → B8(M8) → B9(M9) → B10(M11)
```

## Decisões que preciso do proprietário (defaults se sem resposta)

1. **Instalação de ferramentas** (Docker+Compose, Buf) no ambiente — default: instalar no início do Estágio B.
2. **CI:** GitHub Actions vs runners locais — default: GitHub Actions nos 3 repos.
3. **M7 modelo:** Ollama local (zero custo) vs cloud com orçamento — default: local; cloud só com aprovação explícita.
4. **Revisão do Estágio A** antes de codar — default: apresento resumo e sigo no próximo ciclo, salvo pedido de parada.

## Riscos do plano

- Docs futuras especulativas → mitigado por status `DRAFT` + refinamento no milestone (regra 2).
- Ambiente sem Docker trava M1 → instalação é a B0/TASK-001-001.
- Volatilidade do MAF → pinar 1.26.0 + smoke antes de codar o módulo AI (M7).
- Escopo humano: M1–M11 é um programa longo; fatias verticais entregam valor a cada milestone.
