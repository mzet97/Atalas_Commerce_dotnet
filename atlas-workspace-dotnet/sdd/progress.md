# SDD — Progresso (diário de execução)

## 2026-10-27 — M0 / SPEC-000 (execução completa da documentação)

**Decisões do dia:**
- Coordenação SDD em `atlas-workspace-dotnet/` (repo auxiliar git, branch `main`), irmão do handoff — segue árvore-alvo §5.1 do mestre; `.gitignore` exclui os 3 repos futuros.
- Escopo desta execução: M0 completo + SPEC-001 rascunho (perguntas ao proprietário sem resposta; opções recomendadas aplicadas e registradas no plano aprovado).
- D-1 resolvido: taxonomia de invariantes do mestre é canônica (15 INV-*), blueprint vira alias (tabela em `requirements.md` §1).
- 15 ADRs criadas como `PROPOSED` (cobrem §19 e absorvem ADR-001..006 do blueprint, com numeração mapeada).
- Diagramas: 15 cópias de trabalho (2 com correção de rótulos LEGACY: `00`, `09`; notas `.NET` em `06/07`; `10` já era stack-agnostic) + 12 novos (12–21, C4-01/02) — diffs em `architecture/diagrams/README.md`; **originais do handoff intocados** (re-hash na validação final).
- Baseline de ambiente: .NET SDK 10.0.403 ✅ (coincide com o snapshot do contrato), node 24/pnpm 11 ✅, git 2.55 ✅; **AUSENTES**: docker/compose, kubectl, helm, skaffold, buf, aspire (registrado em audit/decisions-open; bloqueia M1, não M0).

**Concluído:** TASK-000-001..014 (ver `specs/000-business-discovery/tasks.md`).
**Evidências:** EVD-000-001 (integridade 32/32), EVD-000-002 (ambiente).
**Bloqueios:** nenhum para M0. Pendências para M1: instalação docker/buf.
**Próxima tarefa READY:** TASK-001-001 (instalar docker+compose e buf) → TASK-001-002 (matriz de dependências revalidada).

**Honestidade:** nenhum teste de software executado (não há código por design do M0); Gherkin = contrato futuro; PERFORMANCE: NOT_MEASURED.

## 2026-10-27 — Plano de execução completa (doc total + implementação)

- Proprietário solicitou plano para: (A) criar TODA a documentação SDD e (B) implementar em código.
- Plano registrado em `sdd/PLAN-docs-completas-implementacao.md` (Estágio A: SPEC-001→READY + SPEC-002..011 DRAFT completas + ADRs complementares + contratos wire draft + dataset IA + planos benchmark + consolidação; Estágio B: B0 ambiente → M1..M9/M11 com ciclo SDD e gates).
- Regra acordada: docs antecipadas nascem DRAFT e são refinadas para READY ao entrar no milestone (evita especulação fossilizada); PASS só com EVD.
- Decisões pendentes do proprietário (defaults definidos no plano §Decisões): instalação docker/buf; CI; modelo IA M7; checkpoint pós-Estágio A.

## 2026-10-28 — Decisões do proprietário + início Estágio A (execução do plano)

**Decisões do proprietário (precedência máxima):**
1. CI/CD: **GitHub Actions** nos 3 repos (ADR a registrar na SPEC-001 design).
2. M7: **Ollama local, zero custo** (cloud só com aprovação explícita futura — ADR-0008 confirmada nessa parte).
3. Estágio A aprovado ("Ok o estado A") — execução total da documentação antes do código.
4. Docker/Compose: proprietário informou que estão no host; **verificação real contradiz** (`docker` ausente no PATH, /usr/bin, /usr/local/bin, /opt, /snap — auditado em 2026-10-28). Registrado como divergência; gates dependentes de Docker (M1 compose/Keycloak/Testcontainers) permanecem BLOCKED até instalação real. **Buf 1.75.0 instalado com sucesso** em ~/.local/bin (EVD-000-003).

## 2026-10-28 — Estágio A concluído (documentação SDD total)

