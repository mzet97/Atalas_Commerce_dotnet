# EVD-002-002 — Inventory no MongoDB real: CAS, idempotência e outbox transacional

**Data UTC:** 2026-10-29 · **Repo:** atlas-storefront-dotnet · **Commit:** `cbba8c9` · **Resultado:** PASS (5/5 integração; 55/55 no repo)

## Ambiente

Replica set **rs0 PRIMARY** local (mongo:7.0.14 via Podman, EVD-001-006), conexão `mongodb://127.0.0.1:27017/?directConnection=true`; banco `atlas_it_{guid}` por execução (drop no fim). MongoDB.Driver **3.13.0**; CVE scan: zero (política NU1902).

## Testes executados (saídas reais)

```text
$ dotnet test tests/Atlas.Inventory.IntegrationTests --no-build
Passed! 5/5

 1) Última_unidade_sob_10_reservas_concorrentes_permite_exatamente_uma   ← UC003-T01 / INV-STOCK-001
    granted=1 denied=9 | posição final: onHand=1 reserved=1 version=2 | outbox=1 (Stock.ReservedV1)
    | movements=1 (ledger do vencedor) — OVERSELL ZERO com CAS real (update condicional
    $expr: OnHand-Reserved-SafetyStock >= qty, atômico no servidor, dentro de transação)

 2) Mesma_idempotency_key_retorna_a_mesma_reserva_sem_reduzir_duas_vezes ← INV-STOCK-003
    2ª chamada → DuplicateKey com o MESMO ReservationId; reserved=2 (uma única vez); outbox=1

 3) Mesma_chave_em_coração_elege_um_vencedor_pelo_índice_único
    6 Tasks simultâneas, mesma key → 1 Granted + 5 Duplicate; todos com a MESMA reserva
    (catch MongoWriteException 11000 → re-busca por idempotência)

 4) Multi_linha_all_or_nothing_transação_abortada_não_persiste_nada      ← ADR-0003
    linha1 ok + linha2 sem saldo → OutOfStock; NADA persistido (reserved=0, sem reserva,
    sem outbox) — abort desfez a linha bem-sucedida

 5) Confirmar_e_liberar_devolvem_saldo_com_ledger_e_evento               ← INV-STOCK-002
    reserve→confirm(order-99)→release: reserved volta a 0; movements=2 (reserve+release);
    outbox=2 (Stock.ReservedV1 + Reservation.ReleasedV1)
```

## Implementação (resumo)

- `Atlas.Inventory.Application`: `ReserveStockHandler` (idempotência primeiro) + port `IInventoryStore`; resultados tipados (`Granted/DuplicateKey/OutOfStock`) — sem exceção como fluxo de negócio.
- `Atlas.Inventory.Infrastructure/MongoInventoryStore`: update condicional via `$expr` (`$subtract/$gte` — LINQ3 não traduz aritmética), reserva com **índice único** por idempotency key, ledger `inventory.movements` e **outbox `inventory.outbox` na MESMA transação** (ADR-0002/0003); `Reservation.Hydrate` no domínio para reconstrução.

## Fatos de engenharia registrados (diagnóstico real durante o ciclo)

1. Driver 3.x: `Id` serializa como **`_id`** — filtro raw com "Id" nunca casava (diagnóstico com snapshot revelou; corrigido para `_id`).
2. Guid exige representação explícita (3.x) → `[BsonRepresentation(BsonType.String)]`.
3. Overloads com sessão exigem `UpdateOptions` posicional → `cancellationToken:` nomeado.
4. Probe mongosh confirmou o `$expr` funcional no servidor antes do fix C#.

## Limitações honestas

- Gaps do M2 ainda abertos: gRPC owners + projeções PG/Kafka + Gateway/BFF + vitrine (TASK-002-005..011); OIDC 403 por escopo no Control (TASK-002-005).
- Concorrência dos testes via Tasks (10-way real no runtime; o serializador do CAS é o servidor Mongo).
