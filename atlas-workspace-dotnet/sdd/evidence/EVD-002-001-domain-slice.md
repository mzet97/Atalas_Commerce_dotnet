# EVD-002-001 — SPEC-002: fatia de domínio Catalog/Offers/Inventory (sem containers)

**Data UTC:** 2026-10-28/29 · **Repo:** atlas-storefront-dotnet · **Commit:** `b0a5367` (branch main) · **Resultado:** PASS (escopo: domínio puro + testes)

## Comandos executados e saídas

```text
$ dotnet build Atlas.Storefront.slnx
dotnet build: 9 projects, 0 errors, 0 warnings

$ dotnet test (por suíte):
  Atlas.Catalog.Domain.Tests    → Passed! 11/11   (VOs + UC-005: sem título não submete; SKUs pós-publicação rejeitados; Publish exige aprovação+título+SKU; versionamento)
  Atlas.Offers.Domain.Tests     → Passed! 22/22   (Money: moedas inválidas, exponente JPY=0, bankers rounding 10.005→1000, soma de 1/3 reproduz total INV-MONEY-001, moedas mistas rejeitadas; Offer: janela/escopo, price>list rejeitado, reprice limitado, transições)
  Atlas.Inventory.Domain.Tests  → Passed! 14/14   (available=on_hand-reserved-safety; CAS por expectedVersion; ajuste negativo limitado ao disponível; liberação; UC003-T01: 10 reservas concorrentes pela última unidade → exatamente 1 OK; 50 por lotes de 2 sobre saldo 3 → 1 OK; Reservation: confirmada NÃO expira (ADR-0016), confirmar pós-TTL rejeitado, expiração só de Active vencida, ledger exige motivo+ator INV-STOCK-002)
  Atlas.Catalog.Api.Tests       → Passed!  3/3

Total no repo: 50/50 testes PASS
```

## Conteúdo implementado (domínio puro, zero dependências de framework)

- `Atlas.Catalog.Domain`: `ProductDraft` estendido (partial) — SKUs imutáveis (REQ-CAT-005: sem update/remove por design; add só pré-publicação), `Version` para concorrência otimista, `Publish(approvedBy, at)` com pré-condições do UC-005 + evento `ProductPublishedV1`.
- `Atlas.Offers.Domain` (novo): `Money` (INV-MONEY-001: minor units, ISO 4217, exponente por moeda, bankers rounding, soma reproduz total) + agregado `Offer` (escopo sku|seller|market|channel, janela de validade, price≤list, transições Draft/Active/Deactivated/Expired, reprice dentro de limites/janela) + eventos.
- `Atlas.Inventory.Domain` (novo): `StockPosition` (INV-STOCK-001 com `TryApplyReserve(qty, expectedVersion)` = regra CAS pura que o store Mongo executa condicionalmente), `StockMovement` (ledger append-only com motivo/ator obrigatórios — INV-STOCK-002), `Reservation` (INV-STOCK-003: idempotency key, confirmada não expira por TTL — ADR-0016, transições explícitas), eventos; harness in-memory espelhando o update condicional prova o mecanismo de contenção da última unidade (a autoridade real permanece o Mongo — ADR-0002).

## Correções reais durante o ciclo

CA1512 (ThrowIfNegative/OrZero), CA1859 (tipo concreto do dicionário), `Assert.ThrowsAny` (ArgumentOutOfRangeException ⊂ ArgumentException), csproj paths.

## Limitações honestas (próximos passos da SPEC-002)

Persistência MongoDB+outbox transacional, gRPC owners, CDC→Kafka, projeções PG, Gateway/BFF e vitrine **aguardam execução de containers** — agora destravada pela descoberta do **Podman rootless** (socket `/run/user/1000/podman/podman.sock` respondendo; ver EVD-000-004).
