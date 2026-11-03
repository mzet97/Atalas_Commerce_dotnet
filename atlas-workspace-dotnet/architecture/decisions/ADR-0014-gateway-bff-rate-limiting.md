# ADR-0014 — API Gateway/BFF e rate limiting

**Status:** PROPOSED · **Data:** 2026-10-27

## Contexto
Next.js precisa de uma borda HTTP/JSON coesa; o contrato permite YARP/BFF "somente após ADR". Exposição direta de cada microserviço ao browser multiplica CORS, auth e rate limiting.

## Alternativas
1. **BFF/gateway com YARP introduzido no M2 apenas para os serviços existentes, com auth de borda (OIDC), rate limiting e correlation; não é owner de regra de negócio** (proposta).
2. Sem gateway (frontend fala com cada serviço) — mais superfícies para expor, policies duplicadas, e o contrato de borda vira difuso.
3. Gateway completo com lógica de agregação de negócio — proibido: regra de negócio fica nos owners; gateway não vira BFF gordo.

## Decisão proposta
- `src/Gateway/` no repo Storefront: roteamento YARP (ou Minimal API reverse proxy), validação de token de borda, rate limit por IP/subject/rota, problem details uniformes, correlation/trace propagation, OpenAPI agregada.
- Agregação de leitura mínima permitida (BFF pattern) sem decidir preço/estoque; comandos sempre destinados ao owner.
- Rate limiting também nos serviços internos (defesa em profundidade).

## Trade-offs
(+) borda única, políticas coerentes; frontend simples. (−) um hop a mais e um componente para operar.

## Modos de falha / rollback
Falha: gateway down → storefront degradado (página de status), sem afetar admin (borda própria do Control). Rollback: rotas podem apontar direto aos serviços em ambiente local.

## Fontes
mestre §5.1 (Gateway/BFF após ADR), §5.3; blueprint #06 §3 (HTTP/JSON de borda).
