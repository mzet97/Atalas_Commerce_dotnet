# AI Shopping Assistant — Microsoft Agent Framework in C# (.NET 10)

## Confirmed architecture

The assistant is a standalone deployable module/service within `atlas-storefront-dotnet`, implemented **entirely in ASP.NET Core 10/C# 14** and Microsoft Agent Framework, independent from the commercial transaction engine. The only external non-C# component may be a model inference server, such as Ollama. No Python, LangGraph, LangChain or Semantic Kernel runtime. The assistant never owns state from Orders, Payments, Offers or Inventory.

## Technical concepts (validate precise SDK API before coding)

`Microsoft.Agents.AI` core, `Microsoft.Agents.AI.Workflows` graph workflows; `Microsoft.Extensions.AI.IChatClient` as provider boundary; `AIAgent`/`ChatClientAgent`; `AgentSession` for conversation history; `WorkflowBuilder` and typed executors for explicit graphs; tools with typed C# inputs/outputs. The official 2026 examples may change — smoke the selected pinned NuGet package and reference docs on the day of implementation. Never invent classes/functions from a different version.

## Bounded graph

1. Validate customer subject, locale, market and tool scopes.
2. Interpret shopping intent (or deterministic fallback if clear filter query).
3. Call authorized catalog search (read-only API); gather product ID/SKU/Offer ID.
4. Retrieve current offer, availability hint, shipping estimate and metadata provenance.
5. Compare offers and produce a grounded recommendation; never fabricate price/stock.
6. Clarify constraints if material (budget, region, preference).
7. If customer wants a cart change, create a **proposed** action, not a write.
8. Render action summary in trusted Next.js UI; human explicitly confirms.
9. Validate consent nonce, expiry, payload/subject hash on server; call Cart owner API.
10. Direct customer to conventional checkout which **re-quotes** and requires payment authorization via trusted UI.

MAF is not an orchestrator for Payment Saga or CQRS. Domain code handles deterministic transitions and policy checks.

## Tool security matrix

| Tool | Credential/scope | Side effects | Allowed |
|---|---|---|---|
| SearchCatalog | guest/customer market scope | None | Yes |
| GetProductDetails | locale market | None | Yes |
| GetCurrentOffer | quote context | None | Yes |
| GetInventoryHint | warehouse entitlement abstracted | None | Yes |
| CompareOffers | public offer IDs | None | Yes |
| GetCustomerCart | customer session | None | Yes, auth only |
| ProposeCartChange | validated item list | Proposal object only | Yes |
| ApplyApprovedCartChange | **server approval artifact** | Cart owner write | UI-driven only, not MAF tool |
| CapturePayment/Refund/ModifyStock/SetPrice/GrantRole | none | Dangerous | **Never** |

Threat model: prompt injection in untrusted product reviews, hallucinated discounts, model error, untrusted MCP tool, unauthenticated sessions, cross-user data leakage, unauthorized cart action, malicious provider response, tool outputs as instructions, SSRF and token leakage. Server authorization, schema validators and allowlists must prevent escalation **even if model ignores system prompt**.

## Provider and on-prem experience

- Implement a model provider adapter behind `IChatClient`; local inference provider with verified tool calling and streaming is preferred for portfolio demonstration.
- A deterministic fake provider supports unit/integration tests but **is not proof of real inference quality**.
- Cloud provider is an optional configuration with explicit privacy review and budget approval.
- Model switching requires regression evaluation. Test `pt-BR`, `en-US`, `es-ES`, short/long product descriptions, multi-turn shopping conversations and injection attempts.

## Human-in-the-loop and persistence

Persist only necessary session/workflow state with tenancy, TTL and deletion policy. `AgentSession` is not a database of orders. If graph checkpoint/resume is used, prove durability and compatibility on the pinned SDK, and avoid replaying any mutation automatically after restart. Persist audit records of tool invocation/approval (redacted) and ensure cart updates have idempotency keys.

## Minimum gates

- Build + run a real MAF C# agent with an approved provider, or explicitly mark provider unverified.
- Programmatically prove all tools read-only except UI-mediated cart.
- Product/offer references are valid IDs from owner APIs; response exposes provenance and quote expiry.
- Prompt injection cannot cause payment/stock/role mutation; authorization tests at backend level.
- Concurrent users do not share session/state/grants.
- Provider timeout switches to storefront's standard search route without impairing checkout.
- Traces for tool calls and model costs/latency exclude secrets and PII.
- All test evidence with run commands, exit codes, model/provider versions and test IDs.

## Official docs

- https://learn.microsoft.com/en-us/agent-framework/overview/
- https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/
- https://learn.microsoft.com/en-us/agent-framework/agents/providers/
- https://learn.microsoft.com/en-us/agent-framework/agents/providers/ollama
- https://github.com/microsoft/agent-framework/tree/main/dotnet/samples
- https://www.nuget.org/packages/Microsoft.Agents.AI
- https://www.nuget.org/packages/Microsoft.Agents.AI.Workflows
