# ATLAS COMMERCE .NET — M5 GATE REPORT

```text
DATE_UTC: 2026-11-02
REPOS / COMMITS:
  atlas-storefront-dotnet — 0a771d4 (fundação) → e3b484c (BFF) → e737a3c (E2E verde)
  atlas-workspace-dotnet  — (este commit: SPEC-005 READY + evidências + relatório)
MILESTONE: M5 / SPEC-005 — Storefront UX, busca, i18n + Playwright
STATUS: **PASS** (gate M5; pendência externa única: git push → CI)
```

## Gate M5 (contrato/roadmap) — item a item

| Critério | Evidência | Status |
|---|---|---|
| Jornadas E2E nos 3 locales | **Playwright 5/5** (chromium, produção PPR): busca→PDP→carrinho→quote→checkout→Confirmed+orderId em pt-BR/en-US/es-ES; busca vazia; redirect de locale | **PASS** |
| Preço/moeda autoritativos do Offer/Quote | PDP lê `available_offer_view` (moeda da OFERTA do mercado); quote congelada valida total no checkout; hint NUNCA confirma (INV-CQRS-001) | **PASS** |
| i18n × mercados independentes | locale ≠ mercado (facet BR/US/EU); dicionários pt-BR/en-US/es-ES + fallback CAT-01 (chave faltante nunca quebra); `Intl.NumberFormat` por locale | **PASS** |
| Web Vitals auditados | **LCP 0.05s** (budget 2.5s) · **CLS 0.036** (budget 0.1) medidos in-ciclo via PerformanceObserver | **PASS** |
| a11y auditado | **axe-core: 0 violações critical/serious** em home, carrinho e checkout | **PASS** |
| SEO técnico | metadataBase, títulos por página, `generateStaticParams` por locale, hreflang-ready (locales com rotas dedicadas) | **PASS** (sitemap/hreflang tags completos ficam para o hardening do M8) |

## O que foi construído

- **BFF (Gateway :5180)**: `/api/catalog/search` (busca + facets por mercado + paginação),
  `/api/catalog/products/{id}` (PDP: ofertas ativas + hint de disponibilidade),
  forwarder `/api/checkout/**` → Checkout.Api (mesma origem para o browser).
- **Offers.Api (:5293)** — o dono de preço ganhou superfície (dev seed) e o pipeline
  oferta→outbox→Kafka→PG voltou a projetar (bug do dispatcher multi-db resolvido no M4).
- **Vitrine Next 16 (`apps/storefront-next`)**: `[lang]` como raiz + `proxy.ts`
  (redirect Accept-Language), **cacheComponents/PPR** com leituras dinâmicas em
  Suspense, carrinho localStorage, quote congelada (TTL), checkout com três
  desfechos (Confirmed / Pending-reconciliação / Rejected), i18n completo e
  catálogo seedado **pelo pipeline real** (draft→publish→outbox→Kafka→PG).

## Bugs reais do ciclo (5)

1. **fetch client-side cross-origin** (vitrine :3000 → BFF :5180) sem CORS — quote
   falhava só no browser. Fix: rewrites same-origin no next.config.
2. **Rotas YARP do Gateway nunca casaram** (M2 usava endpoint direto; latente até
   hoje). Fix: forwarder explícito na borda.
3. **Race de clique na lista velha** durante stream da busca — o E2E clicava o item
   errado. Fix: filter(hasText) + waitForURL.
4. **Next 16 cacheComponents**: fetch sem Suspense = erro de prerender; `params`
   são Promise — páginas reestruturadas (params/searchParams descem para dentro
   do Suspense).
5. **i18n.ts com chaves duplicadas** após múltiplos patches — reescrita canônica com
   verificação automatizada "toda chave usada existe".

## TESTS EXECUTED (M5)

```text
Storefront .NET: 141/141 (17 projetos) mantidos
Next (tsc): 0 erros · build de produção 19/19 rotas (PPR)
Playwright: 9/9 — compra×3 locales + vazio + redirect + a11y×3 + CWV budget
BFF curl smokes: search/facets, PDP ofertas, quote e checkout APPROVED via forwarder
PERFORMANCE: CWV medidos na vitrine (LCP/CLS); benchmark L0-L4 continua M9
```

## NOT IMPLEMENTED / DEFERRED (honesto)

- sitemap.xml/hreflang tags explícitos e OG images — M8 (hardening SEO)
- busca com sinônimos/ordenação por relevância real (hoje ILIKE + facets de mercado)
- autenticação do comprador na vitrine (guest checkout atual; login Keycloak em M6+)
- CWV de campo (CrUX/RUM) — medidos aqui são de laboratório
- EN portfolio docs — M11

## NEXT EXECUTABLE TASK

**M6/SPEC-006 — Fulfillment, Returns/Refunds, Notifications (RabbitMQ)**. Pendência
externa persiste: git push (CI).
