# ADR-0008 — Microsoft Agent Framework em C# + abstração de provider via IChatClient

**Status:** ACCEPTED (2026-11-03 — spike M7 PASS: MAF 1.26.0 + tool calling Ollama qwen3.6:27b real; EVD-007-001)

## Contexto
O assistente de compras deve ser C#/.NET 10 com MAF; proibidos LangChain/LangGraph/Semantic Kernel como orquestrador e qualquer backend Python de produto. O provedor de modelo precisa ser substituível (local on-prem ↔ cloud opcional).

## Alternativas
1. **MAF (`Microsoft.Agents.AI` + `.Workflows`) com `IChatClient` como fronteira de provider** (proposta do contrato).
2. Orquestração manual de prompts sem framework — perde workflows tipados, checkpoint e observabilidade padronizada.
3. Semantic Kernel legado — proibido explicitamente pelo contrato (confusão de frameworks).

## Decisão proposta
- Módulo `src/AI/ShoppingAssistant/` no repo Storefront com subpastas Api/Domain/Application/Workflows/Agents/Tools/Providers/Memory/Safety/Evaluation/Infrastructure.
- Provider atrás de `Microsoft.Extensions.AI.IChatClient`: adapter para Ollama/endpoint OpenAI-compatible local (preferido para demo on-prem); cloud opcional com privacy review + orçamento; fake determinístico apenas para testes.
- Evolução em fases: F1 agente simples com tools e rules; F2 workflow determinístico tipado `Intent → Search → Compare → Clarify → Recommend`; F3 fan-out/memória durável apenas com necessidade provada.
- Smoke obrigatório da API da versão pinada (baseline 1.26.0, 2026-10-23) **antes** de codar; APIs do MAF mudam entre versões.

## Trade-offs
(+) uma stack só (C#); workflows tipados com checkpoint; provider trocável por configuração. (−) maturidade/velocidade de mudança do MAF (risco R-MAF-API); qualidade do modelo local precisa de validação de tool calling.

## Modos de falha / rollback
Falha: modelo local sem tool calling confiável → registrar `BLOCKED/INCOMPATIBLE` e seguir com fake em testes (sem alegar IA funcional). Rollback: adapter `IChatClient` permite trocar provider sem tocar workflows.

## Fontes
mestre §6.2, §9.1–9.5; `references/dotnet/02`; docs MAF (mestre §6.3).
