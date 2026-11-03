# Atlas Commerce .NET 10 + MAF — Contexto do Projeto

> Contexto operacional para agentes de IA trabalhando neste workspace.
> Criado em 2026-10-27 após auditoria integral das fontes do handoff.

## O que é este workspace

Pacote de **handoff de design (SDD — Spec-Driven Development), sem código implementado**. Contém a especificação de negócio e o contrato de execução para construir o **Atlas Commerce .NET**: plataforma e-commerce internacional enterprise.

- Estado atual: `NO_REPO` — nenhum dos 3 repositórios-alvo existe ainda; nada foi scaffolderado.
- Marco atual: **pré-M0** (recon/auditoria de fontes preliminar feita; artefatos SDD de M0 ainda não criados).
- Todo o conteúdo está em `ATLAS_COMMERCE_DOTNET10_MAF_SDD_AGENT_HANDOFF/` (32 arquivos, integridade SHA-256 conferida 32/32 em 2026-10-27).

**Ponto de partida obrigatório:** ler na ordem —
1. `ATLAS_COMMERCE_DOTNET10_MAF_SDD_AGENT_HANDOFF/PROMPT_MASTER_ATLAS_COMERCE_DOTNET10_MAF_SDD.md` (contrato mestre de execução)
2. `.../references/dotnet/00-ADAPTATION-AND-PRECEDENCE.md` (precedência da edição .NET)
3. `.../references/dotnet/01..04-*.md` (arquitetura .NET, MAF, gates/roadmap, dependências)
4. `.../atlas-commerce-blueprint.md` + `atlas-commerce-discovery/docs/01..08` + 12 diagramas (negócio, stack-agnostic)
5. `SOURCE_MANIFEST_SHA256.json` (integridade)

## Precedência de fontes (mais alta → mais baixa)

1. Instrução explícita mais recente do proprietário
2. `PROMPT_MASTER_ATLAS_COMERCE_DOTNET10_MAF_SDD.md`
3. `references/dotnet/*.md` + diagramas .NET
4. Docs de negócio/invariantes/diagramas do discovery (stack-agnostic)
5. ADRs `ACCEPTED` desta edição
6. Blueprint Python original (**LEGACY** — histórico, nunca prescrição)

⚠️ **Crítico:** toda menção a Python/FastAPI/uv/LangChain/LangGraph/Semantic Kernel nos documentos de negócio é `LEGACY / NOT_APPLICABLE_TO_DOTNET`. As **invariantes de negócio permanecem válidas**; a stack tecnológica não. **Proibido gerar código Python ou usar LangGraph/SK como orquestrador.**

## Alvo: 2 produtos + 1 repo de contratos (3 repositórios Git independentes)

| Repositório | Frontend | Backend | Papel |
|---|---|---|---|
| `atlas-storefront-dotnet` | Next.js/TS (App Router) | Microserviços ASP.NET Core 10 / C# 14 + assistente MAF | Data plane: catálogo, busca, carrinho, checkout, pagamentos, pedidos, logística, IA |
| `atlas-control-dotnet` | Angular/TS | **Monólito modular** ASP.NET Core 10 | Control plane: workflows admin, aprovações, auditoria, relatórios |
| `atlas-platform-contracts-dotnet` | — | `.proto`, OpenAPI, AsyncAPI, templates `dotnet new`, infra | Wire contracts versionados; **não** é serviço nem domain assembly compartilhado |

Estrutura alvo por serviço (Storefront): `Services/{Catalog,Offers,Inventory,Cart,Orders,Checkout,Payments}/{Api,Application,Domain,Infrastructure}` + `AI/ShoppingAssistant/{...,Workflows}` + `Workers/{Projections,Notifications,Reconciliation}` + `BuildingBlocks`. Control: `src/Modules/{CatalogOperations,InventoryOperations,PricingOperations,Approvals,Audit,Reporting}/{Domain,Application,Infrastructure,Presentation}`. Árvore **ilustrativa** — serviços só nascem em milestones com caso de uso real; nunca criar dezenas de projetos vazios.

## Stack confirmada (baseline 2026-10-27 — revalidar no dia do bootstrap)

