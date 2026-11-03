# PLANO FASE FINAL — Atlas Commerce .NET 10 + MAF (M8 → M9 → M11 + consolidação)

> **Escopo:** tudo que falta do roadmap e do SDD, numa única sequência executável.
> **Estado de partida (2026-11-03):** GATES M0–M7 **PASS** · storefront 154 testes (18 projetos) ·
> control 13 · contracts 8+buf · vitrine Next 16 + Playwright 9/9 · assistente MAF+Ollama live ·
> RabbitMQ/Kafka/Mongo/PG/Keycloak no Podman · **pendência externa única: git push → CI**.
> **Regras permanentes:** SDD evidence-first (PASS só com EVD real; relatório §16 por gate);
> SPEC→READY antes de executar; commits por fatia; proibições do contrato (sem Python/SK/LangGraph);
> honestidade — nada declarado sem medição; M10 (marketplace) permanece DEFERRED.

---

## FASE E (externa, proprietário — roda em paralelo a tudo)

| Item | Ação | Fecha |
|---|---|---|
| Git push dos 3 repos | `git remote add origin …` + push + habilitar Actions (workflows já nos templates) | "CI verde" formal do gate M1; CI passa a rodar em cada fatia seguinte |
| GitHub secrets | `ATLAS_*` se necessário (repos públicos: nada sensível) | CI verde |
| Hardware do M9 | Confirmar o laptop/servidor alvo do L1/L2 e deixá-lo livre para runs longos | Gates de benchmark com fingerprint |

Se o push atrasar, as fases seguem com CI local (`dotnet test` + `pnpm test` + `playwright`),
e o critério "CI verde" fica EVIDENCIADO como pendência externa aberta.

---

## FASE 4 — M8 / SPEC-008: Kubernetes on-prem operacional (2–3 sessões)

**Pré-requisito (início da fase):** instalar `kubectl`, `helm`, `skaffold`; cluster **kind**
local como não-prod por padrão (cluster on-prem real se o proprietário disponibilizar — decisão
registrada no gate report).

**Fatia 8.1 — Charts por repo (1ª sessão)**
- `atlas-platform-contracts-dotnet/infrastructure/charts/` → chart base `atlas-service`
  (deployment, service, probes /health, resources, securityContext non-root, PDB) consumido
  como **library chart** pelos outros repos.
- Charts: `storefront` (gateway, catalog, offers, inventory, checkout, payments, fulfillment,
  payments-api, assistant + next web + workers projections/notifications), `control` (host +
  Angular build), `data` (StatefulSets mongo rs0/pg/redis/kafka/rabbit OU apontando para os
  containers existentes via values `external:` — decisão por ADR curta no gate report).
- `helm lint` + `helm template` no CI local; values por ambiente (dev/kind).

**Fatia 8.2 — Deploy no kind + políticas (2ª sessão)**
- Namespace `atlas` + NetworkPolicy deny-by-default com exceções explícitas (owner↔gateway,
  workers↔data); secrets via sealed-secrets (zero segredo em Git); TLS ingress (ingress-nginx
  self-signed dev).
- **Assistente no cluster:** container Ollama sidecar ou node `external:` — honesto no report
  (modelo 27b não cabe em kind sem GPU; usar `qwen3.6:27b` no host via `external:` e documentar).
- HPA (CPU 70%) no gateway/catalog/checkout; **KEDA** escalando workers por lag do Kafka
  (consumer-lag scaler). Carga sintética com `hey`/k6 leve para ver réplicas subirem.

**Fatia 8.3 — Operação: rollout/rollback + backup/restore (3ª sessão)**
- `helm upgrade` rolling update gravado (log + vídeo opcional); rollback `helm rollback` com
  smoke de checkout pós-rollback.
- Backup: pg_dump cronjob + snapshot do volume mongo; **restore drill**: restaurar em
  namespace `restore`, rodar smoke de checkout contra o ambiente restaurado, pedido
  verificável → EVD-008-002.
- Skaffold dev profile (hot reload no kind).

**Gate M8 (§12):** demo implantada em cluster não-prod; réplicas escalando sob carga sintética;
rolling+rollback executados; restore drill com pedido verificável; observabilidade do cluster
ativa (kube-prometheus-stack leve OU LGTM existente apontado). Relatório §16 + EVD-008-001/002/003.

