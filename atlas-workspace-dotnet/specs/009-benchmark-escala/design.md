# SPEC-009 — design.md

O desenho operacional deste milestone **é o plano de benchmark** — ver `benchmark-plan.md` (anexo normativo desta spec). Síntese das decisões de design:

- **Separação de dimensões:** harness gera journeys independentes por operação (navegação, busca, PDP, carrinho/quote, checkout, AI) para que RPS de leitura nunca seja reportado como capacidade de compra (mestre §10.2).
- **Reprodutibilidade:** cada run = pasta `results/<run_id>/` com commit SHA, lockfiles, fingerprint de hardware (CPU/mem/disco/rede), parâmetros, dados brutos (CSV/JSON), gráficos e exportações de dashboard; comando de rerun documentado no próprio run.
- **Dataset:** seed sintética determinística (hash versionado) de 10^6 SKUs com distribuição skew (1% dos SKUs = 40% do tráfego) + hot-SKU dedicado ao teste de contenção de reserva.
- **Carga aberta:** k6 em arrival-rate (não VUs fixos) + warmup padronizado; soaks agendados; spike 10x no L2.
- **Invariáveis sob carga:** os testes de oversell/idempotência rodam **durante** o run (não só em CI), provando INV-STOCK-001/003 e INV-PAYMENT-001 sob estresse.
- **Custo:** estimativa energética (TDP×tempo) e custo de oportunidade do hardware on-prem registrados por run.
- **Honestidade:** L4 é modelo de capacidade (sharding/partições/multi-região/PSP) com premissas explícitas — jamais apresentado como medida.

Ferramentas: k6 (principal), Locust (cross-check), `dotnet-counters`/`dotnet-trace`, BenchmarkDotNet (hot paths micro), Lighthouse CI (front), scripts Kafka perf.
