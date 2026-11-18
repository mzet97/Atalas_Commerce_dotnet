# SDD — Roadmap (M0–M11)

Gates conforme mestre §12 e `references/dotnet/03`. Dependências: M0→M1→M2→M3; M4/M5 parcialmente paralelos; M6 após M3; M7 após M2/M3; M8 após baseline; M9 após recovery; M10 opcional; M11 transversal.

> Estado sincronizado com os gate reports em 2026-11-16 (F1 higiene SDD). Fonte de verdade por milestone: `sdd/reports/*-gate-report.md`.

| M | SPEC | Objetivo | Gate (evidência mínima) | Status |
|---|---|---|---|---|
| M0 | SPEC-000 (business-discovery) | Recon/auditoria/modelagem/ADRs PROPOSED/SPEC-001 proposta | Fontes 32/32 verificadas; invariantes rastreadas; ≥5 cenários críticos; diagramas consistentes; relatório M0 | **PASS** (M0-report.md) |
| M1 | SPEC-001 (platform-foundation) | 3 repos + templates + CI + Compose core/data + Keycloak + OTel | 1 serviço + host admin build/test/health; auth real smoke; trace cross-process; CI verde; rebuild determinístico | **PASS** (2026-10-29, M1-gate-report.md; CI live verde 2026-11-04) |
| M2 | SPEC-002 (catalog-offers-inventory) | 1º slice: Admin→Control→owners→Mongo/outbox→CDC→Kafka→PG→Redis→Next | Produto publicado pelo caminho real; 403 sem scope + audit; evento repetido não duplica; contenda última unidade = 1 venda | **PASS** (2026-10-29, M2-gate-report.md) |
| M3 | SPEC-003 (checkout-saga) | Cart, Quote, Checkout saga, Order, PSP fake | E2E local; oversell=0; duplicate_paid_order=0; T01–T05 cobertos; traces + vídeo Playwright | **PASS** (2026-11-02, M3-gate-report.md; 141/141) |
| M4 | SPEC-004 (admin-hardening) | Four-eyes, scopes, auditoria imutável, lag CQRS visível | Self-approval negado+auditado; revogações avaliadas no owner | **PASS** (2026-11-02, M4-gate-report.md) |
| M5 | SPEC-005 (storefront-ux-i18n) | Busca/facets, UX, SEO, i18n 3 idiomas/mercados | E2E 3 idiomas; CWV/a11y medidos com limitações explícitas | **PASS** (2026-11-02, M5-gate-report.md) |
| M6 | SPEC-006 (fulfillment-returns-notifications) | Picking/shipment/tracking fake, returns/refunds, Rabbit notifications | Refund coerente; DLQ/retry provados; notificações não duplicadas | **PASS** (2026-11-03, M6-gate-report.md) |
| M7 | SPEC-007 (maf-shopping-assistant) | MAF C#, tools read-only, consent, grounding, eval, fallback | Smoke MAF pinado; injection não amplia; consent inviolável; fallback OK | **PASS núcleo** (2026-11-03, M7-gate-report.md; F2/dataset/custo → SPEC-007+) |
| M8 | SPEC-008 (k8s-operacional) | Helm/Skaffold, políticas, HPA/KEDA, backups, DR | Rollout/rollback; restore drill; testes de falha com evidência | **PASS PARCIAL** (charts lint+template OK; runtime bloqueado por ambiente → M11) |
| M9 | SPEC-009 (benchmark-escala) | Planos L0–L4, chaos/soak, custo | Relatório reproduzível (ambiente/carga/percentis/erros); limitações honestas | **IN_PROGRESS** (L0 real com desvio medido; L1 TODO; L2 bloqueado p/ cluster) |
| M10 | SPEC-010 (marketplace-3p) | Sellers/tenancy/split/settlement | **DEFERRED por padrão**; só por ADR + demanda | DEFERRED |
| M11 | SPEC-011 (portfolio-internacional) | Docs EN, demo, postmortems, trade-off essays | Clone→run→verify por avaliador externo; matriz projetado×implementado×medido | DRAFT |

## Próximos passos imediatos

1. Higiene SDD F1 (2026-11-16): ADR-0009 ACCEPTED; decisions-open/roadmap/progress sincronizados; EVD-006-001 §5 (runbook DLQ) recuperado.
2. Fechar L1 soak + L2 no cluster `.51` (Fase 4 do plano de deploy) → M9.
3. Fatias curtas: 9 login buyer, 14 Redis cache, 11/12 F2+métricas, 2 lease, 3 captura, 8 sitemap/hreflang.
4. M11 clone→run→verify contra o `.51` → 100% SDD (M10 segue DEFERRED).

## Slices de release

Cada milestone entrega vertical observável: M2 publica um produto; M3 compra de ponta a ponta; M4 administra com segregação; M5 internacionaliza; M6 opera pós-venda; M7 assiste a compra; M8 opera em K8s; M9 mede; M11 apresenta.
