# EVD-008-002 — M8: deploy declarado no cluster k8s com namespace dotnet-atlas (runtime Pending — honesto)

**Data UTC:** 2026-11-03 · **Cluster:** k3s v1.31.5 do servidor local (compartilhado com o projeto
Python — orientação do proprietário: recursos Atlas com **dotnet** no nome) · **kubectl v1.37.2**

## Aplicado (namespace dotnet-atlas)

- **12 Deployments** dotnet-atlas-*: gateway(2), checkout-api(2), control-host(2), assistant,
  catalog-api, offers-api, inventory-api, payments-api, fulfillment, notifications-worker,
  projections-worker, control-web
- Services, HPAs (gateway/checkout/control-host CPU 70%), NetworkPolicies deny-by-default,
  Ingress TLS atlas.local → gateway

## Estado REAL (honesto)

Node `amdr7` **NotReady** — container runtime do k3s derrubado pela MESMA limitação de ambiente
(cpuset cgroup v2 não delegado no distrobox; o k3s roda como usuário no distrobox). Consequência:
todos os pods **Pending** até o k3s rodar onde cgroups permitam (ex.: systemd nativo no host ou
o servidor real). Nada do projeto Python está em execução também (7 pods Pending).

## O que o M11 (clone→run→verify) ganha pronto

Manifestos versionados + namespace criado + aplicação idempotente validada:
`helm template → kubectl apply` com prefixo dotnet; quando o node ficar Ready os pods
sobe com todos os hardenings já aplicados (probes, NP, HPAs, securityContext).