- **A1:** SPEC-001 → READY (9 arquivos; GitHub Actions incorporado ao design).
- **A2:** SPEC-002..011 criadas completas (SPEC-010 esqueleto DEFERRED por design). Total: 12 specs.
- **A3:** ADR-0016..0019 (TTL quote/reserva, contrato PSP fake, taxonomia Kafka/eventos, testes de arquitetura) — 19 ADRs no total.
- **A4:** `contracts-draft/` com 7 artefatos; **buf lint PASS (1.75.0)** após correções reais (estrutura de dirs, sufixo Service, request/response únicos); YAML/JSON validados (bug de dois-pontos no gateway YAML corrigido).
- **A5/A6:** dataset de avaliação IA (`specs/007/eval-dataset.md`) e plano benchmark L0–L4 (`specs/009/benchmark-plan.md`).
- **A7:** checagem de consistência automatizada → corrigidas 2 lacunas (design.md da SPEC-009; citação explícita INV-ORDER-002). Resultado final: 15/15 invariantes citadas, 12/12 specs completas, 19 ADRs com alternativas, 102 TASKs referenciadas.
- Relatório: `sdd/reports/EstagioA-report.md`. Commits a seguir.


## 2026-10-28 — Estágio B/M1 (parcial): 3 repos com build/test reais

- 3 repositórios criados e commitados: contracts `ad48b1a` (8/8 testes, buf lint PASS), storefront `99cfb96` (14/14, health 200, CVE OTel 1.12.0→1.20.1 bloqueada por NU1902), control `fddef9a` (7/7, 5 testes de arquitetura reflection, health 200).
- Smoke runtime real dos dois hosts via curl (200). Total: **29 testes PASS**.
- BLOCKED persistente: **Docker ausente** (Compose/OIDC/OTel-collector/Testcontainers). Evidências: EVD-000-003, EVD-001-001..003. Relatório: reports/M1-report-parcial.md.
- Próximas sem Docker: TASK-001-011 (frontends base) → TASK-001-004 (templates) → 002 (matriz completa).

## 2026-10-28 — M1: TASK-001-011 concluída (frontends base)

- Next.js (storefront) `pnpm build` exit 0; Angular (control) `npm run build` exit 0 (bundle 1.76s). EVD-001-004.
- Restam no M1: TASK-001-004 (templates dotnet new), TASK-001-002 (matriz completa), TASK-001-007/008/009 (**BLOCKED: Docker**), TASK-001-010 (push remoto).

## 2026-10-28 — M1: TASK-001-004/002/012 concluídas (ciclo sem Docker)

- **TASK-001-004 PASS**: 4 templates criados e testados desde pasta vazia (microservice builda+testa 2/2; module/worker/maf-agent buildam; CI replica). Correções reais: NoWarn CA1707 encadeado no template; const→static readonly.
- **Bônus**: smoke de restore do **MAF 1.26.0** em net10.0 via template (Microsoft.Extensions.AI 10.10.x no grafo) — primeira evidência da ADR-0008 (API smoke segue no M7).
- **TASK-001-002 PASS**: `dotnet list package --vulnerable --include-transitive` = zero nos 3 repos; matrizes atualizadas (commits 9611302/472ad6b/b7e1062).
- **TASK-001-012**: relatório final `sdd/reports/M1-report.md`. Estado M1: tudo que não exige Docker está PASS (31 testes + builds); gate formal aberto por 007/008/009 (Docker ausente — ação do proprietário) e push remoto p/ CI.
- Docker re-verificado em 2026-10-28: **continua ausente**.


## 2026-10-29 — GATE M1 FECHADO (Podman rootless)

- Proprietário esclareceu: containers = **Podman** (não Docker). Socket rootless verificado e API operacional (EVD-000-004). Ambiente real: distrobox dev-fedora com DOCKER_HOST apontando ao socket.
- TASK-001-007 PASS (EVD-001-006): pod atlas-pod (mongo7 rs PRIMARY c/ session-write, pg17 accepting, redis PONG, keycloak 26.0.7 + realm via Admin REST 201) + LGTM standalone.
- TASK-001-008 PASS (EVD-001-007): PKCE S256 end-to-end, client_credentials, JWKS kid, MFA required-action CONFIGURE_TOTP.
- TASK-001-009 PASS (EVD-001-008): OTLP http/protobuf → LGTM; **6 traces cross-process** Control→Catalog (TraceQL && dos dois service names; 3 spans/trace). Commits 0a6377f/a4a9572.
- **Gate M1: PASS** (reports/M1-gate-report.md) — única pendência externa: git push p/ CI remoto (TASK-001-010 parcial).
- SPEC-002: domínio (Catalog+SKUs/eventos, Offers/Money, Inventory/CAS/Reservation) com 50 testes PASS (commit b0a5367, EVD-002-001) — fatia executada enquanto Docker estava bloqueado; próximo: Mongo+outbox no replica set agora disponível.

