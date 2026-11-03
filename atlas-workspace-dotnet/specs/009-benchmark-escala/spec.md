# SPEC-009 — spec.md

**Problema:** quantificar o comportamento da plataforma sob carga por dimensão correta (pageviews ≠ checkouts ≠ pedidos pagos), com metodologia reproduzível e relatórios honestos incluindo limitações.

**Atores:** SRE; harness de carga (k6/Locust, ferramentas Kafka, Lighthouse).

## Requisitos

1. **Separar métricas** (mestre §10.2): catálogo RPS, busca, add-to-cart/s, quote/s, checkout attempts/s, pedidos confirmados/s, eventos/s, AI turns/s — reportadas independentes.
2. **Estágios L0–L4** (blueprint #05 §3) com gates **congelados antes** de cada run: L0 laptop funcional; L1 Compose 1 instância; L2 K8s multi-instância; L3 estresse/soak/chaos; L4 hipótese de escala com modelo de custo (sem promessa).
3. **Metodologia:** warmup, steady, spike 10x, soak longo, chaos; carga aberta (arrival-rate); dataset/seed versionado (10^6 SKUs com skew + hot products); coleta p50/p95/p99/p99.9, erros, saturação (CPU/mem/GC/threadpool/IO), queue/projection lag, custo energético estimado.
4. **Artefatos por run:** `results/<run_id>/` com commit SHA, lockfiles, hardware fingerprint, parâmetros, dashboards/traces exportados, CSV/JSON brutos e gráficos.
5. **Baseline comparativo:** single-instance vs réplicas/caches com intervalo de confiança; hotspot de SKU quente medido (contenção de reserva).
6. **Relatório final:** o que foi medido, o que NÃO foi (multi-região, PSP real), extrapolações rotuladas como hipótese.

## Critérios de aceite
Runs L0–L2 (mínimo) reproduzíveis por comando documentado; L3 conforme ambiente; L4 apenas modelo; zero alegação além do medido; SLOs permanecem PROPOSED até medição aprovada.

## DoD
Relatório M9 com tabelas/gráficos por estágio; comparação com SLOs propostos; limitações explícitas.
