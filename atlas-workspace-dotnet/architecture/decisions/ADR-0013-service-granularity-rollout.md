# ADR-0013 — Granularidade de serviços e rollout incremental

**Status:** PROPOSED · **Data:** 2026-10-27

## Contexto
A árvore-alvo lista ~7+ serviços no Storefront, mas o contrato proíbe criar dezenas de serviços vazios: serviços emergem por fatia vertical com caso de uso funcional, observável e testável.

## Alternativas
1. **Rollout por milestone: M2 cria apenas Catalog, Offers, Inventory (+ Control host); M3 adiciona Cart, Checkout, Orders, Payments; demais (Fulfillment, Returns, Reviews, Notifications, Customer) só quando seus milestones chegarem** (proposta).
2. Criar todos os serviços no M1 — proibido pelo contrato (bounded contexts vazios).
3. Monólito modular único também para o Storefront — contraria a decisão de produto (dois produtos com estilos distintos) e o objetivo de portfólio.

## Decisão proposta
- Cada serviço nasce com: caso de uso vertical de ponta a ponta, health, tracing, contrato versionado e testes.
- Composição inicial mínima (M2): Catalog, Offers, Inventory + Control monolith + projection worker + contracts repo.
- Serviços "candidatos" (blueprint #06 §1) permanecem lista de reserva, não scaffold.

## Trade-offs
(+) cada serviço tem razão de existir; custo operacional gradual. (−) extrair serviço depois tem custo de migração (mitigado por contratos wire desde o início).

## Modos de falha / rollback
Falha: serviço mal fatiado (entidades genéricas) → revisão de contexto antes de expandir. Rollback: serviços raros podem ser fundidos mantendo contratos.

## Fontes
mestre §5.1 ("árvore alvo ilustrativa"), §12 (M1/M2); `references/dotnet/00` §Explicit exclusions.
