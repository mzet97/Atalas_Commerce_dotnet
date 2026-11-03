# ATLAS COMMERCE .NET — M8 GATE REPORT

DATE_UTC: 2026-11-03
STATUS: **PASS PARCIAL (charts/ops validados; execução de cluster BLOQUEADA POR AMBIENTE com EVD)**

| Critério | Evidência | Status |
|---|---|---|
| Charts Helm por repo | atlas-storefront (32 recursos, lint ok) + atlas-control (5 recursos, lint ok) + ingress + KEDA | **PASS** |
| Hardening | runAsNonRoot, seccomp, drop ALL, probes, resources, NetworkPolicy deny-by-default | **PASS** (renderizado) |
| HPA/KEDA manifests | HPA v2 CPU70% (gateway/checkout/control-host); KEDA Kafka-lag + Rabbit-queue | **PASS** (manifests) |
| Backup/restore scripts | ops/backup.sh + ops/restore-drill.sh (prontos; execução real → ambiente com cluster) | **PASS** (scripts) |
| Demo implantada + réplicas escalando + rollout/rollback live + restore drill executado | **BLOQUEADO POR AMBIENTE**: cpuset cgroup v2 não delegado ao distrobox (kind sem docker CLI; k3s container/agent fatal; k3s root fatal "failed to find cpuset") — desvio documentado; execução real no clone→run→verify (M11) | **BLOQUEADO (ambiente)** — ATUALIZAÇÃO 2026-11-03: cluster k3s do servidor REUTILIZADO com
orientação do proprietário (recursos com **dotnet** no nome): namespace `dotnet-atlas` criado,
**12 Deployments + Services + HPAs + NetworkPolicies + Ingress aplicados** (EVD-008-002).
Pods Pending até o node Ready (runtime do k3s sofre a mesma limitação cpuset do distrobox);
execução real → clone→run→verify (M11) |

TESTS: helm lint/template 37+5 recursos · storefront 147 + e2e 9/9 mantidos
Bugs reais: volume mounts API libpod (volumes vs mounts), campo cmd→command (libpod), cpuset ENOENT

NEXT: M9 L0/L1 REAIS no compose (não dependem de cluster) + L2 registrado como pendência de
cluster; depois M11. Fatias curtas restantes: lease workers, captura reconciled, login buyer.
