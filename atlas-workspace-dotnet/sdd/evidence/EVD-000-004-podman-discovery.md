# EVD-000-004 — Resolução da divergência de containers: Podman rootless no host

**Data UTC:** 2026-10-29 · **Resultado:** AMBIENTE DE CONTAINERS CONFIRMADO (Podman, não Docker)

## Histórico da divergência

O proprietário informou (2026-10-28) "docker e docker compose estão no host". Verificações em 2026-10-27/28 não encontraram o binário `docker` (PATH//usr/bin//usr/local//opt//snap) e EVD-000-003 registrou a divergência com gates M1 BLOCKED. Em 2026-10-29 o proprietário esclareceu: **"o docker na verdade é podman no host"**.

## Verificação real executada (2026-10-29)

```text
$ ls /run/user/1000/podman/podman.sock
/run/user/1000/podman/podman.sock  (socket rootless EXISTENTE)

$ curl -s --unix-socket /run/user/1000/podman/podman.sock http://d/v4.0.0/libpod/info
{"host":{"arch":"amd64","buildahVersion":"1.43.5","cgroupManager":"systemd","cgroupVersion":"v2",
 "conmon":{"package":"conmon-2.2.1-2.fc44.x86_64","path":"/usr/bin/conmon"}, "cpus":24, ...}}

$ which podman / ls /usr/bin/podman* / rpm -q podman
binário podman NÃO visível neste sandbox de shell (comando `rpm` também sem acesso a db do host)
```

## Interpretação

- **Podman rootless está operacional no host** (serviço de usuário ativo; API libpod v4 respondendo com dados reais do host: fc44, 24 CPUs, cgroups v2/systemd, buildah 1.43.5).
- O binário `podman` e o `docker` CLI não são acessíveis no sandbox do agente; **a automação usará a API REST do Podman via socket unix** (curl) — camada compatível Docker — mantendo o `docker-compose.yml` como fonte declarativa (paridade Compose, ADR-0010).
- Testcontainers for .NET poderá apontar `DOCKER_HOST=unix:///run/user/1000/podman/podman.sock`.

## Impacto nos bloqueios

- TASK-001-007/008/009 (Compose core+data, OIDC smoke, OTel cross-process): **DESTRavadas via API Podman** — execução a seguir, com evidências.
- EVD-000-003 fica como registro histórico da divergência (Docker CLI ausente = fato; containers = Podman = fato).
