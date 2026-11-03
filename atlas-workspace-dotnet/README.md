# Atlas Workspace .NET — Coordenação SDD

Repositório auxiliar de **coordenação SDD** (Spec-Driven Development) do projeto **Atlas Commerce .NET 10 + Microsoft Agent Framework**. Contém apenas a documentação de especificação, decisões e evidências da fase de coordenação; **não é um produto de software** e não contém código de aplicação.

## O que vive aqui

| Caminho | Conteúdo |
|---|---|
| `sdd/` | Contrato mestre (cópia com provenança), auditoria, manifesto de fontes, glossário, registro de requisitos/invariantes, roadmap, riscos, decisões abertas, rastreabilidade, progresso, relatórios e evidências |
| `specs/` | Unidades de especificação por milestone: `000-business-discovery` (M0), `001-platform-foundation` (M1, proposta), … |
| `architecture/` | ADRs (`decisions/`), diagramas editáveis `.mmd`/`.puml` (`diagrams/`), modelo de ameaças, runbooks |

Os três repositórios de produto — `atlas-storefront-dotnet/`, `atlas-control-dotnet/`, `atlas-platform-contracts-dotnet/` — serão criados como **irmãos deste diretório** a partir do M1 (nunca dentro deste repo; ver `.gitignore`). Cada repo de produto terá seu próprio `sdd/` e `specs/` para os comportamentos sob sua responsabilidade; este repo coordena o que é transversal até a distribuição.

## Estado atual

- **Milestone:** M0 — recon, auditoria e modelagem de negócio (sem scaffolding de código).
- **Fontes de verdade:** pacote de handoff `../ATLAS_COMMERCE_DOTNET10_MAF_SDD_AGENT_HANDOFF/` (intocado; integridade verificada em `sdd/source-manifest.md`).
- Contexto resumido do projeto: `../AGENTS.md` (workspace).

## Precedência de fontes (mais alta → mais baixa)

1. Instrução explícita mais recente do proprietário
2. `sdd/prompt.md` (cópia do `PROMPT_MASTER_ATLAS_COMMERCE_DOTNET10_MAF_SDD.md`)
3. `references/dotnet/*` do handoff
4. Documentos de negócio/invariantes/diagramas stack-agnostic do `atlas-commerce-discovery`
5. ADRs `ACCEPTED` desta edição
6. Blueprint Python original — **LEGACY**, histórico apenas

## Convenções

- IDs imutáveis após publicação: `SPEC-NNNN`, `REQ-{CTX}-NNN`, `INV-*`, `NFR-*`, `SEC-*`, `AI-*`, `TASK-{MMM}-NNN`, `TEST-{MMM}-NNN`, `EVD-{MMM}-NNN`, `ADR-NNNN`. Deprecar, nunca renumerar.
- Estados: `TODO | READY | IN_PROGRESS | PASS | FAIL | BLOCKED | DEFERRED`; ADRs: `PROPOSED | ACCEPTED | REJECTED | SUPERSEDED`.
- `PASS` somente com evidência executável registrada em `sdd/evidence/`.
- Idioma: pt-BR na documentação interna; identificadores, APIs e portfólio público em inglês.
