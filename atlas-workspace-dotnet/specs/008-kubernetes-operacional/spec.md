# SPEC-008 — spec.md

**Problema:** levar a pilha local (Compose) a um cluster Kubernetes on-prem de demonstração com segurança, escalonamento e recuperabilidade verificáveis — sem fingir HA com pods únicos.

**Atores:** SRE/operação; cluster on-prem (kubeadm/k3s a definir pelo ambiente).

## Requisitos

1. **Charts Helm versionados** por repo (`helm create` como base) com values por ambiente; `helm lint` + `helm template` no CI; Skaffold para dev.
2. **Namespaces e políticas:** `atlas-apps/workers/data/messaging/security/observability`; NetworkPolicy deny-by-default com exceções explícitas; quotas/limits; PDB; topology spread.
3. **Workloads:** Deployments com probes (readiness/liveness/startup), graceful shutdown, resources requests/limits, non-root, seccomp básico; StatefulSets/operadores para Mongo/PG/Kafka/Rabbit quando disponíveis (ou plano documentado de PV+backup).
4. **Escala:** HPA por CPU/memória; KEDA por consumer lag (workers); limites documentados (partições, serialização de SKU quente).
5. **Secrets/config:** env separation; ExternalSecrets/Vault ou sealed-secrets; zero segredo em Git.
6. **Resiliência de dados:** backups agendados (Mongo dump/PG basebackup) e **restore drill** verificado com checkout funcional pós-restore.
7. **Rollout/rollback:** estratégia rolling update; rollback verificado em evidência; GitOps (Argo CD) opcional por ADR.
8. **TLS/ingress:** Ingress/Gateway API com TLS interno; mTLS interno onde aplicável.

## Critérios de aceite
Demo implantada em cluster não-prod; réplicas escalando sob carga sintética; rollback executado; restore drill com pedido verificável pós-restore; observabilidade do próprio cluster ativa.

## DoD
EVD com comandos helm/kubectl, saídas de rollout/rollback/restore, dashboards ativos; runbooks de operação reais; limitações honestas (o que NÃO é HA).
