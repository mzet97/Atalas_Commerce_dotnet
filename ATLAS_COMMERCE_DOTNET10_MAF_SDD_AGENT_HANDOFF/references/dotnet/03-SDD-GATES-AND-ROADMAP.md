# Custom SDD — gates, repository process and roadmap .NET 10

**Do not use GitHub Spec Kit.** Every decision is a markdown-spec issue documented as a versioned contract, implemented via an atomic task, verified by real tests and linked to evidence.

## File layout (per repository)

```
sdd/{prompt,audit,source-manifest,glossary,requirements,roadmap,risks,decisions-open,traceability,progress}.md
sdd/reports/
sdd/evidence/
specs/000-business-discovery/{README,audit,spec,design,tasks,acceptance.feature,traceability,evidence,risks}
specs/001-platform-foundation/...
architecture/decisions/ADR-0001-*.md
architecture/diagrams/*.mmd
architecture/runbooks/
```

`SPEC` references `REQ`, `INV`, `NFR`, `SEC`, `AI`; `TASK` includes exact paths, prerequisites, command to prove acceptance, risks/rollback and state; `TEST` points to automated suite/scenario; `EVD` records commit, environment, versions, command, exit code and real outputs. User corrections take precedence and cause spec diff + ADR; no retroactive editing of evidence to appear successful.

## Milestones

| M | Goal | Exit evidence |
|---|---|---|
| M0 | Recon, business-first modeling, source hashes, ER/classes/usecases, ADR proposals | read sources, invariants traced, SPEC-001/002 proposed; **no scaffolding** |
| M1 | .NET SDK, slnx, CLI scaffolding templates, CI, Compose, Keycloak, OTel | one real service + admin host, auth and tracing, deterministic builds/tests |
| M2 | Admin Angular → C# modular host → C# Catalog/Offer/Inventory owners → Mongo/outbox/Kafka/PG/Next | real published SKU, owner policy check, idempotent projection, last-unit contention |
| M3 | Cart, quote, reservation, checkout saga, order, PSP Fake | repeatable local E2E, payment dedupe, timeout/compensation/reconciliation |
| M4 | Admin approvals/audit/warehouse controls | 4-eyes approval, scope auth, audit provenance, projection lag |
| M5 | International storefront search/SEO/accessibility | locale-market-currency E2E and measured CWV/a11y |
| M6 | Fulfillment, returns, refunds, RabbitMQ notifications | refund/return lifecycle and DLQ/retry proof |
| M7 | MAF in C# shopping assistant | allowlisted tools + human consent + provider smoke + eval + fallback |
| M8 | Helm/Kubernetes on-prem, Skaffold, Grafana stack | HA plan, rollout/rollback, backup restore drill, traces |
| M9 | Load/chaos/benchmark/reliability | reproducible measured throughput/latency/error/cost, no invented million TPS |
| M10 | 3P marketplace advanced scope | deferred by default pending explicit ADR |
| M11 | English portfolio case studies, architecture and demo | clone→run→verify guidance, reports and honest limitation inventory |

## Status protocol

`TODO`, `READY`, `IN_PROGRESS`, `PASS`, `FAIL`, `BLOCKED`, `DEFERRED`. A milestone cannot pass because 80% of tasks were estimated complete. Its critical invariants and acceptance gates must have passing test evidence. If an external dependency missing, use `BLOCKED` and describe the exact remediation; do not fake logs. BDD/Gherkin is documentation until linked to an actual executable test.

## First execution template

```
ATLAS .NET 10 — M0 REPORT
DATE_UTC: <actual>
REPOS / COMMITS: <actual or NO_REPO>
FILES_READ + SHA-256: <actual>
LEGACY_PYTHON_REFERENCES: <inventory with superseding docs>
BUSINESS_MODEL: <invariants and owners verified>
ADRs: <proposed, accepted by user, blocked>
SPECS + TASKS: <created files>
TESTS: <executed or NONE; do not confuse doc review with test>
PERFORMANCE: NOT_MEASURED
RISKS: <open issues>
NEXT: <first READY TASK after M0>
```
