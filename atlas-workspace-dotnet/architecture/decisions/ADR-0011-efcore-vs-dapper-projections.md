# ADR-0011 — EF Core 10 vs Dapper/Npgsql nas projeções PostgreSQL

**Status:** ACCEPTED (2026-11-03 — fechada com L1 real: EVD-009-002) · **Decisão:** Dapper NÃO
adotado. Npgsql/EF direto nas views bateu p95 4,76ms SEM cache e 0,61ms com cache-aside Redis —
muito abaixo do gate 1500ms. Dapper só entraria por rota medida como gargalo (nenhuma até M9 L1).

## Contexto
Projection workers (.NET `BackgroundService`) gravam read models no PostgreSQL na mesma transação do `processed_events`; APIs de consulta leem essas projeções. A escolha de ORM afeta migrations, perf e manutenção.

## Alternativas
1. **EF Core 10 para schema/migrations das projeções + consultas; Dapper/Npgsql direto nas rotas quentes após benchmark comprovado** (proposta).
2. EF Core para tudo sem medição — risco de queries ineficientes invisíveis.
3. Dapper/Npgsql puro desde o início — migrations e mapeamento manuais para ~10+ projeções custam mais que o benefício inicial.

## Decisão proposta
- EF Core 10 dono de migrations das views/tabelas de projeção e das escritas idempotentes dos workers (upsert + `processed_events` transacional).
- Consultas quentes (PDP, busca com GIN, grade admin) podem usar Dapper/Npgsql quando benchmark demonstrar ganho; decisão por consulta, com evidência.
- Nunca usar EF Core transacionalmente com MongoDB como se houvesse ACID distribuído.

## Trade-offs
(+) migrations versionadas e produtividade; escape hatch para perf. (−) dois estilos de acesso a dados; disciplina para não deixar a decisão viciar por gosto.

## Modos de falha / rollback
Falha: projeção com drift → rebuild determinístico (ADR-0002). Rollback: leitura pode voltar a EF Core sem quebrar contratos (interno).

## Fontes
mestre §5.4, §18.1; ADR-0002.
