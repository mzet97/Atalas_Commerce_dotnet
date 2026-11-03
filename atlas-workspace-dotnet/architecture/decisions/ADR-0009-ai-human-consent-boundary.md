# ADR-0009 — Boundary de consentimento humano para mutações da IA

**Status:** PROPOSED · **Data:** 2026-10-27

## Contexto
O LLM não é autoridade de negócio; o contrato proíbe compra autônoma, captura, estorno, preço, estoque e IAM por agente (INV-AI-001). A mutação de carrinho é o único efeito permitido, condicionado a confirmação humana verificável.

## Alternativas
1. **Consent artifact assinado no servidor (subject, nonce, TTL, hash canônico da proposta) emitido após confirmação explícita em UI confiável; `ApplyApprovedCartChange` fora do alcance do agente** (proposta).
2. Confiança na conversa ("o usuário disse sim no chat") — proibida: prompt não é evidência de consentimento; vulnerável a injection.
3. Agente sem capacidade de mutação alguma — mais simples, mas degrada a proposta de valor do assistente sem necessidade (a proposta inerte + confirmação é segura e auditável).

## Decisão proposta
- `ProposeCartChange` retorna **proposta inerte** (sem efeito). A UI Next.js renderiza o resumo e exige ação direta do usuário.
- O servidor emite consent artifact com nonce, TTL, customer ID e hash canônico; `ApplyApprovedCartChange` valida tudo e revalida preços/quantidades/autorização no Cart owner, com idempotency key.
- Consent token reutilizado para payload/subject diferente → rejeitado (AI-003). Revogação e expiração funcionais.
- Tool nunca exposta ao agente: a mutação ocorre em fluxo de API separado, acionado pela UI.

## Trade-offs
(+) consentimento verificável e auditável; superfície de ataque mínima. (−) fricção extra no fluxo; exigência de UX clara de confirmação.

## Modos de falha / rollback
Falha: artifact expirado → nova proposta. Ataque: tentativa de bypass → log + denial auditável. Rollback: desligar mutação de carrinho mantendo apenas recomendação.

## Fontes
mestre §9.3, §9.7, §13 (Gherkin AI); `references/dotnet/02` §Tool security matrix.