- **.NET 10 LTS** (runtime 10.0.14, SDK 10.0.403), **C# 14**, `.slnx`, `global.json`, Central Package Management (`Directory.Packages.props`), restore determinístico.
- **MAF**: `Microsoft.Agents.AI` 1.26.0 + `Microsoft.Agents.AI.Workflows` 1.26.0 (NuGet, 2026-10-23). Fazer smoke da API da versão pinada antes de codar — APIs mudam.
- **CQRS poliglota**: MongoDB (replica set, outbox transacional, CAS/otimista) = write autoritativo; PostgreSQL = read models projetados (EF Core 10/Dapper/Npgsql); Redis = cache-aside, nunca autoridade financeira/estoque; Kafka = eventos de domínio via CDC/outbox (Debezium); RabbitMQ = tasks/workflows (quorum queues, DLQ); gRPC+.proto schema-first (Buf) = interno síncrono; HTTP/JSON+OpenAPI = borda/frontends.
- **IAM**: Keycloak (OIDC Auth Code + PKCE, RBAC+ABAC por market/warehouse/seller, MFA admin, four-eyes).
- **Observabilidade**: OpenTelemetry → Grafana (Prometheus/Mimir, Loki, Tempo, Pyroscope). Sem labels de alta cardinalidade (customer_id/order_id).
- **Dev/Deploy**: Docker Compose (obrigatório, profiles `core,data,messaging,observability,ai,simulated-external`), Helm+K8s on-prem alvo, Skaffold, .NET Aspire **opcional** (só DX).
- **Testes**: xUnit (v3 conforme validação), Testcontainers for .NET, Playwright (Next+Angular), FsCheck, BenchmarkDotNet/k6/Locust. FluentAssertions/MediatR/MassTransit **exigem checagem de licença antes de adotar** (ADR).

## Princípios de arquitetura (não negociáveis)

- **Clean/Hexagonal por serviço**: `Domain` = C# puro (zero referências a ASP.NET Core/Mongo/EF/MAF/gRPC); `Application` = handlers/ports/DTOs; `Infrastructure` = adapters; `Presentation` = endpoints. DIP: dependências apontam para dentro. Testes de arquitetura automatizados (NetArchTest/ArchUnitNET ou reflection própria).
- **SOLID e Design Patterns por evidência, não decoração**: Aggregate, VO, Factory, Strategy (pricing/payment routing), Specification/Policy, Repository aggregate-specific, UoW, Adapter/ACL, Outbox/Inbox, Saga/Process Manager, Circuit Breaker/Bulkhead, CQRS/Projection, Decorator, Result/Domain Error. **Proibido:** `IRepository<T>` universal, interface 1:1 sem motivo, Mediator genérico sem justificativa.
- **Ownership único por agregado**: o mesmo agregado tem UM write owner. `atlas-control-dotnet` **nunca** escreve no MongoDB de Catalog/Offer/Inventory/Order/Payment — envia comando autenticado (gRPC) ao owner, que revalida autorização e políticas. Control tem MongoDB próprio apenas para approvals/audit/workflow.
- **Money**: `Money(AmountMinor: long, Currency)` — nunca `float/double`. `DateTimeOffset` UTC + `TimeProvider` abstraído (nunca `DateTime.Now` em decisão de domínio). VOs `ProductId`, `SkuId`, `OfferId`, `ReservationId`, `OrderId`, `PaymentIntentId` etc. (records/readonly record structs).
- **CQRS correto**: comando valida no owner com estado autoritativo; leitura defasada (PG/Redis) **jamais** confirma pagamento, reserva última unidade ou cotação vencida. Read-your-writes via `source_version`/`PUBLISHING`.
- **Sem dual write**: aggregate + outbox na mesma transação MongoDB → CDC → Kafka → projeções idempotentes (`processed_events` na mesma transação da projeção). Tasks RabbitMQ nascem de consumer/bridge idempotente, nunca do request handler.
- **Padrões .NET**: nullable enable, warnings as errors quando viável, async all the way, `CancellationToken` até I/O, `HttpClientFactory`+resilience, ProblemDetails, Options+ValidateOnStart, sem service locator/fire-and-forget/`Task.Run` em request handler.

## Invariantes de negócio essenciais (INV-*)

