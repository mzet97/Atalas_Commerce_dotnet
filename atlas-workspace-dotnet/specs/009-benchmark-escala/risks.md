# SPEC-009 — risks.md

| Risco | P | I | Mitigação |
|---|---|---|---|
| Ambiente local limitar L2/L3 | Alta | Médio | reportar o que o hardware permitiu; L4 sempre hipótese |
| Resultado ruim ser omitido | Baixa | Alto | contrato: resultados negativos são resultados; reviewer confere pastas |
| Carga mal calibrada (coordenação de clientes) | Média | Médio | arrival-rate aberta + warmup documentado |
| Custo de tempo dos soaks | Média | Baixo | soaks agendados fora do horário; 2h mínimos |
