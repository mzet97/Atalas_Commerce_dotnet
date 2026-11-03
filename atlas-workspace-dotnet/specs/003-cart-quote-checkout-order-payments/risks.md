# SPEC-003 — risks.md

| Risco | P | I | Mitigação |
|---|---|---|---|
| Estado UNKNOWN do PSP sem resolução | Média | Alto | Reconciliador com SLA + alerta para `PENDING_RECONCILIATION` idoso; caso de conciliação |
| Ordem reserva↔pedido↔pagamento mal escolhida gerar rework | Média | Médio | ADR-0006 com matriz de falhas; test-first com T01–T05 antes da implementação |
| Fault injection depender de Docker (ausente) | Alta | Médio | Alternativa: kill de processo local documentada; harness completo quando Docker existir |
| Divergência de arredondamento quote/order/projeção | Média | Alto | `Money` compartilhado via repo de contratos + property tests (FsCheck) |
| PSP fake confundido com gateway real | Baixa | Médio | ADR-0017: naming/headers explícitos "SIMULATED"; sandbox isolado |
