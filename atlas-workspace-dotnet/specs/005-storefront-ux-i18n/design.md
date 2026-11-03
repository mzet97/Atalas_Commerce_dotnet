# SPEC-005 — design.md

- **Busca:** query service no Gateway/BFF lendo `catalog_product_search` (GIN pg_trgm/tsvector por locale); facets agregadas por JSONB; cache Redis por query+locale+market com invalidação por `ProductPublished/Updated`; `SearchObserved` → `funnel_metrics_hourly`.
- **i18n (Next.js App Router):** dicionários por namespace `/{locale}/dictionaries/*`; middleware de locale com cookie + path; catálogo localizado do PIM (`LocaleText`); fallback explícito componente `<LocalizedText fallback>`; datas/números via `Intl` com moeda do market.
- **Rotas/SEO:** estrutura `/{locale}/{market}/c/{category}`, `/p/{slug}`; `generateMetadata` com hreflang (pt-BR/en-US/es-ES + x-default), canonical por market; `app/sitemap.ts` dinâmico paginado; robots.
- **A11y/Perf:** design system com componentes acessíveis; axe-core no CI Playwright; imagens `next/image` com prioridade LCP; fontes otimizadas; orçamentos CWV no CI (Lighthouse CI) com ambiente documentado.
- **Estados de UX:** hooks `useAsyncResource` padronizando loading/empty/error/stale/partial; offline via service worker básico (leitura stale permitida, checkout nunca stale).

## Testes
E2E Playwright trilíngue (mesma jornada nos 3 locales, moedas por market), a11y (axe), SEO (validador de hreflang/canonical/sitemap), Lighthouse CI com orçamentos.