## 2026-10-29 — SPEC-002: Inventory no MongoDB real (EVD-002-002)

- Application (handler idempotente + port) + Infrastructure (MongoInventoryStore: update condicional $expr ATÔMICO, índice único de idempotency key, ledger + outbox NA MESMA transação).
- **5/5 testes de integração reais no replica set** (commit cbba8c9; 55/55 no repo): UC003-T01 com CAS real (10 concorrentes → 1 vencedor, oversell=0), duplicate-key race, all-or-nothing multi-linha, confirm/release com ledger+eventos.
- Diagnósticos reais registrados: Id→_id no driver 3.x; Guid representation; overloads de sessão; probe mongosh.
- Restantes do M2: gRPC owners, projeções PG (Kafka/bridge), Gateway/BFF, vitrine Next, four-eyes no Control.

## 2026-10-29 — SPEC-002: pipeline de eventos ponta a ponta REAL (EVD-002-003)

- Kafka single-node KRaft no Podman (apache/kafka 3.9.0; advertised/bind pattern correto).
- OutboxDispatcher (Mongo→Kafka, ack+flag Published) e CatalogProjector (Kafka→PG, processed_events dedup transacional + stale guard por source_version).
- **E2E real PASS**: owner→outbox→Kafka→projeção; reentrega dedup (INV-EVT-001); stale ignorado. Commit 9b51374. 64 testes no repo; CVE zero.
- Fatos: binds kafka `://:porta`; AdminClient cria tópico no setup; dispatcher usa topic canônico do doc.
- Restam do M2: gRPC owners, available_offer_view/inventory_admin_view, Gateway/BFF, vitrine Next, four-eyes/403 Control.

## 2026-10-29 — SPEC-002: gRPC owner + views restantes (EVD-002-004)

- Contracts: SDK Atlas.Contracts.Grpc (Grpc.Tools sobre os protos canônicos; commit 00a1f1f, buf PASS) + proto aditivo SubmitForApproval (UC-005).
- Catalog.Api: CatalogOwnerGrpcService completo (UpsertDraft/SubmitForApproval/PublishProduct/GetProduct) — 4 testes gRPC in-memory (commit 0923ed6): fluxo UC-005 por contrato, publish sem aprovação/token negado, conflito de version, evento no outbox. Referência cross-repo ao SDK documentada (vira pacote NuGet no release; caminho 5 níveis da Api).
- Views M2 completas (commit pendente): OffersProjector→available_offer_view e InventoryProjector→inventory_admin_view (deltas pós-dedup) — E2E real com Kafka+PG; reentrega sem efeito. **66 testes no storefront**.
- Restam do M2: four-eyes/403 auditado no Control + Gateway/BFF YARP + vitrine Next lendo available_offer_view.

## 2026-10-29 — SPEC-002: four-eyes ponta a ponta REAL (EVD-002-005)

- Inventory.Api owner gRPC: UC003-T01 via gRPC real (10 concorrentes→1; commit b08c3d5).
- Owner Catalog valida approval_token HMAC (assina+exp+payload); bug real corrigido ($"{byte[]}" literal).
- Control: Approval+Store Mongo próprio+Issuer HMAC+JWT Keycloak real (MapInboundClaims=false)+403/409 auditados (middleware ANTES do UseAuthorization — Forbid short-circuit). Commit 36a3c3d.
- Testes: Control 8 (5 arch refinada+3 four-eyes c/ Keycloak real), Catalog 6, storefront 74 total.
- Restam p/ GATE M2: smoke cross-process Control→Catalog real; Gateway/BFF YARP + vitrine Next; relatório §16.

## 2026-10-29 — GATE M2 PASS (EVD-002-006)

- **E2E REAL de ponta a ponta**: rota dev→owner→four-eyes(Control JWT+HMAC)→gRPC h2c→Published→outbox→dispatcher→Kafka→projector→PG→BFF(Gateway YARP)→**vitrine Next exibindo "Produto Smoke M2"**.
- Novos: Workers.Projections executável (dispatcher+projectors loop + schema auto + tópicos), Gateway/BFF (YARP + Npgsql view read), vitrine home (Suspense), título real no evento, Kestrel h2c 5290, fix NU1109 CPM Hosting 10.0.14, fix [FromBody], middleware 403 pré-autorização, MapInboundClaims=false.
- Gate M2: **PASS** (reports/M2-gate-report.md — 4 critérios item a item). 74+8 testes + E2Es. Pendência externa: git push p/ CI.
- Próximo: SPEC-003 (Cart/Quote/Checkout saga/Order/PSP fake).

