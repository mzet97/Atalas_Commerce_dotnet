# EVD-001-005 — Templates `dotnet new` (TASK-001-004) + smoke de restore do MAF

**Data UTC:** 2026-10-28 · **Resultado:** PASS (4/4 templates) · **MAF restore:** PASS

## Comandos e saídas (resumo; saídas completas no histórico da sessão)

```text
$ dotnet new install ./templates
Success: 4 templates instalados (atlas-microservice, atlas-module, atlas-worker, atlas-maf-agent)

$ dotnet new atlas-microservice -o svc-ofertas --service-name Atlas.Offers && cd svc-ofertas
$ dotnet build AtlasService.slnx     → 0 Error(s), 0 Warning(s)
$ dotnet test tests/...              → Passed! 2/2   (namespace substituído: Atlas.Offers)

$ dotnet new atlas-module -o mod-reports --module-name Atlas.Control.Modules.Reporting && dotnet build  → 0 errors
$ dotnet new atlas-worker -o worker-proj --worker-name Atlas.Workers.Projections && dotnet build        → 0 errors
$ dotnet new atlas-maf-agent -o agent-demo --agent-name Atlas.Agents.Shopping && dotnet build           → 0 errors

# MAF restore smoke (grafo em obj/project.assets.json do agt):
Microsoft.Agents.AI/1.26.0 · Microsoft.Agents.AI.Abstractions/1.26.0
Microsoft.Extensions.AI/10.10.0 · Microsoft.Extensions.AI.Abstractions/10.10.1  → net10.0 ✓

# Vulnerabilidades (TASK-001-002, nos 3 repos):
$ dotnet list package --vulnerable --include-transitive → "no vulnerable packages" em TODOS os projetos
```

## Correções reais durante a validação

- `tests/Directory.Build.props` (cadeia + NoWarn CA1707) adicionado ao template microservice após erro real na geração.
- `const string[]` → `static readonly string[]` no maf-agent (CS0133).

## Limitações honestas

- CI do contracts agora executa a mesma validação de templates; execução remota depende de push (TASK-001-010).
- MAF: **restore/build validados; API e execução do agente ainda NÃO** (ADR-0008: smoke de API no M7 antes de codar workflows).
