# ADR-0012 — Estratégia de dependências: mediator/messaging/test libs e licenças

**Status:** ACCEPTED (NOT_ADOPTED — resolvida no M4, 2026-11-02) · **Data:** 2026-10-27 · **Decisão:** NENHUMA das três bibliotecas foi adotada. Dispatch em memória manual (handlers diretos nos módulos — sem MediatR); mensageria com Confluent.Kafka nativo + RabbitMQ.Client nativo virá no M6 (sem MassTransit); testes com xUnit nativo (sem FluentAssertions). Motivo: licenças comerciais (McMaster/Stack Overflow) conflitam com uso comercial prospectivo do Atlas; os ganhos ergonômicos não justificam o custo nem o risco de auditoria de licença. Revisar SOMENTE se o proprietário adquirir licenças.

## Contexto
O contrato exige handlers explícitos por padrão e permite bibliotecas (MediatR, MassTransit, Wolverine, FluentAssertions…) apenas após verificação de licença/benefício/compatibilidade (ADR condicional). Nenhuma lib deve substituir Kafka/RabbitMQ "por conveniência".

## Alternativas
1. **Baseline sem mediator nem messaging-lib: handlers explícitos por DI, Confluent.Kafka + RabbitMQ.Client diretos; libs de teste xUnit + NSubstitute/Moq; FluentAssertions somente após licença** (proposta).
2. MediatR por padrão — rejeitado por enquanto: custo de licença/dependência sem evidência de ganho (o contrato permite reavaliar).
3. MassTransit/Wolverine como abstração de mensageria — rejeitado por enquanto: os SDKs nativos + workers próprios cobrem o escopo sem acoplar semânticas.

## Decisão proposta
- Handlers de comando/consulta como classes resolvidas por DI (composition root), sem dispatcher genérico até que a duplicação demonstre custo real.
- Testes: xUnit (v3 se validada), `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers; assertion library nativa até veredito de licença.
- Cada adoção futura de lib exige entrada nesta ADR (SUPERSEDE) com licença, versão, CVE e razão; `docs/dependency-matrix.md` obrigatório no M1.

## Trade-offs
(+) menos dependências e riscos de licença; código explícito. (−) boilerplate controlado de handlers; sem abstração mágica de retry (implementar nos workers com policy própria).

## Modos de falha / rollback
Falha: duplicação de handlers virar dor → reabrir ADR com evidência. Rollback: lib pode ser removida isoladamente por serviço.

## Fontes
mestre §5.2, §6.1.9; `references/dotnet/04` §License investigation.
