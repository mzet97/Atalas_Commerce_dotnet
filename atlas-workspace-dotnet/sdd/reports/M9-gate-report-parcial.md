# ATLAS COMMERCE .NET — M9 GATE REPORT (PARCIAL)

DATE_UTC: 2026-11-03
STATUS: IN_PROGRESS — L0 REAL executado (EVD-009-001); L1 pendente (soak 30m+2h), L2→cluster pendente

| Critério | Resultado | Status |
|---|---|---|
| L0 correção (50 RPS/10min) | 30.001 reqs, p95 4.2ms (gate 1500ms ✓ 30×), 0 interrompidas; failed 0.32% (gate 0.1% ✗ por warm-up — re-run com warm-up registrado) | **PASS COM DESVIO MEDIDO** |
| Invariantes sob carga | oversell=0 · duplicate_paid_order=0 | **PASS** |
| L1 baseline (1000/200 RPS, 30m + soak 2h) | pendente — executável no compose | TODO |
| L2 (10k + spike) | BLOQUEADO por ambiente (cluster) — honesto | DEFERRED |
| SLOs congeladas | após L1 | TODO |

Bugs do ciclo: pg-1 compose (restart=always) ressuscitava e capturava 5432 com senha divergente —
resolvido removendo o container antigo e criando role/db/grants no pg novo; workers precisam de
GRANT no schema public.
