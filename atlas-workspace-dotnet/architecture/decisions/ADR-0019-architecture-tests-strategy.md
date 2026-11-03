# ADR-0019 — Estratégia de testes de arquitetura (.NET)

**Status:** PROPOSED · **Data:** 2026-10-28

## Contexto
O contrato (§18.4) exige testes automatizados de fronteiras: Domain sem frameworks; Control sem adapters owners; sem ciclos; contratos do pacote aprovado. Precisamos escolher ferramenta/abordagem.

## Alternativas
1. **NetArchTest.Rules (se licença/estabilidade confirmadas) com suíte por repo; fallback: testes próprios por reflection sobre o grafo de assemblies.** (proposta)
2. ArchUnitNET — equivalente; escolha final por licença/SKU compatível no M1 (critério igual).
3. Apenas code review — rejeitado: não é gate automatizado.

## Decisão proposta
- Suite `tests/Architecture` em cada repo com regras: (a) `Atlas.*.Domain` não referencia ASP.NET/Mongo/EF/MAF/Kafka; (b) Application não referencia Infrastructure; (c) Control não referencia adapters/Mongo owners; (d) sem ciclos entre módulos (DAG); (e) contratos wire só do pacote versionado.
- A escolha NetArchTest×ArchUnitNET×reflection é verificada no M1 com licença e suporte net10.0; resultado registrado aqui (SUPERSEDE se necessário).

## Falhas/rollback
Regra falsamente verde → revisar prefixo/escaneamento; manter testes-canário (classe violadora proposital em ambiente de teste) para validar a própria suite.
