# ADR-0020 — Congelamento das SLOs pós-benchmark L1

**Status:** ACCEPTED (2026-11-03 — base MEDIDA no L1 real, EVD-009-002) · **Data:** 2026-11-03

## Decisão

SLOs congeladas para as rotas cobertas pelo k6 (busca/search, PDP list, quotes):
- **p95 busca ≤ 100ms** com cache-aside (medido: 0,61ms) e **≤ 1500ms** sem cache (medido: 4,76ms)
- **Erro HTTP < 0,1%** em regime estável (medido: 0,00% em 1,8M reqs / 30min)
- **checks quotes = 100%** (medido)
- **oversell = 0** e **duplicate_paid_order = 0** (invariantes, testadas durante os runs)
- Projection lag: acompanhado via /modules/ops/overview e cqrs-lag (gate p99 < 5s na SPEC-008+)

## Contexto

L1 real: 1,8M reqs @ 999,9 RPS por 30min sem cache (p95 4,76ms) + 600k reqs @ 1000 RPS por 10min
com cache-aside Redis (p95 0,61ms) — compose local. Revisão apenas com novo benchmark (L2).
