# SPEC-006 — risks.md

| Risco | P | I | Mitigação |
|---|---|---|---|
| Duplicar notificação em reentrega Rabbit | Média | Médio | manual ack + notification_key único + testes de crash |
| Refund parcial inconsistente com capture | Média | Alto | refund ≤ capture verificado no Payments; conciliação diária |
| Carrier fake divergir de contrato real futuro | Baixa | Médio | Adapter isolado; contrato de carrier versionado |
| Broker restart perder mensagens | Baixa | Alto | quorum queues + publisher confirms + testes de restart |
