# SPEC-005 — spec.md

**Problema:** experiência de compra internacional de qualidade mensurável: busca/facetas rápidas, 3 locales × 3 mercados, SEO técnico e acessibilidade — com preço/moeda autoritativos do Offer/Quote (INV-I18N-001).

**Atores:** visitante e cliente nos mercados BR/US/UE; avaliador de portfólio.

## Requisitos

1. **Busca:** paginação + facets (categoria, faixa de preço, atributos) sobre `catalog_product_search` (GIN); sugestões; observabilidade de impressões sem PII (REQ-DSC-001/002).
2. **i18n:** rotas localizadas (`/pt-BR/...`, `/en-US/...`, `/es-ES/...`), traduções por namespace com fallback explícito (CAT-01: SKU sem es-ES não quebra checkout), conteúdo de catálogo localizado do PIM, Intl/ICU, UTC persistido/exibição local, RTL preparado (teste de expansibilidade).
3. **SEO:** hreflang por rota, canonical por região/market, sitemap dinâmico, metadata SSR, OpenGraph.
4. **Moeda/mercado:** seleção de market independente de locale; preço sempre da Offer do market; FX apenas indicativa quando sem offer na moeda (INV-I18N-001).
5. **A11y:** WCAG 2.2 AA como meta verificável (axe no CI + auditoria manual documentada); navegação por teclado; estados ARIA nos componentes.
6. **Performance:** CWV medidos (LCP/CLS/INP) com orçamentos declarados; imagens otimizadas; SSR/ISR adequado; cache de leitura respeitando `availability_hint ≠ saldo`.
7. **Estados de UX:** loading/empty/error/offline/stale/partial em todas as jornadas (UC-001/002/004 no front).

## Fora de escopo
Fulfillment/tracking UI detalhada (M6), assistente IA (M7), marketplace 3P.

## Critérios de aceite
E2E nos 3 idiomas e mercados (preço/moeda corretos por market); fallback CAT-01; hreflang/canonical validados; axe sem críticos; CWV dentro do orçamento com ambiente documentado; sem conversão monetária autoritativa na UI.

## DoD
EVD com relatórios (Lighthouse/axe/sitemaps), E2E verde, limitações explícitas.