## 2026-10-29 — SPEC-003 parcial: domínios + PSP fake + saga orquestrada (EVD-003-001)

- Orders.Domain (9 testes), Payments.Domain (9), CheckoutSaga (7), SimulatedPSP (4), CheckoutOrchestrator (6) = 35 novos; 115 no storefront.
- Bugs pegos pelos testes: compensação ausente em Submitted; intent-criado≠autorizado; UNKNOWN sem transição do Order; fake de estoque stateless.
- Restam p/ GATE M3: reconciliador, webhook end-to-end, stores Mongo reais, Quote/Cart, E2E Playwright.

## 2026-10-29 — SPEC-003: checkout REAL com owners gRPC + PSP HTTP (EVD-003-002)

- Checkout.Api host real: SubmitCheckout idempotente (200/202/409), ports gRPC h2c (Inventory 5292) + HTTP PSP.
- E2E REAL: T03 UNKNOWN → Pending + reserva RETIDA (correto — sem decisão não compensa); T02 mesma key → mesmo resultado; fluxo feliz → Confirmed. reserved_final=2 consistente.
- Fixes reais: warehouse Guid.Empty mismatch; retriable txn 11000 (inserts idempotentes); NU1109 CPM; cache incremental corrompido escondendo erros (clean+rebuild).
- Restam p/ gate M3: reconciliador, webhook e2e, Quote/Cart, E2E Playwright.

## 2026-10-29 — SPEC-003: reconciliador (EVD-003-003)

- CheckoutReconciler (Application): consulta PSP por orderId, resolve pago/not-pago/indeterminado; hosted service no Checkout.Api (5s); adapters de reconciliação com guard de estado (Unknown→Authorized, ReconciliationRequired→Confirmed/Cancelled).
- Transições MarkReconciliationPaid/NotPaid no aggregate; ISagaStore.FindByStateAsync.
- 9/9 testes (3 reconciliador + 6 saga). Fix real: reconciliador consultava pela checkout key, mas a autorização foi registrada sob o orderId.
- E2E T03 completo: pendente de re-execução (instabilidade de ambiente — hosts background morrem entre tool calls). Restam p/ gate M3: E2E T03 completo, Quote/Cart, Playwright.

## 2026-10-29 — SPEC-003: reconciliação REAL automática (EVD-003-004)

- T03 E2E REAL FECHADO: checkout UNKNOWN → PENDING + reserva retida → reconciliador consulta PSP → AUTHORIZED → order Confirmed + intent Authorized + saga Confirmed + reserva mantida. Tudo com owners reais (gRPC, Mongo replica set, PSP HTTP).
- Fixes reais: reconciliador consultava por checkout key mas a autorização estava sob orderId; order não era marcado ReconciliationRequired; Confirm com fallback duplo de estado.
- Fix de processo: pkill -9 dotnet corrompe cache incremental MSBuild — SEMPRE rm bin/obj após pkill em projetos editados.
- Restam p/ gate M3: Quote (TTL), Cart, E2E Playwright, docs.

## 2026-10-29 — SPEC-003: Cart + Quote (commit b178a22)

- Cart.Domain: add/remove/merge guest (INV-03), limite de linhas, validação de quantidade — 7 testes.
- PriceQuote: TTL INV-QUOTE-001, diferença na re-cotação COM-08, moedas incompatíveis — 6 testes.
- 18 testes de domínio Checkout.Domain; 121 total no storefront.
- Relatório M3 parcial: reports/M3-report-parcial.md (121 testes, status honesto das fatias restantes).

## 2026-10-29 — SPEC-003: Cart e PriceQuote nos stores Mongo reais (EVD-003-005)

- MongoCheckoutStore: Cart upsert por owner_id; Quote persistida com TTL. 2/2 integração real (commit 07e20c7).
- 119 testes verificados. Restam p/ gate M3: SubmitCheckout valida Quote vigente do store (COM-08), reconciliador e2e estável, webhook PSP, Playwright, relatório §16.