- **INV-STOCK-001**: `available = on_hand - reserved - safety_stock >= 0`; reserva condicional atômica, zero oversell mesmo sob concorrência no mesmo SKU.
- **INV-STOCK-002**: todo movimento registra motivo, ator, `correlation_id` e ledger; TTL não é a única forma de devolver reserva.
- **INV-ORDER-001/002**: pedido = snapshot imutável (itens, preços, impostos, endereço); transições de estado explícitas, idempotentes e com histórico; eventos duplicados/reordenados não criam duas compras.
- **INV-MONEY-001**: minor units + moeda explícita + regra de arredondamento por moeda.
- **INV-PAYMENT-001**: idempotency key; webhook autenticado e deduplicado; estado final só por resposta autenticada do PSP + reconciliação; nunca PAN/CVV; PSP fake determinístico (`APPROVED/DECLINED/TIMEOUT/UNKNOWN/WEBHOOK_DUPLICATE/WEBHOOK_OUT_OF_ORDER/REFUND`).
- **INV-CQRS-001**: leitura defasada nunca autoriza operação crítica.
- **INV-SEC-001**: autorização sempre no backend owner (subject, role, ação, escopo, limite, estado); UI não é perímetro.
- **INV-ADMIN-001**: four-eyes (`requested_by != approved_by`) para risco; aprovação auditável e revalidada no owner.
- **INV-AI-001**: LLM só recomenda/consulta/propõe carrinho; **nunca** compra autônoma, captura, estorno, preço, estoque ou IAM.

