# EVD-008-001 — M8: charts Helm validados (32+5 recursos), ingress, KEDA, backup/restore-drill

**Data UTC:** 2026-11-03 · **Commits:** contracts `4b3922b`/`f6ab650`-related · **Ferramentas:** helm v3.16.4, kubectl v1.37.2 (instalados)

## Deliverables (contracts repo: infrastructure/charts + k8s + ops)

| Artefato | Validação |
|---|---|
| charts/atlas-storefront (10 serviços) | `helm lint` 0 failed · `helm template` = **32 recursos**: 10 Deployment + 10 Service + 10 NetworkPolicy + 2 HPA (gateway+checkout) |
| charts/atlas-control (host+web) | `helm lint` ok · template = 5 recursos (2 Deployment+2 Service+1 HPA) |
| k8s/ingress-storefront.yaml | nginx TLS atlas.local → gateway |
| k8s/keda-scalers.yaml | Kafka consumer-lag (projections) + RabbitMQ queue-length (notifications) |
| ops/backup.sh + ops/restore-drill.sh | pg_dump custom + mongodump; restore em *_restore com contagem de pedidos |

Hardening nos charts: runAsNonRoot 1654, seccomp RuntimeDefault, drop ALL caps, probes /health, resources requests/limits, NetworkPolicy deny-by-default com exceção explícita gateway→apis.

## DESVIO DE AMBIENTE (honesto e evidenciado)

**Cluster executável indisponível neste ambiente:** kind exige Docker/Podman CLI (ausente no
distrobox — EVD M1); k3s em container rootless falha (pasta sem DNS inter-container); k3s binário
direto (root no distrobox) fatal em **"failed to find cpuset cgroup (v2)"** — o namespace de
cgroup do distrobox expõe apenas `cpu io memory pids` (sem cpuset; write em subtree_control →
ENOENT). Execução runtime (pods reais, HPA escalando, rollout/rollback live, restore drill
executado) fica para o **clone→run→verify do avaliador (M11)** nos charts versionados.
Todos os artefatos passam validação client-side (lint+template) e os scripts de operação
estão prontos para execução em hardware com Kubernetes real.
