# Plano — Implementar tudo que falta (fechar M4 → M5 → M6 → M7 → M8 → M9 → M11)

Base: contrato mestre + `PLAN-docs-completas-implementacao.md` (Estágio B) + digest das specs 005–011 (todas DRAFT → refinar para READY ao entrar em cada milestone). Regras permanentes: SDD evidence-first (PASS só com EVD real, relatório §16 por gate, commits por fatia), proibições do contrato (sem Python/SK/LangGraph), invariantes INV-* intocáveis, hosts dev como background tasks.

## FASE 0 — Fechar M4/SPEC-004 (~1 sessão)
1. Verificador de cadeia como teste xUnit (append N registros, mutar 1 no Mongo, assert `brokenAtSeq`).
2. Req.5: paginação/filtro server-side nas listagens (`/modules/audit`, approvals, positions da view PG).
3. Req.6: `GET /modules/ops/overview` — outbox age por owner, contagem de sagas por estado (conciliação), DLQ depth como "N/A até M6".
4. Runbook de revogação de emergência + `GET /modules/audit/export` (JSON/CSV) para auditor.
5. ADR-0012 fechada como NOT_ADOPTED (sem MediatR/MassTransit/FluentAssertions — dispatch manual + Kafka nativo + xUnit). ADR-0010 idem (Aspire não adotado).
6. Relatório M4 §16 final + EVD-004-002 + gate **M4 PASS**.

## FASE EXTERNA (proprietário, paralela)
`git push` dos 3 repos + secrets do GitHub Actions — fecha o "CI verde" do M1 e ativa CI para todos os gates seguintes (workflows já existem nos templates).

## FASE 1 — M5/SPEC-005 Storefront UX, busca, i18n + Playwright (~3–4 sessões)
Spec DRAFT→READY. A maior fatia nova (a UI de carrinho/checkout não existe ainda).
1. Vitrine: busca com facets/sugestões sobre `catalog_product_search` (GIN) + PDP lendo views PG (com revalidação no owner no checkout — INV-CQRS-001).
2. **Carrinho + checkout UI** conectados ao Checkout.Api real (POST /quotes, POST /checkout) com estados loading/empty/error.
3. i18n pt-BR/en-US/es-ES × mercados BR/US/UE: dicionários por namespace, locale/market/moeda INDEPENDENTES (INV-I18N-001), fallback CAT-01 (produto sem tradução não quebra), FX só indicativa.
4. SEO: `generateMetadata`, hreflang/canonical/sitemap/OG.
5. **Playwright E2E**: jornada de compra completa nos 3 idiomas + falha controlada (PSP DECLINED) — fecha o E2E de browser herdado do M3.
6. a11y (axe, WCAG 2.2 AA no CI) + CWV medidos (LCP/CLS/INP) com orçamentos e limitações explícitas. Gate §16 M5.
(Redis cache-aside pode entrar aqui na PDP/busca — primeira uso real do Redis no pipeline.)

## FASE 2 — M6/SPEC-006 Fulfillment, Returns, Notifications (~3 sessões)
Spec→READY. 
1. `FulfillmentOrder` (evento `OrderConfirmed` → state machine CREATED→…→DELIVERED) + multi-remessa (`ShipmentLine`).
2. **Release físico de estoque** no cancelamento via gRPC real (fecha limitação herdada do M3/M4 — port deixa de ser stub).
3. Carrier fake determinístico (quote/label/tracking/POD) documentado como simulado.
4. RMA (janela por market) + **refund four-eyes** (REQ-RET-001): `RefundRequested` → Payments idempotente → PSP cenário REFUND; refund parcial; logística reversa ≠ financeira.
5. Notificações: task bridge Kafka→RabbitMQ (ADR-0004), quorum queues, manual ack, retry/backoff, DLQ `notifications.dlq`, dedup por `notification_key`, consentimento/opt-out (REQ-CST-001), templates i18n.
6. Fault tests: broker restart, crash pós-ack parcial, eventos fora de ordem rejeitados. Runbook DLQ/replay. Gate §16 M6.

