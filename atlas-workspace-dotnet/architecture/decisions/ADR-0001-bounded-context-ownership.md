# ADR-0001 — Bounded context ownership e fronteira Admin→Owner

**Status:** PROPOSED · **Data:** 2026-10-27 · **Absorve:** blueprint ADR-001 e ADR-006 (LEGACY Python)

## Contexto
Dois produtos (Storefront = microserviços; Control = monólito modular Angular/ASP.NET Core 10) precisam operar o mesmo domínio comercial. Risco central: duas fontes de escrita para o mesmo agregado (split-brain, dual-write).

## Alternativas
1. **Owner único por agregado; Control comanda via gRPC autenticado** (escolhida para proposta).
2. Control escreve diretamente nas coleções Mongo dos serviços — proibida: viola ownership, gera dual-write e bypass de políticas.
3. Control mantém réplica sincronizada dos agregados — proibida: sincronização bidirecional inviável sem consenso; divergência inevitável.

## Decisão proposta
- Cada agregado tem **um write owner** (Catalog, Offers, Inventory, Orders, Payments…). Módulos do Control (`CatalogOperations`, `InventoryOperations`…) autenticam/autorizam o operador, registram intenção/aprovação/auditoria no **MongoDB próprio do Control** e enviam comando autenticado ao owner, que **revalida** políticas, escopo e pré-condições.
- Leitura administrativa: projeções PostgreSQL por contrato de consulta exposto; nunca acesso direto a tabelas internas.
- Comunicação in-process apenas **dentro** do monólito (DAG de módulos); cross-sistema sempre por contrato.

## Trade-offs
(+) zero dual-write; políticas aplicadas no ponto de mutação; auditoria consistente. (−) dependência do Control da disponibilidade dos owners (mitigar com retry idempotente, outbox de comandos e UX de estado pendente); latência extra de um hop.

## Modos de falha / segurança / rollback
Falha: owner indisponível → comando fica pendente auditável, nunca "sucesso presumido". Segurança: owner é o perímetro (INV-SEC-001). Reversão: possível enquanto não houver dados canônicos duplicados; após M2, reversão exige migração.

## Fontes
mestre §3.1, §5.1, §5.3; `references/dotnet/01` §1–2; blueprint #02 (contexts/ownership).
