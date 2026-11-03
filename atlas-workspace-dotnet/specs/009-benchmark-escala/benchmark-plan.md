# SPEC-009 — benchmark-plan.md (plano de execução L0–L4 — A6 do plano-mestre de execução)

**Regra de ouro:** congelar gate ANTES do teste; registrar ambiente/seed/hardware; resultados negativos são resultados.

## Estágios e cargas

| Estágio | Ambiente | Catálogo RPS | Checkout attempts/s | Pedidos confirmados/s | Duração | O que prova |
|---|---|---:|---:|---:|---|---|
| L0 | Local funcional (1 processo, sem K8s) | 50 | 1 | depende PSP fake | 10 min | correção/observabilidade sob carga leve |
| L1 | Compose, 1 instância/serviço | 1.000 | 20 | medir | 30 min + soak 2h | baseline p50/p95/p99, recursos |
| L2 | K8s, réplicas + caches | 10.000 | 200 | medir | 1h + spike 10x | escala horizontal, backpressure, HPA/KEDA |
| L3 | K8s + chaos | 100.000 | 2.000 | medir | soak 8h + chaos | limites, degradação graciosa, recuperação |
| L4 | Modelo apenas | 1.000.000 | 10.000+ | — | — | hipótese com custo/sharding/multi-região; NUNCA alegado |

## Workloads (distribuição)

Navegação 80% · busca 12% · PDP 5% · carrinho/quote 2% · checkout 0.7% · AI 0.3% (com hot-key skew: 1% dos SKUs = 40% do tráfego; catálogo 10^6 SKUs sintético versionado).

## Gates de aceite por run (congelar antes)

- Erro HTTP < 0.1% em steady; p95 dentro do alvo do estágio; oversell=0 e duplicate_paid_order=0 verificados durante o run (invariantes sob carga); projection lag p99 < alvo; sem OOM/threadpool starvation.

## Métricas coletadas

Latências por operação (p50/95/99/99.9), throughput real, erros por classe, CPU/mem/GC(LOH, gen2)/threadpool/sockets, Kafka consumer lag, Rabbit queue depth, outbox age, Redis hit ratio, PSP fake latência, energia estimada (TDP×tempo).

## Ferramentas

k6 (journeys HTTP), Locust (alternativa), scripts Kafka perf, `dotnet-counters/trace`, BenchmarkDotNet (micro de hot paths), Lighthouse (front), dashboards Grafana exportados por run.

## Runs mínimos do M9

1. L0 correto (10 min) · 2. L1 baseline (30 min + soak 2h) · 3. L2 escala (1h + spike) · 4. L3 se cluster disponível · 5. Comparativo réplicas×caches com IC 95% · 6. Hot-SKU contention run dedicado.