**Deferidos desta fase (honestos):** multi-região, autoscaling de dados, GitOps Argo (opcional
por ADR — decidir no report), service mesh.

---

## FASE 5 — M9 / SPEC-009: Benchmark honesto L0–L2 + SLOs congelados (2–3 sessões)

**Pré-requisito:** gates congelados ANTES de cada run (arquivo `results/<run_id>/gates.json`
commitado); hardware fingerprint; carga aberta k6; mix 80/12/5/2/0.7/0.3; dataset 10^6 SKUs
gerado e versionado (seed pela pipeline real: drafts+offers+positions em lote).

**Fatia 9.1 — Harness (1ª sessão)**
- `bench/` no contracts repo: cenários k6 (browse/search/PDP/cart/checkout/AI) + gerador de
  dataset (idempotente, seed versionado) + script `run.sh` que captura percentis, erros,
  recursos (docker stats→containerd), projection lag (`/modules/ops/overview` + `cqrs-lag`).
- Invariantes testadas DURANTE o run: oversell=0 e duplicate_paid_order=0 (queries nos stores).

**Fatia 9.2 — Runs (2ª sessão; longos em background)**
- **L0** (50 RPS / 1 checkout·s, 10 min): prova de correção end-to-end na vitrine real.
- **L1** (1.000 RPS / 20 checkouts·s, 30 min + soak 2h): baseline p50/p95/p99 por rota.
- **L2** (10.000 RPS / 200 checkouts·s, 1h + spike 10×): no kind com HPA; capturar
  escalonamento, backpressure e lag de projeção; **hot-SKU contention run** (contenda na
  última unidade sob carga) + comparativo réplicas×caches com IC 95%.
- **L3** conforme ambiente (soak 8h + chaos leve: kill de pods, restart de Kafka) — opcional.
- **L4** NUNCA alegado (modelo teórico já escrito no SPEC-009).

**Fatia 9.3 — Relatório + SLOs (fecho)**
- `results/<run_id>/` com CSV/JSON/dashboards + relatório honesto (o que NÃO atingiu).
- **Congelar SLOs por ADR** (fecha OPEN-05) apenas com base no que foi MEDIDO.
- ADR-0011 (Dapper vs EF por rota) decidida com os números do L1/L2.

**Gate M9:** relatório reproduzível (ambiente/carga/percentis/erros/limitações). Sem promessa
de milhões/s. Relatório §16 + EVD-009-*.

---

## FASE 6 — M11 / SPEC-011: Portfólio internacional EN (1–2 sessões)

- **ADR essays EN** (5): monolith-vs-microservices (a jornada do monólito modular),
  polyglot CQRS (Mongo/PG/Redis/Kafka/Rabbit), at-least-once + idempotência (outbox/DLQ/dedup),
  AI consent boundary (INV-AI-001 com consent HMAC), saga-vs-2PC (T03 recovery como caso).
- **READMEs quickstart EN** nos 3 repos (clone→compose→seed→demo em ≤ 10 comandos).
- **Matriz projetado×implementado×medido** com links EVD (a matriz mais honesta do projeto —
  inclui os ~30 bugs reais corrigidos como postmortems curtos).
- **Demo scripts + vídeos** (Playwright trace): checkout com falha controlada (DECLINED→
  compensação), admin four-eyes→owner→projeção, assistente com consent.
- **Seção obrigatória "o que NÃO está implementado/medido"** (lista abaixo, completa).
- **Gate M11:** clone→run→verify por avaliador externo (proprietário ou terceiro) sem ajuda.

---

## FASE 7 — Consolidação de dívidas (DECISÃO explícita por item — 1 sessão)

Cada item de "SPEC-00X+" vira um destes três estados: **FAZER** (entra como fatia curta),
**ACEITAR** (limitação honesta documentada no M11), ou **DEFERRED** (ADR/registro).

