# Runs L0/L1 (compose — não dependem de cluster)
- L0: RATE=50 DURATION=10m k6 run bench/k6-checkout.js → gates: erro<0.1%, p95<1.5s, oversell=0
- L1: RATE=1000 DURATION=30m + soak 2h RATE=200 → p50/p95/p99 por rota + lag cqrs-lag
- Durante o run: contagem duplicate_paid_order (mongo) + oversell (reserved<=on_hand)
- L2 (10k/200) BLOQUEADO por ambiente (cluster) — registrado honesto; L4 nunca alegado
