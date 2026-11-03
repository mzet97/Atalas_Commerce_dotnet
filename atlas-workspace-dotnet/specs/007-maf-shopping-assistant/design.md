# SPEC-007 — design.md

Estrutura interna (`references/dotnet/02` + mestre §9.2): `Api/` (HTTP/SSE chat, rate limit, auth), `Domain/` (ConversationId, Proposal, Consent, ToolGrant, Policy, Risk), `Application/` (use cases, ciclo de sessão/identidade, emissão de approval token), `Workflows/` (executores MAF, edges, checkpoint/resume), `Agents/` (Intent, Finder, Comparison, Recommendation, CartProposal), `Tools/` (adapters tipados p/ APIs owners; sem DB direta), `Providers/` (IChatClient: OllamaAdapter, FakeProvider), `Memory/` (sessão com TTL/retenção), `Safety/` (policies, allowlist, injection controls), `Evaluation/` (dataset/golden), `Infrastructure/` (OTel, quotas, cache).

## Fluxo de referência
Auth/escopo → Input validation & abuse policy → Intent (+locale/market/currency) → CatalogSearch (tool) → Offer/stock/shipping (tools read-only) → Comparison → Recommend (IDs + timestamps + TTL) → [se cliente quer carrinho] Proposta INERTE → UI confiança → consent artifact (nonce/TTL/hash) → `ApplyApprovedCartChange` (API fora do agente) → checkout normal com re-quote.

## Decisões técnicas
- MAF pinado (baseline 1.26.0; revalidar no dia); smoke compile antes de codar; `WorkflowBuilder`/executores tipados; checkpoint apenas se suportado (nunca reexecuta mutação).
- Ollama adapter: `Microsoft.Extensions.AI.Ollama` ou OpenAI-compatible endpoint; **validação obrigatória de function calling com modelo concreto** — se inadequado: registrar `BLOCKED/INCOMPATIBLE` e usar fake em CI sem alegar IA funcional.
- SSE p/ chat com cancelamento e buffer limitado; rate limit por usuário/sessão.
- Quotas: max tokens/turno, custo estimado zero (local), tempo máx de workflow, profundidade de recursão, circuit breaker por tool.

## Testes
Unit (policies, consent validation, grant), Integration (tools contra APIs reais com Testcontainers), AI-eval (dataset `eval-dataset.md` com grounding/injection/latência), E2E (chat→proposta→confirmação→carrinho), Fallback (provider down → busca convencional).