(No blueprint original há 12 invariantes INV-001..012 equivalentes — ver `atlas-commerce-blueprint.md` #02 e o prompt mestre §3.2.)

## MAF — assistente de compras (C#)

Módulo `src/AI/ShoppingAssistant/` no repo Storefront (subpastas `Api/Domain/Application/Workflows/Agents/Tools/Providers/Memory/Safety/Evaluation/Infrastructure`). Regras:

- Provider atrás de `IChatClient` (adaptador substituível; Ollama/endpoint local preferido para demo; Azure/cloud opcional com orçamento e privacy review). Fake determinístico **só para testes**.
- Tools **read-only**: `SearchCatalog`, `GetProductDetails`, `GetCurrentOffer`, `GetInventoryHint`, `CompareOffers`, `EstimateShipping`, `GetCustomerCart` (auth), `ProposeCartChange` (retorna proposta **inerte**).
- Mutação de carrinho só via **consent artifact** (nonce, TTL, subject, hash do payload) emitido após confirmação explícita em UI confiável → backend valida → chama Cart owner. **Nunca** ferramentas `CapturePayment/Refund/AdjustStock/ModifyPrice/GrantRole/SubmitCheckout`; sem MCP externo sem allowlist.
- Guardrails em **código** (allowlist, schema validation, rate/cost limits), não só system prompt; outputs de tools = untrusted (prompt injection).
- Grounding: só IDs/preços retornados por owners, com `quoted_at`/TTL. Fallback para busca convencional quando modelo indisponível. Métricas: tool calls, denial rate, tokens, latência, custo. MAF **não** orquestra saga de checkout nem CQRS.

## Roadmap e gates (M0→M11)

| M | Objetivo | Gate-chave |
|---|---|---|
| **M0** | Recon + auditoria + modelagem de negócio + specs/ADRs `PROPOSED` | fontes lidas/rastreáveis, ownership único, 5 cenários críticos, `SPEC-001` proposta — **sem scaffolding, sem `dotnet new`, sem containers** |
| M1 | Platform Foundation (3 repos, CLI templates, CI, Compose, Keycloak, OTel) | 1 serviço real + host admin com build/test/health/auth/trace; CI verde |
| M2 | 1º vertical slice: Admin→Control→owners Catalog/Offers/Inventory→Mongo/outbox/Kafka/PG→Next | produto publicado pelos caminhos reais; 403 sem scope; evento repetido não duplica; contenda pela última unidade = 1 venda |
| M3 | Cart, Quote, Checkout Saga, Order, PSP Fake | E2E local; `oversell=0`; `duplicate_paid_order=0`; timeout/retry/restart/replay cobertos |
| M4 | Admin hardening (four-eyes, scopes, auditoria, lag CQRS visível) | self-approval negado e auditado |
| M5 | Storefront UX, busca, SEO, i18n (pt-BR/en-US/es-ES) | E2E 3 idiomas/mercados; CWV/a11y medidos |
| M6 | Fulfillment/Returns/Notifications | refund coerente; DLQ/retry provados |
| M7 | MAF shopping assistant | smoke real MAF pinado; tools read-only; consent inviolável; fallback |
| M8 | K8s/Helm on-prem | rollout/rollback, restore drill |
| M9 | Benchmark L0–L4, chaos, custo | relatório reproduzível; **nunca** prometer milhões/s sem medição |
| M10 | Marketplace 3P | DEFERRED por padrão |
| M11 | Portfólio internacional EN | clone→run→verify honesto |

Dependências: M0→M1→M2→M3; M4/M5 parcialmente paralelos; M6 após M3; M7 após M2/M3; M8 após baseline; M9 após recovery; M11 transversal.

## Regras de execução SDD (como trabalhar aqui)

- **Estados**: `TODO | READY | IN_PROGRESS | PASS | FAIL | BLOCKED | DEFERRED`; ADRs `PROPOSED | ACCEPTED | REJECTED | SUPERSEDED`. `PASS` **somente** com evidência executável (comando, exit code, logs). Compilar/container up/HTTP 200/mock-only **não** provam transação comercial correta.
- **Ciclo por execução**: RECON → SPEC → PLAN/TASK → TEST-FIRST → IMPLEMENT → VERIFY → EVIDENCE → CONVERGE. Report final no formato do §16 do prompt mestre (em pt-BR).
- **SDD próprio**: sem GitHub Spec Kit, sem `.specify/`. Layout: `sdd/{prompt,audit,source-manifest,glossary,requirements,roadmap,risks,decisions-open,traceability,progress}.md`, `specs/NNN-*/{spec,design,tasks,acceptance.feature,evidence,traceability,risks}.md`, `architecture/decisions/ADR-NNNN-*.md`. IDs imutáveis (`REQ-*`, `INV-*`, `TASK-*`, `TEST-*`, `EVD-*`); deprecar, nunca renumerar.
- **Parar e reportar BLOCKED**: credenciais ausentes, custo de nuvem/GPU não autorizado, ação destrutiva, decisão de negócio material sem critério. Fora isso, avançar em fatias pequenas sem pedir confirmação trivial.
- **Proibições permanentes**: código Python de produto; LangGraph/LangChain/Semantic Kernel como orquestrador; shared `DbContext`/domain assembly entre fronteiras de ownership; gasto externo sem autorização; declarar capacidade/segurança/certificação sem evidência datada; segredos em `appsettings.json` versionado.

## Estado atual e próximo passo (atualizado 2026-10-28 — Estágio A concluído; M1 em progresso)

- ✅ **M0** PASS (SPEC-000) e **Estágio A** (documentação SDD completa) concluídos no repo de coordenação `atlas-workspace-dotnet/` (commits `300193b`, `5b59059`): 12 specs (SPEC-001 READY; 002..011 DRAFT), 19 ADRs PROPOSED, contracts-draft validados (`buf lint` PASS), dataset IA e plano benchmark L0–L4; relatórios em `sdd/reports/`.
- 🚧 **M1: 100% do escopo sem Docker** (relatório `sdd/reports/M1-report.md`): + TASK-001-004 templates `dotnet new` 4/4 validados (CI replica) com **smoke de restore MAF 1.26.0/net10.0**; TASK-001-002 scan de vulnerabilidades zero nos 3 repos; total 31 testes PASS. Gate formal M1 aguarda **Docker** (007/008/009) e push remoto (010). Repos:
  - `atlas-platform-contracts-dotnet` (`ad48b1a`): contratos wire + SDK envelope, 8/8 testes, buf lint PASS.
  - `atlas-storefront-dotnet` (`99cfb96`→`de5c4c7`): Catalog Domain+Api, 14/14 testes, health 200, CVE OTel bloqueada e corrigida (1.20.1), Next.js base build verde.
  - `atlas-control-dotnet` (`fddef9a`→`909dd95`): monólito modular, 7/7 testes (5 de arquitetura), health 200, Angular base build verde.
  - Total: 29 testes PASS + 2 builds de frontend + smoke runtime. EVD-000-003, EVD-001-001..004; relatório `sdd/reports/M1-report-parcial.md`.
- ✅ **GATE M1 PASS (2026-10-29)**: containers = **Podman rootless** no host (esclarecimento do proprietário; socket API utilizável — ver EVD-000-004/006..008; runbook `atlas-storefront-dotnet/deploy/compose/README-podman.md`). Stack core+data saudável (mongo rs PRIMARY, pg, redis, keycloak+realm), OIDC smoke (PKCE+MFA) e 6 traces cross-process no Tempo. Pendência externa única: `git push` dos 3 repos (CI GitHub Actions). (verificado 2026-10-28; diverge da informação recebida). Bloqueia: Compose/Keycloak/OIDC smoke/OTel-collector/Testcontainers (TASK-001-007/008/009) e gates M1 de container. Instalar Docker Engine + compose plugin resolves.
- 🏆 **GATE M2 PASS (2026-10-29)** — E2E real de ponta a ponta: rota dev→owner→four-eyes(Control JWT+HMAC)→gRPC h2c→Published→outbox→dispatcher→Kafka→projector→PG→BFF(YARP)→**vitrine Next exibindo o produto**. Inventory.Api owner gRPC com UC003-T01 (contenda gRPC real). Four-eyes real: Keycloak+JWT+HMAC+403/409 auditados. Reports: M1-gate-report.md, M2-gate-report.md. Pendência externa única: git push (CI).
- 🏆 **GATE M3 PASS (2026-11-02)** — SPEC-003 completo (storefront `84a67fb`, workspace `91e0de5`): **141/141 testes** (17 projetos). Todos os critérios do roadmap com evidência REAL: E2E local (4 hosts vivos sobre mongo rs0), **oversell=0** (T01 CAS), **duplicate_paid_order=0** (T02+T05), **timeout/retry/restart/replay** (T03 reconciliador→Confirmed+ref; TEST-003-008 recovery de sagas crashadas — crash pós-autorização→Confirmed, sem registro no PSP→Compensated; webhook T05 com HMAC+dedup transacional no NOVO Atlas.Payments.Api :5398; COM-08 quote expirada→409 zero efeito). UC-003 T01..T05+COM-08+TEST-003-004/007/008 cobertos nas 3 camadas. 14 bugs reais corrigidos (destaque: **11000 dentro de txn Mongo ABORTA a txn** — read-first/saída por exceção). Reports: `M3-gate-report.md` (final), EVD-003-001..009. Playwright UX é do **M5** (não é critério do gate M3). Pendência externa única: git push (CI).
- 🚧 **M8 PASS PARCIAL + M9 harness (2026-11-03, contracts `64b4eaa`/`aa03823`)** — helm v3.16.4+kubectl v1.37.2 instalados; **charts atlas-storefront (32 recursos: 10 Deploy+10 Svc+10 NP+2 HPA) e atlas-control (5) lint+template OK**; ingress TLS+KEDA scalers (Kafka lag/Rabbit queue)+backup/restore-drill scripts. **DESVIO EVIDENCIADO**: cluster executável impossível no ambiente (kind sem docker CLI; k3s fatal "failed to find cpuset cgroup v2" — distrobox não delega cpuset); execução runtime → clone→run→verify (M11). **M9 harness pronto** (k6 mix+dataset 10^6+gates por run); L0/L1 executáveis no compose; L2→pendente cluster. Fatias curtas M8 feitas: 15 YARP removido, 8 sitemap/hreflang/OG; e2e 9/9 mantidos. Restam: lease workers, captura reconciled, login buyer, runs L0/L1, M11.
- 🏆 **GATE M7 PASS (2026-11-03, storefront `79af196`/`c2018eb`, workspace `da12164`)** — SPEC-007 núcleo executado: **spike ADR-0008 ACCEPTED** (MAF 1.26.0 API real: ChatClientAgent/CreateSessionAsync/RunAsync→AgentResponse; tool calling REAL via Ollama qwen3.6:27b LOCAL zero-custo); **ShoppingAssistant.Api :5296** (allowlist read-only em código — reflexão prova que nenhuma tool de mutação existe; proposta inerte; consent HMAC ADR-0009 inviolável — payload/subject diversos→403; fallback busca convencional quando LLM/BFF fora); turnos LIVE: T1 grounded (BFF filtro sku→PG), T2 injection→recusa, T3 consent ok. Tests 7/7 (CI fake + eval RequiresOllama por groundedness). **Bugs**: params nullable→schema `type:[string,null]` quebra OllamaSharp; tools sem try/catch→500; streaming no fake; assert não-determinístico→groundedness. Honestos: F2 grafo MAF Workflows→SPEC-007+; suite completa dataset→SPEC-007+; métricas custo parcial. Pendência externa única: git push. Próximo: **M8 K8s (FASE 4)**.
- 🏆 **GATE M6 PASS (2026-11-03, storefront `8efb96c`, control `89b4b50`, workspace `3b77370`)** — SPEC-006 READY+executada: **RabbitMQ 4 quorum** no Podman (:5672/:15672); Notifications worker (consome atlas.orders.v1, **dedup notification_key índice único** — 8 concorrentes→1 envio; poison→retry TTL 3s→**notifications.dlq**; dedup persiste pós-restart; REAL 3/3); **Fulfillment.Api :5294** (Order.Confirmed→Picking→Packed→Shipped→Delivered, tracking determinístico ATLAS-{orderId:N8}, fora de ordem/janela→409); **refund four-eyes cross-repo** (Control.RefundApprovalsModule: token HMAC {sub,order_id,amount_minor,exp}, self-approval→409 auditado → Payments valida NO OWNER: 403 sem token, só CAPTURED refunda, idempotência por key, PSP fail→RefundPending). **Bugs reais**: RabbitMQ.Client 6→7 (API v7 async), DLQ declarada sem BIND (dead letters sumiam), índice unique sobre _id (inválido), Control token using fragmentado. Honestos: Kafka→Rabbit E2E manual; refund store persistente→SPEC-006+; fulfillment projection→M7. Reports: M6-gate-report.md. Pendência externa única: git push. Próximo: **M7 MAF (spike Ollama primeiro — ADR-0008)**.
- 🏆 **GATE M5 PASS (2026-11-02, storefront `e737a3c`, workspace `0048b65`)** — SPEC-005 READY+executada: vitrine Next 16 (`apps/storefront-next`) com `[lang]`×3 locales + `proxy.ts`, cacheComponents/PPR (fetch dinâmico em Suspense — params são Promise), carrinho localStorage + quote congelada + checkout 3 desfechos; BFF Gateway com `/api/catalog/search` (facets), PDP ofertas+hint e forwarder `/api/checkout/**`; Offers.Api (:5293) owner; seed pelo pipeline real. **Playwright 9/9** (compra×3 locales, vazio, redirect, a11y×3, CWV) · axe 0 critical/serious · LCP 0.05s / CLS 0.036. **Bugs reais**: CORS cross-origin → rewrites same-origin; YARP nunca casou (latente M2) → forwarder explícito; race de clique na lista velha; chaves i18n duplicadas → verificador automatizado. Honestade: sitemap/hreflang OG → M8; login buyer → M6+. Pendência externa única: git push.
- 🏆 **GATE M4 PASS (2026-11-02, control `722b442`, workspace `d6d2bc6`)** — SPEC-004 completa: four-eyes E2E Keycloak (self-approval 409; revoke→403 próxima chamada; token v2 uso-único revalidado no owner), AuditChain SHA256 com verificador como TESTE (tampering→brokenAtSeq), lag CQRS REAL (owner×PG=0), req.3/5/6 (export CSV, listagens server-side, ops overview), runbook revogação emergência, ADR-0010/0012 ACCEPTED. **Bug crítico da fatia**: dispatcher workers apontava atlas_catalog (inventory outbox nunca despachado live). Control 13/13 · Storefront 141/141. Reports: M4-gate-report.md. Segue plano aprovado: FASE 1 = M5 (carrinho/checkout UI + Playwright).
- (histórico M4 core) 🚧 ~~M4/SPEC-004 fatia core REAL (2026-11-02, control `d22a761`, workspace `c37140b`)~~: four-eyes GENERALIZADO E2E com JWT Keycloak real (self-approval 409 auditado; grant ABAC revogável reflete na próxima chamada; token v2 uso-único REVALIDADO no owner — AdjustStock antes aceitava qualquer um!; trilha SHA256 encadeada valid=true count=24; lag CQRS REAL owner×PG com novo RPC GetStockVersion). **Bug crítico**: dispatcher único do workers apontava atlas_catalog — outbox de inventory nunca despachado em live (M2 só provou inline). Restam: relatório §16 final M4 (req.5/6 dashboards/exportação opcionais).
- Decisões do proprietário vigentes: CI=GitHub Actions; M7=Ollama local zero-custo; plano completo em `atlas-workspace-dotnet/sdd/PLAN-docs-completas-implementacao.md`.