## FASE 3 — M7/SPEC-007 Assistente MAF (~3–4 sessões, risco mais alto)
0. **SPIKE PRIMEIRO (ADR-0008)**: Ollama local + `IChatClient` + tool calling com MAF 1.26.0 pinado. Se falhar → BLOCKED documentado, fake em CI, sem alegar IA funcional (decisão do proprietário: Ollama zero-custo).
1. Scaffolding `AI/ShoppingAssistant` (Api/Domain/Application/Workflows/Agents/Tools/Providers/Memory/Safety/Evaluation/Infrastructure) + adapter Ollama + fake determinístico p/ CI.
2. 8 tools read-only allowlist EM CÓDIGO (SearchCatalog…ProposeCartChange inerte), grounding com `quoted_at`/TTL — zero invenção de preço/estoque.
3. Workflow F2 tipado: Intent→Search→Compare→Clarify?→Recommend→ProposeCart? com quotas (tokens/tempo/recursão) + circuit breaker; nunca orquestra saga/checkout.
4. Consent artifact (ADR-0009: subject+nonce+TTL+hash canônico) → única mutação via `ApplyApprovedCartChange` no Cart owner; token p/ payload diverso = rejeitado.
5. Guardrails injection (casos `inj-desc-01`/`inj-review-02` do dataset) — outputs de tools = untrusted; fallback para busca convencional dentro do budget de latência.
6. **Eval com Ollama real** (dataset `eval-dataset.md` já existe): tool choice ≥0.9, clarify ≥0.8, injection bypass=0, hallucination=0, approval bypass=0; métricas REQ-AI-008; versão do modelo registrada. Gate §16 M7.

## FASE 4 — M8/SPEC-008 K8s on-prem (~2–3 sessões)
Pré-requisito: instalar kubectl/helm/skaffold. Ambiente: **kind local como cluster não-prod por padrão**; cluster on-prem real se o proprietário disponibilizar.
1. Charts Helm por repo (versionados, values por env) + `helm lint`/`template` no CI; Skaffold dev.
2. Hardening: NetworkPolicy deny-by-default, non-root/seccomp, probes, graceful shutdown, PDB/quotas.
3. HPA (CPU) + KEDA (consumer lag) escalando sob carga sintética; zero segredo em Git (sealed-secrets).
4. Backups + **restore drill** com checkout smoke verificável pós-restore; rolling+rollback gravados em vídeo/log. Gate §16 M8.

## FASE 5 — M9/SPEC-009 Benchmark (~2–3 sessões; runs longos em background)
Plano L0–L4 já existe (anexo normativo). Gates congelados ANTES de cada run; carga aberta k6; mix 80/12/5/2/0.7/0.3; dataset 10^6 SKUs gerado e versionado.
1. Harness k6 + geração do dataset.
2. **L0** (50 RPS/1 checkout·s, 10 min — prova correção) e **L1** (1000/20, 30 min + soak 2h — baseline p50/95/99) com hardware fingerprint + artefatos `results/<run_id>/`.
3. **L2** (10k/200, 1h + spike 10×) em kind com HPA; invariantes testadas DURANTE o run (oversell=0, duplicate_paid_order=0); hot-SKU contention run; réplicas×caches com IC 95%.
4. Relatório final honesto (limitações explícitas) + congelar SLOs por ADR (fecha OPEN-05). **L3 conforme ambiente; L4 nunca alegado.** Gate §16 M9.

## FASE 6 — M11/SPEC-011 Portfólio internacional (~1–2 sessões)
1. ADRs em EN + trade-off essays (monolith vs microservices, CQRS poliglota, at-least-once+idempotência, consent boundary da IA, saga vs 2PC).
2. READMEs quickstart nos 3 repos + demo scripts + vídeos (Playwright trace: checkout com falha controlada, admin→projeção, assistente com consent).
3. Matriz **projetado×implementado×medido** com links EVD; postmortems dos ~20 bugs reais do projeto; seção obrigatória "o que NÃO está implementado/medido".
4. Gate: **clone→run→verify por avaliador externo** (proprietário ou terceiro).

## Transversal contínuo
- `traceability.md` global atualizado ao fim de cada milestone (item A7 do plano).
- OPEN-02/03 (backorder, emissão fiscal) permanecem fora do MVP — só com decisão do proprietário.
- M10 (marketplace 3P) permanece **DEFERRED** (gatilho: decisão + ADR tenancy).
- Cada gate: SPEC→READY antes, relatório §16 + EVD + commit + atualização AGENTS.md/memória no fim.

## Riscos e mitigação
| Risco | Mitigação |
|---|---|
| ADR-0008 BLOCKED (modelo sem tool calling) | Spike ANTES da spec READY; candidatos: qwen2.5/llama3.1; fallback busca convencional já é requisito |
| M8 sem cluster real | kind local com limitação honesta no relatório |
| M9 hardware insuficiente p/ L2/L3 | Gates congelados antes do run; L3 opcional; resultados não atingidos documentados |
| CI externo não feito | Fases continuam com CI local (dotnet test); CI remoto fecha quando o push acontecer |

Ordem: Fase 0 → Fase 1 (paralelizável com tail da 0) → 2 → 3 → 4 → 5 → 6.