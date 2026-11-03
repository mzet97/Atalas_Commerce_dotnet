# EVD-002-004 — Owner gRPC do Catalog + views M2 completas

**Data UTC:** 2026-10-29 · **Repos/commits:** contracts `00a1f1f` · storefront `0923ed6` + `778501b` · **Resultado:** PASS

## 1) Contracts — SDK gRPC (ADR-0005)

- `Atlas.Contracts.Grpc` (Grpc.Tools 2.85 sobre os `.proto` canônicos; `buf lint` PASS — proto **aditivo** `SubmitForApproval` registrado como evolução UC-005, não-quebrante).
- Referência cross-repo (Atlas.Contracts.Grpc ← Catalog.Api): caminho relativo de **5 níveis** (4 caía dentro do storefront — MSB9008 no diagnóstico). Substituída por pacote NuGet versionado no release (nota no csproj).

## 2) Owner gRPC do Catalog (Catalog.Api)

`CatalogOwnerGrpcService`: UpsertDraft · **SubmitForApproval** · PublishProduct · GetProduct, delegando aos handlers/store já testados. Owner revalida no DOMÍNIO (INV-SEC-001); `approval_token` validado como presença na borda — **validação criptográfica chega com a fatia four-eyes do Control** (registrado).

Teste REAL in-memory (canal HTTP/2 sobre WebApplicationFactory, Mongo isolado por execução) — 4/4 PASS:

```text
1) UpsertDraft → accepted, version persistida = 1 (insert inicial; increments de construção não contam)
2) Publish sem Submit → negado pelo DOMÍNIO: "Publicação exige aprovação prévia (estado atual: Draft)"
3) SubmitForApproval → accepted (v2)
4a) Publish SEM token → FailedPrecondition (borda do owner)
4b) Publish com version stale → conflito, sem evento
5) Publish correto → accepted + EventId; outbox = 1 (exatamente um evento)
6) GetProduct → status Published, 1 SKU
```

## 3) Views M2 completas (E2E real Kafka+PG)

| View | Projector | Evento | Prova E2E |
|---|---|---|---|
| `catalog_product_search` | CatalogProjector | Product.PublishedV1 | ciclo anterior (EVD-002-003) |
| `available_offer_view` | **OffersProjector** (novo) | Offer.PublishedV1 | publish → linha Active (sku/market/price) · reentrega dedup · stale guard por version |
| `inventory_admin_view` | **InventoryProjector** (novo) | Stock.ReservedV1 (DELTAS) | delta aplicado 1× (reentrega sem efeito — dedup vem ANTES da aplicação do delta) |

**Suíte do storefront: 66 testes PASS** (4 gRPC + 2 E2E pipeline incluídos).

## Fatos do ciclo

- Ambiguidade `LocalizedText` (Domain × proto) → alias de namespace no serviço.
- `SkuDraft.sku_code` → propriedade gerada `SkuCode` (não `Code`).
- DI: port `ICatalogStore` precisa registro explícito (singleton concreto não satisfaz a interface).
- Npgsql: reader aberto bloqueia próximo comando na mesma conexão → `using` nos readers.

## Limitações honestas

- approval_token = presença (four-eyes criptográfico pendente — Control).
- Vitrine/BFF ainda não leem as views (próxima fatia do M2).
- Referência cross-repo enquanto não houver release do pacote de contratos.
