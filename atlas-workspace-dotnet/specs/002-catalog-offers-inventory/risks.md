# SPEC-002 — risks.md

| Risco | P | I | Mitigação |
|---|---|---|---|
| Debezium inviável no ambiente local (requer Kafka Connect) | Média | Alto | Bridge C# (BackgroundService lê outbox→produz Kafka) como fallback documentado; semântica at-least-once mantida (ADR-0003) |
| Contenção de SKU quente degradar reservas | Média | Alto | CAS + métrica `reservation_contention_total`; benchmark de hotspot no M9 |
| Divergência projeção↔owner confundir operador | Média | Médio | source_version/PUBLISHING na UI; runbook de rebuild (TEST-002-005) |
| Docker ausente travar integração (Testcontainers) | Alta (constatado) | Alto | Instalação pendente do proprietário; unit/contract avançam antes |
| gRPC deadlocks/latência Admin→owner | Baixa | Médio | deadlines por chamada; retry apenas idempotente |
