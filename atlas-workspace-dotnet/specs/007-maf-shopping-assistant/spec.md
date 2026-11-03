# SPEC-007 — spec.md

**Problema:** assistir descoberta/comparação de produtos e propor carrinho com segurança: o LLM nunca é autoridade de negócio (INV-AI-001); toda mutação passa por consentimento humano verificável (ADR-0009).

**Atores:** cliente autenticado/visitante; assistente (sistema); owners (Catalog/Offers/Cart) via tools.

## Requisitos (herdam REQ-AI-001..008 + AI-001..005)

1. **Workflows MAF:** F1 agente simples com tools+rules → F2 grafo tipado `Intent → Search → Compare → Clarify? → Recommend → ProposeCart?` com executores tipados; F3 fan-out/memória apenas com necessidade provada.
2. **Tools read-only (allowlist em código):** SearchCatalog, GetProductDetails, GetCurrentOffer, GetInventoryHint, CompareOffers, EstimateShipping, GetCustomerCart(auth), ProposeCartChange (retorna proposta inerte). Schema validation por tool; limites de tokens/custo/tempo/recursão.
3. **Mutação de carrinho:** somente `ApplyApprovedCartChange` via API após consent artifact (subject, nonce, TTL, hash canônico) emitido por ação explícita na UI; revalidação de preços/quantidades no Cart owner; token para payload/subject diverso = rejeitado.
4. **Grounding:** respostas citam apenas IDs/preços/estoque retornados por owners com `quoted_at`/TTL; nunca inventa valores; provenance exposta na UI.
5. **Sessões:** `AgentSession` isolada por cliente (histórico/tokens/carrinho); TTL e política de exclusão (INV-PII-001); persistência mínima.
6. **Segurança:** prompt injection em conteúdo de catálogo/reviews não amplia escopo; outputs de tools = untrusted; guardrails em código; sem MCP externo sem allowlist.
7. **Provider:** `IChatClient` com adapter Ollama local (validação real de tool calling/JSON/streaming); fake determinístico para CI; cloud opcional desativada por padrão (decisão do proprietário).
8. **Observabilidade/eval:** métricas REQ-AI-008; dataset versionado (`eval-dataset.md`); fallback para busca convencional dentro do budget de latência.

## Fora de escopo
Compra autônoma, estorno, preço/estoque por IA, memória cross-sessão persistente de longo prazo.

## Critérios de aceite
AI-001..005 PASS com EVD (injection negada por ausência de tools; consent rejeita payload diverso; sessões isoladas; timeout→fallback; smoke real do MAF pinado com Ollama ou limitação registrada).

## DoD
Eval executada (grounding/injection/latência); traces sem PII; smoke do provider real documentado; relatório M7.
