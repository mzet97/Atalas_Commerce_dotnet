# ADR-0010 — .NET Aspire (opcional, DX) vs Compose + Skaffold/Helm

**Status:** ACCEPTED (REJECTED para Aspire — resolvida no M4, 2026-11-02) · **Data:** 2026-10-27 · **Decisão:** Aspire NÃO é adotado (nem como DX opcional). Compose declarativo + Podman rootless provados do M1 ao M4 (runbook README-podman.md); Helm/K8s entram no M8. Aspire adicionaria um orquestrador proprietário em cima de ferramentas já provadas sem ganho de produção (o dashboard é DX local apenas). Revisitável só por nova ADR com evidência.

## Contexto
O contrato exige Docker Compose como ambiente local/demo obrigatório e Helm/K8s como alvo de produção; Aspire é permitido apenas como composição de desenvolvimento, sem ser deploy.

## Alternativas
1. **Compose obrigatório (profiles core/data/messaging/observability/ai/simulated-external) + Helm/Skaffold para K8s; Aspire opcional por time/serviço como DX** (proposta).
2. Aspire como orquestrador principal — rejeitado: AppHost não é deployment K8s e o dashboard Aspire não substitui Grafana.
3. Somente Compose, sem K8s — contraria os gates M8 (rollout/rollback, restore drill).

## Decisão proposta
- Compose: fonte de verdade local com profiles progressivos e versões mínimas (Mongo replica set, broker local); zero obrigação de iniciar tudo.
- K8s: Helm charts versionados (`helm create` como base), Skaffold para dev, HPA/KEDA, probes, PDB, network policies.
- Aspire: adotar somente quando aportar valor mensurável de DX em serviços novos; nunca dependência obrigatória dos pipelines.

## Trade-offs
(+) local reproduzível e K8s-alvo claro. (−) manter dois/de três modos de orquestração; Aspire opcional pode gerar inconsistência se adotado sem critério.

## Modos de falha / rollback
Falha: divergência Compose×Helm → garantir paridade via variáveis/imagens pinadas e testes de smoke em ambos. Rollback: remover Aspire não afeta Compose/Helm.

## Fontes
mestre §5.4, §6.1.8; `references/dotnet/01` §6; `references/dotnet/04` (Aspire CLI).
