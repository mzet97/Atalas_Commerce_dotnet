<div align="center">

# Atlas Commerce .NET 10

**International e-commerce platform built with Spec-Driven Development (SDD) — .NET 10 / C# 14 / Microsoft Agent Framework**

[![CI](https://github.com/mzet97/Atalas_Commerce_dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/mzet97/Atalas_Commerce_dotnet/actions/workflows/ci.yml)

</div>

---

Atlas Commerce is a full-stack, enterprise-grade international e-commerce platform: catalog,
pricing, inventory (zero-oversell), cart/quote, checkout saga with PSP reconciliation,
four-eyes admin approvals, fulfillment/returns, event-driven notifications and an AI shopping
assistant — built **spec-first**: every milestone is closed with executable evidence, never claims.

> **What makes this repo different:** it was built by an AI agent under a strict SDD contract —
> [M0→M9 gate reports](atlas-workspace-dotnet/sdd/reports/) with dated evidence (EVD-*),
> 30+ real production-grade bugs found and fixed with postmortems, and honest
> "what is NOT implemented" sections throughout.

## Highlights (all measured, see [Benchmarks](#benchmarks))

| | |
|---|---|
| **1.8M requests / 30 min @ 1000 RPS** | **0 failures**, p95 **4.76 ms** (Npgsql direct) |
| Cache-aside Redis (search) | p95 **0.61 ms** — 7.8× faster |
| Zero oversell | 10 concurrent reservations for 1 unit → exactly 1 sale (CAS on Mongo replica set) |
| Zero duplicate payments | idempotency keys + webhook HMAC dedup + reconciliation |
| Crash recovery | saga crashed *after* PSP authorization → confirmed; *before* → compensated |
| AI assistant | grounded tool-calling (local qwen via Ollama), injection-refusing, consent-gated |

## Architecture

Polyglot CQRS with transactional outbox. MongoDB is the authoritative write store;
PostgreSQL holds read projections; Kafka carries domain events; RabbitMQ carries notification
tasks. **Stale reads never authorize critical operations** — the owner revalidates everything.

```mermaid
flowchart LR
    subgraph Control Plane
        CTRL[Control · Angular + modular monolith<br/>four-eyes · audit chain]
    end
    subgraph Data Plane[Storefront · Next.js 16]
        UI[Vitrine i18n pt-BR/en-US/es-ES<br/>cart · checkout · AI chat]
    end
    subgraph Services[ASP.NET Core 10 microservices]
        GW[BFF Gateway :5180]
        CAT[Catalog] OFF[Offers] INV[Inventory] CHK[Checkout saga] PAY[Payments] FUL[Fulfillment] AI[MAF Assistant]
    end
    subgraph Events[Async backbone]
        OB[(outbox)] --> K[[Kafka]] --> PR[(PostgreSQL views)] & MQ[[RabbitMQ quorum + DLQ]]
    end
    subgraph Data[(Write stores)]
        MG[(MongoDB rs0)] & RD[(Redis)]
    end
    UI <--> GW
    GW --> CAT & OFF & INV & CHK & PAY & AI
    CAT & OFF & INV & PAY & FUL & CHK -.-> OB
    K --> PR & MQ
    CAT & OFF & INV & CHK & PAY & FUL & AI -.-> MG
    PR -.-> RD
```

**Key rule (INV-CQRS-001):** projected reads are *hints*. Checkout reserves stock on the owner
(atomic CAS), validates the frozen quote, and confirms payment only through authenticated PSP
responses — never from a cached view.

## Repositories layout (monorepo)

```
Atalas_Commerce_dotnet/
├── atlas-storefront-dotnet/        # Data plane — 48 .NET projects + Next.js 16 storefront
│   ├── src/Services/{Catalog,Offers,Inventory,Checkout,Payments,Fulfillment}/{Api,Application,Domain,Infrastructure}
│   ├── src/AI/ShoppingAssistant/   # MAF agent: read-only tools, consent HMAC, Ollama provider
│   ├── src/Workers/                # Projections (Kafka→PG) + Notifications (RabbitMQ quorum/DLQ)
│   ├── apps/storefront-next/       # Next.js 16 vitrine (i18n ×3, Playwright, axe, CWV)
│   ├── deploy/compose/             # Podman/Docker compose + Podman API runbook
│   └── tests/                      # 19 test projects (unit + integration + E2E + benchmark)
├── atlas-control-dotnet/           # Control plane — modular monolith + Angular
│   └── src/Modules/                # Approvals (four-eyes), InventoryOps (grants+audit chain), PaymentsOps
├── atlas-platform-contracts-dotnet/# .proto (buf lint), SDK, CLI templates, Helm charts, k8s, ops scripts
└── atlas-workspace-dotnet/         # SDD workspace: specs, gate reports, EVD evidence, ADRs, plans (pt-BR)
```

## Tech stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 LTS · C# 14 · `.slnx` · Central Package Management |
| AI | Microsoft Agent Framework (`Microsoft.Agents.AI` 1.26.0) + Ollama (local qwen, zero-cost) |
| Write | MongoDB replica set (transactions + outbox + CAS) |
| Read | PostgreSQL projections + Redis cache-aside (TTL 15 s, hint-only) |
| Async | Kafka (domain events) · RabbitMQ 4 quorum queues (notification tasks, DLQ) |
| RPC | gRPC schema-first (buf lint) between owner services |
| IAM | Keycloak (OIDC Auth Code + PKCE, RBAC by role, MFA for admin) |
| Frontend | Next.js 16 (PPR/cacheComponents) + Angular (control) |
| Tests | xUnit · **154 automated tests / 19 projects** · Playwright (9/9 E2E) · axe · k6 · MAF fake (CI) + real (Ollama) |

## Quickstart

Prerequisites: .NET 10 SDK, Podman or Docker, Node.js 20+ (storefront), k6 (benchmarks only).

```bash
# 1) Infrastructure (Mongo rs0, PG, Redis, Kafka, RabbitMQ, Keycloak)
podman play or: docker compose — see atlas-storefront-dotnet/deploy/compose/

# 2) Run the services (dev ports — see table below)
dotnet build atlas-storefront-dotnet/Atlas.Storefront.slnx
#   each host: dotnet run --project src/Services/<Service>/<Service>.Api

# 3) Seed the catalog through the REAL pipeline (draft → publish → outbox → Kafka → PG view)
curl -X POST http://127.0.0.1:5299/admin/drafts -H 'Content-Type: application/json' \
  -d '{"marketId":"BR","titles":[{"locale":"pt-BR","title":"Câmera Atlas Pro X200"}],"skuCodes":["SKU-CAMERA-BR"]}'

# 4) Storefront (pt-BR / en-US / es-ES)
cd atlas-storefront-dotnet/apps/storefront-next && pnpm install && pnpm dev   # :3000

# 5) Tests
dotnet test atlas-storefront-dotnet/Atlas.Storefront.slnx
pnpm exec playwright test   # apps/storefront-next — purchase ×3 locales, a11y, CWV
```

Service ports (dev): BFF Gateway `5180` · Catalog `5299`(+gRPC `5290`) · Offers `5293` ·
Inventory `5291`(+gRPC `5292`) · Checkout `5297` · Payments `5398` · Fulfillment `5294` ·
AI Assistant `5296` · Control `5099` · PSP fake `5399` · Keycloak `8090`.

## What is proven (gates M0→M9 — all with dated evidence)

| Gate | Proven capability |
|---|---|
| **M0** Spec/audit | 32 handoff sources verified (SHA-256), unique ownership per aggregate, 12 specs, 19 ADRs |
| **M1** Foundation | 3 repos + CLI templates + CI + compose runbook, OIDC smoke (PKCE+MFA), cross-process traces |
| **M2** Vertical slice | Admin→four-eyes→gRPC→publish→outbox→Kafka→projection→**product on the storefront**; repeated event ≠ duplicate view |
| **M3** Checkout saga | contended last unit (10 racing → 1 sale); same idempotency key → same result; PSP timeout → **reconciler confirms/compensates**; crashed saga recovered; webhook HMAC + dedup |
| **M4** Admin hardening | self-approval → 409 **audited**; grant revoked → 403 on next call; SHA-256 hash-chained audit trail + tamper detector; **CQRS lag visible** (owner head vs PG view) |
| **M5** Storefront | purchase E2E ×3 locales; axe 0 critical/serious; LCP 0.05 s / CLS 0.036 |
| **M6** Ops | fulfillment state machine (idempotent transitions, out-of-window → 409); refund **four-eyes** → only CAPTURED intents, idempotent; notifications **never duplicated** (8 racing workers → 1 send), poison → **DLQ** after retry/backoff |
| **M7** AI assistant | real tool-calling with local LLM; **injection refused**; **consent HMAC inviolable** (payload/subject mismatch → 403); fallback to conventional search when LLM down |
| **M8** K8s/Helm | Helm charts (37 resources: Deployments/Services/NetworkPolicies deny-by-default/HPAs), ingress TLS, KEDA scalers, backup + restore-drill scripts — **lint+template validated** |
| **M9** Benchmark | L0 + L1 real runs (below) · L2 pending cluster · **L4 never claimed** |

## Benchmarks (k6, constant-arrival-rate, compose local)

| Run | Load | Result |
|---|---|---|
| L0 · 10 min | 50 RPS | 30,001 reqs · p95 **4.2 ms** · 0 interrupted |
| L1a · 30 min (no cache) | 1000 RPS | **1,799,923 reqs · 0 failures · p95 4.76 ms** |
| L1b · 10 min (Redis cache-aside) | 1000 RPS | p95 **0.61 ms** (7.8× vs baseline) |

SLOs frozen by [ADR-0020](atlas-workspace-dotnet/architecture/decisions/ADR-0020-slo-frozen.md)
from measured data: search p95 ≤ 100 ms cached / ≤ 1500 ms uncached · HTTP errors < 0.1% steady ·
`oversell=0` · `duplicate_paid_order=0` · quote checks 100%.

> **L2/L4 honesty:** 10k RPS needs a cluster (runtime blocked in this dev environment, see
> below); million-RPS numbers are **never claimed**.

## Documentation

| Path | Content |
|---|---|
| [`atlas-workspace-dotnet/sdd/`](atlas-workspace-dotnet/sdd/) | Full SDD: specs, requirements, ADRs, gate reports, EVD evidence (pt-BR) |
| [`atlas-workspace-dotnet/sdd/reports/`](atlas-workspace-dotnet/sdd/reports/) | Gate reports M0→M9 (dated, format §16) |
| [`atlas-workspace-dotnet/architecture/decisions/`](atlas-workspace-dotnet/architecture/decisions/) | ADR-0001..0020 (ACCEPTED/PROPOSED) |
| [`atlas-storefront-dotnet/deploy/compose/README-podman.md`](atlas-storefront-dotnet/deploy/compose/README-podman.md) | Podman rootless runbook (API-driven) |
| [`atlas-platform-contracts-dotnet/infrastructure/`](atlas-platform-contracts-dotnet/infrastructure/) | Helm charts, k8s manifests, backup/restore-drill scripts |

## What is NOT implemented (honesty section)

- **K8s runtime execution**: node NotReady in this dev environment (cgroup v2 `cpuset` not
  delegated to the distrobox namespace — kubelet cannot start). Charts are lint/template-validated
  and applied to the `dotnet-atlas` namespace (pods Pending); real rollout/HPA/restore-drill
  runs require a Ready node (M11 clone→run→verify).
- **Buyer login on storefront** (Keycloak PKCE public client) — admin/operator auth is real;
  storefront checkout is guest-mode.
- **L2 benchmark** (10k RPS + spike) and **2 h soak** — harness ready, execution pending.
- **MAF Workflows F2 typed graph** (Intent→Search→Compare→Recommend→ProposeCart) — F1 agent
  with tools+guardrails is delivered; real-model eval covers 1 anchor case (full dataset pending).
- **Real PSP / real e-mail provider** — deterministic fakes per contract (ADR-0017); refund
  idempotency store is persistent, approval tokens are HMAC (asymmetric keys pending).
- **Marketplace 3P (M10)** — deferred by design.

## Postmortem highlights

30+ real bugs found by the evidence-first process (full postmortems in gate reports), including:

- **Duplicate-key inside a MongoDB transaction aborts the transaction** — swallow-and-continue
  poisoned the txn and the driver retry looped ~985 times. Fix: read-first inside the txn.
- **A single outbox dispatcher pointed at one database** — inventory events were never
  dispatched in live mode (only proven in inline tests). Found by the L1 benchmark.
- **Nullable tool parameters produce `type: [string, null]`** JSON Schema that breaks the
  OllamaSharp serializer — the AI turn silently fell back mid-conversation.
- **YARP routes silently never matched** since M2 (the storefront was using a direct endpoint).

## License

All rights reserved © 2026 — internal project (dev credentials in this repo are
explicitly dev-only and documented as such).
