# SPEC-005 — evidence.md (M5 executado — 2026-11-02)

## E2E Playwright (tests/e2e, chromium headless — vitrine :3000 produção, PPR)
- compra completa pt-BR/en-US/es-ES: busca→PDP→carrinho→quote→checkout→**Confirmed + orderId** (5/5 ✓)
- busca sem resultado → estado vazio ✓ · raiz → redirect locale (proxy) ✓
- axe a11y: 0 violações critical/serious em home/carrinho/checkout ✓
- CWV medidos IN-CICLO: LCP 0.05s (budget 2.5s) · CLS 0.036 (budget 0.1) ✓

## Bugs reais do ciclo
1. fetch client-side cross-origin (:3000→:5180) sem CORS → rewrites same-origin no next.config
2. race: clique na lista velha antes do stream → filter(hasText) + waitForURL produto
3. Next 16 cacheComponents: fetch sem Suspense = build error → páginas dinâmicas com
   params/searchParams como PROMISES dentro de Suspense (home/PDP)
4. rotas YARP do gateway nunca casaram (M2 era endpoint direto) → forwarder explícito /api/checkout/**
5. dispatcher workers apontava 1 db só → ofertas/estoque nunca projetavam (resolvido antes, achado aqui)
