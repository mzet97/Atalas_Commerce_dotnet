# SPEC-007 — risks.md

| Risco | P | I | Mitigação |
|---|---|---|---|
| Ollama local sem tool calling confiável | Média | Alto | Smoke primeiro (TASK-007-001); registrar BLOCKED/INCOMPATIBLE; fake apenas p/ CI sem alegar IA real |
| MAF mudar API na versão pinada | Média | Médio | pin exato; adapter isolado; smoke contínuo no CI |
| Injection emergente não coberta | Média | Alto | dataset versionado + casos âncora; guardrails em código (não só prompt) |
| Custo/latência fora do budget | Baixa | Médio | quotas por turno/workflow; métricas p95 |
| PII em traces | Média | Alto | redação por padrão; revisão de dashboards |
