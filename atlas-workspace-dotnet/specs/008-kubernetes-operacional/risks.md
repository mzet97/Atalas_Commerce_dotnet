# SPEC-008 — risks.md

| Risco | P | I | Mitigação |
|---|---|---|---|
| Sem cluster disponível no ambiente | Média | Alto | k3s local como fallback documentado; gate M8 BLOCKED com remediação se nada existir |
| Operadores de dados indisponíveis | Média | Médio | StatefulSet+PV+scripts de backup com trade-off documentado |
| KEDA sem métricas de lag configuradas | Média | Médio | exporter de consumer lag obrigatório no M2+ |
| Credenciais no Git | Baixa | Alto | ExternalSecrets + auditoria de secrets no CI |
