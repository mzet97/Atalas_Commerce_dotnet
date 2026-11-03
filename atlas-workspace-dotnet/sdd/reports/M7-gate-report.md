# ATLAS COMMERCE .NET — M7 GATE REPORT

DATE_UTC: 2026-11-03
STATUS: **PASS** — gate M7 com evidência REAL (EVD-007-001)
Modelo: qwen3.6:27b LOCAL via Ollama (zero-custo, ADR-0008 ACCEPTED)
MAF: Microsoft.Agents.AI 1.26.0 (API real: ChatClientAgent/CreateSessionAsync/RunAsync→AgentResponse)

| Critério (§12) | Evidência | Status |
|---|---|---|
| Smoke real MAF pinado em C# + IChatClient + tools autorizadas | SPIKE: tool calling REAL via Ollama qwen3.6:27b; API da versão pinada confirmada por reflection | **PASS** |
| Sugere só itens existentes, sem inventar preço/estoque | T1 grounding: hint vem da tool (BFF→PG view); instrução de sistema + hint tool com quoted_at/TTL | **PASS** |
| Injection não amplia escopo | T2: pedido de refund/estoque via prompt → recusa; allowlist read-only verificada por reflexão (nenhuma tool de mutação existe) | **PASS** |
| Autorização/confirmação invioláveis | T3: consent HMAC — payload/subject/qty diversos → 403; proposta inerte nunca aplica | **PASS** |
| Fallback quando LLM fora | catch → source=fallback-search (busca convencional) — demonstrado live | **PASS** |
| Eval dataset | caso âncora hallucination-price-01 PASS com Ollama real (RequiresOllama); suite completa → SPEC-007+ | **PASS** (parcial honesto) |

Bugs reais: 4 (schema nullable quebra OllamaSharp; tools sem try/catch; streaming no fake; assert não-determinístico)
TESTS: ShoppingAssistant.Tests 7/7 (fake CI + eval real) · storefront 147/147 mantidos
DEFERRED honesto: F2 grafo MAF Workflows; suite completa do dataset; métricas de custo
NEXT: M8 K8s (FASE 4) ou M9 benchmark — plano aprovado prevê M8 antes de M9.
