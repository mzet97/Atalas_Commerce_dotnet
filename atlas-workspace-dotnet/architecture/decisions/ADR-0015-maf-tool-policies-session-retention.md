# ADR-0015 — MAF: políticas de tools, checkpoint/session e retenção de PII

**Status:** PROPOSED · **Data:** 2026-10-27

## Contexto
Sessões, memória e checkpoints do agente não são verdade de negócio; conter PII e podem virar vetor de vazamento. Ferramentas precisam de allowlist, escopo e limites verificáveis em código.

## Alternativas
1. **Allowlist de tools em código + validação de schema por tool + quotas (tokens/custo/tempo/recursão) + sessão persistida com TTL/tenancy e política de exclusão; checkpoint somente com durabilidade provada na versão pinada** (proposta).
2. Liberdade de tools definida apenas no system prompt — proibida: injection pode manipular; guardrails devem existir em código.
3. Sem persistência de sessão (stateless puro) — perde multi-turn de qualidade; a persistência com TTL é o equilíbrio.

## Decisão proposta
- `ToolGrant` por sessão com escopos derivados do subject; matriz de tools do `references/dotnet/02` (read-only + proposta inerte; mutação nunca é tool).
- Sessão separada de negócio (`CustomerSession` ≠ `AgentSession`); retenção/exportação/exclusão conforme consentimento (INV-PII-001).
- Checkpoint/resume do workflow: usar apenas se suportado pela versão pinada; nunca reexecutar mutação automaticamente após restart; idempotency keys explícitas.
- Traces/logs sem prompts completos nem PII; custos e latências medidos por conversa.

## Trade-offs
(+) superfície auditável; privacidade por padrão. (−) esforço de implementar quotas/allowlist custom; limita "capacidades" do agente por design (intencional).

## Modos de falha / rollback
Falha: checkpoint incompatível após upgrade do MAF → desativar resume e recomeçar sessão (sem risco financeiro). Rollback: desligar módulo AI não afeta checkout/admin (REQ-AI-007).

## Fontes
mestre §9.4, §9.6, §9.7; `references/dotnet/02` §Human-in-the-loop/Minimum gates.
