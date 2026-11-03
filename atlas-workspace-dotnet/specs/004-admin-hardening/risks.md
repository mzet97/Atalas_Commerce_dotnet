# SPEC-004 — risks.md

| Risco | P | I | Mitigação |
|---|---|---|---|
| Token de aprovação falsificável | Baixa | Alto | Assinatura assimétrica + payload hash + TTL curto; verificação no owner |
| Cadeia de auditoria com gap em falha de escrita | Média | Alto | Escrita na mesma transação do comando; verificador contínuo + alerta |
| Revogação com lag de TTL permitir janela | Média | Médio | TTL curto (minutos) + invalidação push quando possível; audit da janela |
| Dashboards confundirem projeção com verdade | Média | Médio | Componente freshness obrigatório; comandos revalidam no owner |
