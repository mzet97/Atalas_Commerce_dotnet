# SDD — Manifesto de Fontes (source-manifest)

**Data da auditoria:** 2026-10-27 (UTC) · **Verificação de integridade:** `sdd/evidence/EVD-000-001` — 32/32 arquivos OK contra `SOURCE_MANIFEST_SHA256.json`.

Origem: pacote de handoff `../ATLAS_COMMERCE_DOTNET10_MAF_SDD_AGENT_HANDOFF/` (manifesto `manifest_version 1.0`, `specification_date 2026-10-27`). Todos os arquivos foram **lidos integralmente** na sessão de 2026-10-27.

Classificações: `SOURCE` = original fornecido, inalterado · leitura: `READ` (lido integralmente) · statement: ver coluna Classe.

| # | Caminho (no handoff) | Bytes | Classe | Conteúdo |
|---|---|---:|---|---|
| 1 | `PROMPT_MASTER_ATLAS_COMMERCE_DOTNET10_MAF_SDD.md` | 79505 | CONFIRMED (contrato) | Contrato mestre de execução SDD; cópia versionada em `sdd/prompt.md` |
| 2 | `README_HANDOFF_DOTNET.md` | 1930 | CONFIRMED | Instruções de início, estrutura-alvo, aviso de não-implementação |
| 3 | `atlas-commerce-blueprint.md` | 72803 | MISTO | Negócio = CONFIRMED; trechos Python/FastAPI/LangGraph = LEGACY |
| 4 | `atlas-commerce-discovery/README.md` | 3245 | MISTO | Idem: negócio CONFIRMED; "FastAPI/LangGraph" LEGACY |
| 5–12 | `atlas-commerce-discovery/docs/01-VISAO-E-ESCOPO.md` … `08-I18N-IAM-AI.md` | — | MISTO | 8 docs de modelagem; invariantes/fluxos CONFIRMED; stack LEGACY |
| 13–24 | `atlas-commerce-discovery/diagrams/{00..11}.{mmd,puml}` (12 arquivos) | — | MISTO | Fontes editáveis de negócio; rótulos FastAPI em `09/10` LEGACY |
| 25 | `references/dotnet/00-ADAPTATION-AND-PRECEDENCE.md` | 3.2K | CONFIRMED | ADR-base: precedência, tabela de conversões, exclusões explícitas |
| 26 | `references/dotnet/01-DOTNET-ARCHITECTURE.md` | 5.1K | CONFIRMED | Arquitetura .NET: 2 sistemas + 3 repos, camadas, decisões |
| 27 | `references/dotnet/02-MAF-SHOPPING-ASSISTANT.md` | 5.2K | CONFIRMED | MAF em C#: grafo, matriz de tools, gates mínimos |
| 28 | `references/dotnet/03-SDD-GATES-AND-ROADMAP.md` | 3.5K | CONFIRMED | Layout SDD, milestones M0–M11, protocolo de status |
| 29 | `references/dotnet/04-DEPENDENCIES-AND-SOURCES.md` | 2.6K | SNAPSHOT | Matriz de versões datada 2026-10-27 (revalidar no bootstrap) |
| 30–32 | `references/dotnet/diagrams/{00,01,02}*.mmd` (3 arquivos) | — | CONFIRMED | Diagramas .NET: sistemas/owners, workflow MAF, gates |

SHA-256 individuais: ver `SOURCE_MANIFEST_SHA256.json` no pacote (todos conferidos — EVD-000-001). O manifesto completo com hashes está disponível no pacote de origem e é re-verificável pelo script registrado em EVD-000-001.

## Citações divergentes / conflitos detectados

| Item | Divergência | Tratamento |
|---|---|---|
| D-1 | Duas taxonomias de invariantes: blueprint `INV-001..012` × prompt mestre `INV-STOCK-001…INV-AI-001` | Reconciliadas em `sdd/requirements.md` (taxonomia do prompt mestre = canônica; blueprint = alias) |
| D-2 | Blueprint (docs 06/07, README discovery) descrevem FastAPI/uv/Copier/LangGraph | LEGACY/NOT_APPLICABLE_TO_DOTNET conforme `references/dotnet/00-ADAPTATION-AND-PRECEDENCE.md` §Conversions |
| D-3 | `04-DEPENDENCIES-AND-SOURCES.md` lista baseline de versões (.NET 10.0.14/SDK 10.0.403, MAF 1.26.0) | Snapshot datado; revalidação obrigatória no M1; nunca tratar como "última versão" garantida |
| D-4 | Diagramas `09/10` rotulam participantes como FastAPI/BFF Python | Cópias de trabalho em `architecture/diagrams/` corrigidas para ASP.NET Core 10 (diff registrado no `README.md` local) |

## Estado dos repositórios

`NO_REPO` — nenhum dos três repositórios-alvo (`atlas-storefront-dotnet`, `atlas-control-dotnet`, `atlas-platform-contracts-dotnet`) existe. Este repo auxiliar (`atlas-workspace-dotnet`) contém apenas coordenação SDD.