| # | Dívida | Origem | Decisão proposta |
|---|---|---|---|
| 1 | Nonce uso-único validado no OWNER (payments/inventory) | M4 | ACEITAR (TTL 90s + single-host dev; anotar) |
| 2 | Lock distribuído p/ reconciliador/recovery multi-instância | M3/M4 | FAZER leve no M8 (lease Mongo no worker) — 1h |
| 3 | Captura automática no caminho reconciliado/webhook | M3/M6 | FAZER (1 endpoint + política doc) — 2h |
| 4 | Refund idempotência em STORE persistente | M6 | FAZER (coleção refunds no Mongo) — 2h |
| 5 | Kafka→Rabbit E2E smoke ao vivo (worker consomendo OrderConfirmed real) | M6 | FAZER durante M8 (worker no cluster) — 1h |
| 6 | Fulfillment projection → timeline view do cliente | M6 | DEFERRED (SPEC-006+; API direta serve o dev) |
| 7 | Notificação provider real + opt-out REQ-CST-001 | M6 | ACEITAR (fake determinístico + opt-out por flag doc) |
| 8 | sitemap.xml + hreflang + OG images | M5→M8 | FAZER no M8 (integrado ao ingress/chart) — 2h |
| 9 | Login buyer na vitrine (Keycloak PKCE público) | M5→M6+ | FAZER leve no M8 (login + carrinho por subject) — 3h |
| 10 | CWV de campo (RUM) + busca por sinônimos/relevância | M5 | ACEITAR (laboratório medido; busca GIN básica) |
| 11 | F2 grafo MAF Workflows + suite completa do dataset eval | M7 | FAZER fatia curta no pós-M8 (Workflows é diferencial do plano) — 1 sessão |
| 12 | Métricas REQ-AI-008 completas (tokens/custo via Usage) | M7 | FAZER junto com 11 — 2h |
| 13 | ADR-0015 checkpoint durability (MAF) | M7 | ACEITAR (fake; smoke de resume na SPEC-007+) |
| 14 | Redis cache-aside real (PDP/busca) | M5 | FAZER no M9 (o L1/L2 exige cache p/ bater L2) — 3h |
| 15 | ADR-0001 YARP→forwarder (registrado no M5) | M5 | FAZER: remover YARP do Gateway (pacote morto) — 1h |
| 16 | OPEN-02 backorder / OPEN-03 emissão fiscal / M10 marketplace | negócio | DEFERRED por decisão do proprietário (registros já existem) |

**Regra:** itens FAZER entram como fatias curtas LOGO APÓS a fase correspondente (2,3,5,8,9,14,15
no M8; 14 no M9; 11,12 no pós-M8), sempre com EVD.

---

## Sequência executável única (dependências)

```text
FASE E (push/CI, paralelo)
FASE 4 M8 (8.1 charts → 8.2 kind+HPA/KEDA → 8.3 drill) ──┬─→ fatias curtas 2,3,5,8,9,15
FASE 5 M9 (9.1 harness → 9.2 L0→L1→L2 → 9.3 SLOs+ADR-0011) ─┤─→ fatia 14 (cache p/ L2)
FASE 6 M11 (essays EN + quickstarts + matriz + demos) ←──────┴─→ fatias 11,12 (pós-M8)
FASE 7 consolidação (decisões acima gravadas nos ADRs/risks)
Fechar: M11 gate (clone→run→verify externo) = FIM DO PLANO
```

## Critério de "tudo pronto" (checklist final do portfólio)

- [ ] Gates M0–M9 + M11 PASS com relatórios §16 e EVD (M10 DEFERRED registrado)
- [ ] CI verde no GitHub (push feito) em todos os repos
- [ ] Demo no kind: compose→helm→smoke de compra→assistant→four-eyes→DLQ visível
- [ ] Benchmark L0–L2 com relatório reproduzível e SLOs congeladas por ADR
- [ ] Portfólio EN: essays + quickstart + matriz + vídeos + seção "não implementado"
- [ ] Clone→run→verify executado por avaliador externo sem ajuda
- [ ] Memória/AGENTS.md atualizados com o estado final

## Riscos do plano final

| Risco | Mitigação |
|---|---|
| kind sem recursos p/ L2 | L2 reduz alvo e registra; L3 só se houver folga |
| Ollama 27b pesado no cluster | modelo `external:` no host (já é o padrão do M7) |
| Runs longos atravessarem sleep do host | runs em background com relatório por run_id; retomável |
| Push do proprietário atrasar | CI local continua; item E fecha quando acontecer |