## 2026-10-29 — SPEC-003: IQuoteStore no orchestrator + rotas /quotes no Checkout.Api

- SubmitCheckoutCommand recebe QuoteId e valida Quote vigente no store (COM-08/INV-QUOTE-001): null → rejeita, expirada → rejeita, total diverge → rejeita.
- Rotas POST /quotes e GET /quotes/{id} no Checkout.Api (rota dev/ops para o smoke).
- Checkout.Api integrado ao MongoCheckoutStore real (mesma réplica, db própria).
- E2E M3E: quote emitida → checkout Confirmed → mesma key mesmo resultado. COM-08 expirada→verificar no próximo ciclo.

## 2026-10-29 — SPEC-003: consolidação M3 parcial (M3-report-parcial.md)

- M3-report-parcial atualizado com: 92 testes de domínio+application + integração real + E2E T03/reconciliador.
- 7 bugs reais de engenharia documentados (reconciliador key, compensação, intent≠autorizado, etc).
- Restam p/ gate M3: reconciliador e2e estável, COM-08 e2e estável, webhook, Playwright, stores no host.

## 2026-10-29 — SPEC-003: M3 gate report parcial consolidado

- M3-gate-report-parcial.md: 10 critérios do gate M3 avaliados item a item, 7 bugs reais documentados, ~137 testes.
- Restam p/ gate M3: reconciliador e2e estável, COM-08 e2e estável, webhook e2e, Playwright, rel §16.

## 2026-10-29 — SPEC-003: M3 gate report parcial consolidado (final do ciclo)

- M3-gate-report-parcial.md atualizado: 10 critérios do gate avaliados item a item, 7 bugs reais documentados, ~137 testes verdes.
- COM-08 e2e: gap real (script usa ttlMinutes, rota agora valida ttlSeconds) — script precisa de re-run com ttlSeconds.
- Restam p/ gate M3: reconciliador e2e estável, COM-08 e2e estável, webhook e2e, Playwright, rel §16.

## 2026-11-02 — Ciclo final do M3 (gate PASS)

- T03 reconciliador E2E estável: 2 runs + smoke no HEAD — UNKNOWN→202→Confirmed+ref (EVD-003-006).
  Bug 8 (reidratação infiel) e 9 (ProviderReference) corrigidos com regressão de round-trip.
- COM-08 e2e: TTL 1s→valid:false→409→zero efeito (EVD-003-007). Bug 11 (fire-and-forget no /quotes).
- T05 webhook e2e: NOVO Atlas.Payments.Api (:5398) — HMAC constant-time + dedup transacional +
  CAS + outbox em 1 txn; convergência webhook×reconciliador idempotente (EVD-003-008). Bug 12
  (11000 no dedup — padrão do bug 10) pego pelo próprio teste no 1º build.
- TEST-003-008 recovery: RecoverStuckAsync (crash pós-autorização → Confirmed; sem registro →
  Compensated) + branch "não pago → compensa" e2e via TIMEOUT→404 (EVD-003-009).
- TEST-003-007: DECLINED agora leva order a PaymentFailed (bug 13). TEST-003-004: out-of-order
  409 sem efeito/sem dedup (bug 14). Bug 10 (read-first) resolveu contenda antiga (2 falhas→5/5 em 1s).
- Infra: pg recriado conforme compose canônico (28P01 resolvido); pg/kafka conformes; hosts como
  bg tasks persistentes do harness (fim da instabilidade de E2E).
- Final: **141/141 testes (17 projetos)** — gate **M3 PASS** (M3-gate-report.md).
  Pendência externa única: git push (CI GitHub Actions).

## 2026-11-02 (2) — FASE 0 do plano completo: GATE M4 PASS

- Chain verifier como teste (tampering→brokenAtSeq), req.3/5/6 fechadas (export CSV,
  listagens server-side, ops overview), runbook de revogação, ADR-0010/0012 ACCEPTED.
- Control 13/13 · Contracts 8/8 · Storefront 141/141. EVD-004-001/002 + M4-gate-report.md.
- Lição de processo: CA1305 warning-as-error falhava builds silenciosamente e o binário
  velho continuava servindo — SEMPRE checar timestamp do DLL após rebuild.
- Próximo: FASE 1 — M5/SPEC-005 (spec DRAFT→READY; carrinho/checkout UI + Playwright).
