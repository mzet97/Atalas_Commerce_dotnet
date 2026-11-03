# EVD-000-002 — Baseline de ambiente (M0)

**Tipo:** coleta de versões (leitura; nenhuma instalação executada) · **Resultado:** PASS (coleta) com ausências registradas · **Data UTC:** 2026-10-27

## Comando

```bash
for c in "dotnet --list-sdks" "dotnet --version" "node --version" "pnpm --version" \
         "docker compose version" "kubectl version --client" "helm version" \
         "skaffold version" "buf --version" "aspire --version" "git --version" "python3 --version"; do
  printf "%-28s => " "$c"; timeout 10 bash -c "$c" 2>&1 | head -1 || echo "ABSENT"
done
```

## Saída (integral)

```text
dotnet --list-sdks           => 8.0.427 [/home/mzet/.dotnet/sdk]
dotnet --version             => 10.0.403
node --version               => v24.15.0
pnpm --version               => 11.1.3
docker compose version       => bash: line 1: docker: command not found
kubectl version --client     => bash: line 1: kubectl: command not found
helm version                 => bash: line 1: helm: command not found
skaffold version             => bash: line 1: skaffold: command not found
buf --version                => bash: line 1: buf: command not found
aspire --version             => bash: line 1: aspire: command not found
git --version                => git version 2.55.0
python3 --version            => Python 3.14.7
```

## Interpretação

- `.NET SDK 10.0.403` **coincide** com o baseline do contrato (`references/dotnet/04`, snapshot 2026-10-27). Runtime/alvo `net10.0` disponível para o M1 (validar `dotnet --list-runtimes` no bootstrap).
- Node/pnpm atendem Next.js/Angular/Playwright.
- **AUSENTES e impacto:** docker+compose (bloqueia gate M1), buf (bloqueia contratos M1/M2), kubectl/helm/skaffold (M8), aspire (opcional — ADR-0010).
- python3 usado apenas como ferramenta de auditoria (hashes); **não é stack do produto**.

Ações registradas em `sdd/decisions-open.md` §Pendências de ambiente → TASK-001-001.
