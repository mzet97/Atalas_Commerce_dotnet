# EVD-003-005 — Cart e PriceQuote persistidos no replica set (stores Mongo reais)

**Data UTC:** 2026-10-29 · **Commit:** `07e20c7` · **Resultado:** PASS (2/2 integração)

## Testes (replica set real, db por execução)

```text
Cart_upsert_por_owner_e_reidratação_completa:
  upsert por owner_id; 2 linhas reidratadas com quantidades corretas
Quote_salvo_e_recuperado_com_TTL_e_linhas:
  TTL, linhas e total corretos; IsValidAt vigente/expirado
Passed! 2/2
```

## Fatos

- CartDoc upsert por `OwnerId` (um carrinho por cliente/guest); reidratação via `Cart.Hydrate`.
- QuoteDoc com `ExpiresAtUtc` persistido; `IsValidAt` usa clock do servidor.

## Limitações

- Quote sem criptografia de breakdown (M4+); Cart sem TTL de limpeza automática (política de retenção na SPEC-006).
