# ADR-0005 — gRPC schema-first (.proto + Buf) + service identity

**Status:** PROPOSED · **Data:** 2026-10-27

## Contexto
Chamadas internas síncronas (Admin→owner, Checkout→Pricing/Inventory) precisam de contrato estável, deadlines e autenticação de workload. Portfólio pretende compatibilidade wire e uso polyglot futuro.

## Alternativas
1. **Protobuf schema-first com Buf (lint + breaking) e clients gerados** (proposta do contrato).
2. code-first gRPC (contratos derivados de C#) — rejeitada como fonte única: reflexão não é contrato versionado polyglot.
3. HTTP/JSON interno para tudo — verboso para comandos internos frequentes; sem deadlines tipados nativos.

## Decisão proposta
- `.proto` canônicos no repo `atlas-platform-contracts-dotnet`; geração com `Grpc.Tools`/Buf; pacote versionado (semver) consumido pelos serviços.
- CI: `buf lint` + `buf breaking` contra N-1; testes de contrato N/N-1.
- mTLS/identidade de workload entre serviços; deadlines explícitos; retry apenas quando idempotente; authorization revalidada no owner.

## Trade-offs
(+) contratos explícitos e evolutivos; perf e tipagem fortes. (−) toolchain Buf adicional (ausente no ambiente hoje — ver audit); dois formatos de contrato para manter (proto + OpenAPI de borda).

## Modos de falha / rollback
Falha: breaking change não detectado → gate CI bloqueia merge. Rollback: contratos versionados permitem dupla publicação N/N-1.

## Fontes
mestre §4.2, §5.3, §6.1.6; `references/dotnet/01` §3.
