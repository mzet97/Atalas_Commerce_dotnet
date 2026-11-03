# SPEC-007 — tasks.md (DRAFT)

| ID | Tarefa | Aceite | Estado |
|---|---|---|---|
| TASK-007-001 | Smoke do MAF pinado + Ollama adapter (tool calling real) | smoke compile+run documentado ou BLOCKED/INCOMPATIBLE registrado | TODO |
| TASK-007-002 | Módulo AI (estrutura interna + Api SSE + auth) | chat básico funciona com fake provider | TODO |
| TASK-007-003 | Tools read-only tipadas + allowlist + quotas | tools chamam owners; mutação inexistente como tool | TODO |
| TASK-007-004 | Consent artifact + ApplyApprovedCartChange | token p/ payload diverso rejeitado; revogação/expiração OK | TODO |
| TASK-007-005 | Workflow F2 tipado (Intent→…→Recommend/Propose) | fluxo determinístico com groundedness | TODO |
| TASK-007-006 | Safety: injection controls, schemas, limites | dataset de injection com bypass = 0 | TODO |
| TASK-007-007 | Sessões isoladas + TTL/exclusão | dois clientes sem leakage | TODO |
| TASK-007-008 | Observabilidade (tokens/latência/custo/denials) + fallback | provider down → busca convencional dentro do budget | TODO |
| TASK-007-009 | Eval completa (dataset) + smoke real + relatório M7 | métricas do dataset publicadas | TODO |
