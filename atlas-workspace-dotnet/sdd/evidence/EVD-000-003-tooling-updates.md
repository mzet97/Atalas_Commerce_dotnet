# EVD-000-003 — Atualizações de ferramentas (Estágio A / B0)

**Data UTC:** 2026-10-28 · **Resultado:** buf PASS · docker FAIL (ausente)

## Comandos e saídas

```text
$ ~/.local/bin/buf --version
1.75.0
→ buf 1.75.0 INSTALADO em /home/mzet/.local/bin (baixado de github.com/bufbuild/buf releases/latest, binário único, sem root)

$ docker compose version / docker version / docker ps
bash: docker: command not found (todas as tentativas)
$ ls /usr/bin/docker /usr/local/bin/docker /usr/libexec/docker/cli-plugins/  → inexistentes
$ find /opt /usr/local /snap -maxdepth 3 -name docker → vazio
→ Docker NÃO está instalado neste host (diverge da informação do proprietário de 2026-10-28;
  possivelmente instalado em outra máquina/sessão). Gates M1 dependentes (Compose/Keycloak/
  Testcontainers) permanecem BLOCKED até instalação verificável com `docker compose version` exit 0.
```

Impacto: Estágio A (documentação) 100% independente de Docker; Estágio B1 parcialmente executável (repos, dotnet build/test, CI, contratos com buf) — os trechos que exigem containers ficam BLOCKED com evidência.
