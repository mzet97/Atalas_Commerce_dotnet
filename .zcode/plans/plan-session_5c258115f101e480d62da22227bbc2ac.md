# Plano — Documentação SDD do Atlas Commerce .NET 10 + MAF (execução do M0)

## Contexto

Todos os 18 arquivos `.md` do handoff foram lidos integralmente (prompt mestre 830 linhas, blueprint consolidado, 8 docs de discovery, 5 docs `references/dotnet`, READMEs), além dos 15 diagramas e do manifesto SHA-256 (32/32 arquivos conferem). Estado atual: `NO_REPO`, sem artefatos SDD. O contrato mestre (§11, §17, §21) define exatamente o que o M0 deve produzir — este plano executa isso, **sem scaffolding de código** (sem `dotnet new`, sem Docker, sem gasto externo).

**Decisões aplicadas** (perguntas sem resposta; usando as recomendadas): coordenação SDD em nova pasta **`atlas-workspace-dotnet/`** (árvore-alvo §5.1 do prompt mestre); **`git init`** do repo auxiliar de coordenação (permitido pelo §11.1; `.gitignore` exclui os 3 repos futuros); escopo = **M0 completo + SPEC-001 proposta** (SPEC-002..011 ficam como backlog no `roadmap.md`). Pacote de handoff permanece **intocado** (provenança).

## 1. Estrutura a criar

```
atlas-workspace-dotnet/                  # repo auxiliar de coordenação (git)
├── README.md                            # propósito, layout, como o SDD funciona, precedência
├── .gitignore                           # exclui atlas-storefront-dotnet/ atlas-control-dotnet/ atlas-platform-contracts-dotnet/
├── sdd/
│   ├── prompt.md                        # cópia do PROMPT_MASTER + cabeçalho de provenança (hash SHA-256 d1247f04…)
│   ├── audit.md                         # inventário real, baseline de ambiente, classificação LEGACY/CONFIRMED, divergências
│   ├── source-manifest.md               # 32 fontes: caminho, tamanho, SHA-256, classificação, data de leitura
│   ├── glossary.md                      # linguagem ubíqua (PT↔EN, tipos de ID, termos de domínio)
│   ├── requirements.md                  # registro de IDs: REQ-*/INV-*/NFR-*/SEC-*/AI-* (ver taxonomia §3)
│   ├── roadmap.md                       # M0–M11 com gates, dependências, mapeamento SPEC-000..011, status
│   ├── decisions-open.md                # decisões OPEN + impacto + alternativa segura
│   ├── risks.md                         # matriz de riscos (probabilidade/impacto/mitigação)
│   ├── traceability.md                  # matriz REQ → SPEC → TASK → código → TEST → EVD
│   ├── progress.md                      # diário de execução (entrada M0)
│   ├── reports/M0-report.md             # relatório final no formato §16 + checklist §21
│   └── evidence/
│       ├── EVD-000-001-source-integrity.md   # script + saída da verificação 32/32
│       └── EVD-000-002-environment-baseline.md # versões de dotnet/node/pnpm/docker/kubectl/helm/buf/aspire ou ABSENT
├── specs/
│   ├── 000-business-discovery/          # 9 arquivos: README, audit, spec, design, tasks,
│   │   │                                #   acceptance.feature, traceability, evidence, risks
│   │   └── …                            #   (conteúdo: bounded contexts, ownership, agregados,
│   │                                    #    invariantes, UC-001..011, ER/Mongo/PG, cenários críticos)
│   └── 001-platform-foundation/         # rascunho PROPOSTO (gate M0 exige "SPEC-001 proposta"):
│       └── README, spec, design, tasks  #   3 repos, templates dotnet new, CI, Compose, Keycloak, OTel
└── architecture/
    ├── decisions/ADR-0001..ADR-0015-*.md # 15 ADRs PROPOSED (§19 do prompt mestre)
    ├── diagrams/                        # 27 fontes editáveis (ver §4) + README.md de provenança/diffs
    └── threat-model.md                  # STRIDE por contexto + ameaças específicas de IA/MAF
```

~65 arquivos no total.

## 2. Conteúdo-chave por artefato

**`sdd/audit.md`** — inventário do workspace (`NO_REPO`), tabela de classificação de statements (`CONFIRMED/PROPOSED/OPEN/DEFERRED/LEGACY`), ambiente de ferramentas (baseline do §21), e **divergências encontradas** na auditoria: (a) duas taxonomias de invariantes (blueprint `INV-001..012` × prompt mestre `INV-STOCK-001…`) — reconciliadas em `requirements.md`; (b) blueprint doc 06/07 referem Python/FastAPI/LangGraph → marcados `LEGACY/NOT_APPLICABLE_TO_DOTNET`.

**`sdd/requirements.md`** — registro canônico de IDs (imutáveis após publicação):
- **Invariantes canônicas (15)**: taxonomia do prompt mestre como autoridade — `INV-STOCK-001/002`, `INV-ORDER-001/002`, `INV-MONEY-001`, `INV-PAYMENT-001`, `INV-CQRS-001`, `INV-SEC-001`, `INV-ADMIN-001`, `INV-AI-001` — **estendidas** com `INV-STOCK-003` (idempotência de ReserveStock, alias INV-002 bp), `INV-QUOTE-001` (bp INV-004), `INV-EVT-001` (bp INV-010), `INV-PII-001` (bp INV-011), `INV-I18N-001` (bp INV-008), incluindo **tabela de mapeamento blueprint↔canônico**.
- **REQ por contexto**: `REQ-CAT/PIM/OFF/INV/DSC/CRT/CHK/ORD/PAY/FFM/RET/CST/NOT/AI/ADM/IAM/PLT-{nnn}` (≈60–80 requisitos, cada um com fonte e UC ligados).
- **NFR** de `05-NFR-E-ESCALA` (SLOs propostos, L0–L4): `NFR-PERF/REL/OBS/SEC/SCALE-{nnn}`. **SEC-*** do §7 do prompt mestre; **AI-*** do §9 + doc 08.

**15 ADRs `PROPOSED`** (títulos do §19): 0001 ownership/Admin→owner (absorve ADR-001/006 do blueprint), 0002 Mongo write/PG read, 0003 outbox+CDC+Kafka, 0004 RabbitMQ tasks vs Kafka events, 0005 gRPC schema-first+Buf, 0006 checkout saga/compensações, 0007 Keycloak RBAC+ABAC+four-eyes, 0008 MAF+IChatClient, 0009 consent boundary da IA, 0010 Aspire opcional × Compose/Helm, 0011 EF Core × Dapper nas projeções, 0012 estratégia mediator/messaging/licenças (MassTransit/MediatR/FluentAssertions), 0013 granularidade de serviços, 0014 gateway/BFF+rate limiting, 0015 políticas de tools/checkpoint/PII do MAF. Cada ADR: contexto, ≥2 alternativas, proposta, trade-offs, modos de falha, custo, rollback, fontes, status `PROPOSED`.

**`specs/000-business-discovery/spec.md`** — os 16 bounded contexts com ownership único, agregados e limites transacionais, invariantes, UC-001..011 com fluxos alternativos, escopo 1P e fases 2/3. **`design.md`** — consolidação .NET: mapa de contextos com ACL, write model Mongo (coleções + outbox), read model PG (10 projeções), pipeline CQRS, comunicação (HTTP/gRPC/Kafka/Rabbit) e tradução LEGACY→.NET (tabela de conversões do `00-ADAPTATION`). **`tasks.md`** — TASK-000-001..N com comando de aceite (ex.: "revisar diagrama X sintaxe Y"). **`acceptance.feature`** — ≥8 cenários Gherkin críticos: contenda pela última unidade (UC003-T01); publicação via Admin→owner→projeção; webhook duplicado idempotente (T02/T05); saga reconcilia após falha (T03); revalidação no owner com projeção atrasada (T04); four-eyes self-approval negado; prompt injection não amplia escopo da IA; isolamento de sessões MAF. Cada `Scenario` ganha `TEST-ID` pendente.

**Diagramas (27)**: copiar os 15 originais como cópias de trabalho (procedência registrada; originais intactos no handoff) — atualizar rótulos stack-específicos (FastAPI→ASP.NET Core 10 em `09/10`; ownership+ACL em `00`; VOs anotados em `06/07`). Autorar **12 novos**: `12-state-payment-intent`, `13-state-reservation`, `14-state-approval`, `15-sequence-admin-publish`, `16-sequence-concurrent-reservation`, `17-sequence-refund`, `18-sequence-projection-rebuild`, `19-er-ai-assistant` (Conversation/ToolInvocation/Consent/Proposal), `C4-01-context`, `C4-02-container`, `20-deployment-compose`, `21-deployment-k8s`.

## 3. Ordem de execução

1. `atlas-workspace-dotnet/` + `git init` + README + .gitignore
2. `sdd/prompt.md` (cópia+provenança), `source-manifest.md`, `audit.md` → coletar baseline de ambiente (EVD-000-002)
3. `glossary.md` + `requirements.md` (reconciliação das invariantes)
4. 15 ADRs + `decisions-open.md` + `risks.md`
5. Diagramas (15 cópias atualizadas + 12 novos) + `threat-model.md` + `diagrams/README.md`
6. `specs/000-business-discovery/` completo (9 arquivos)
7. `specs/001-platform-foundation/` rascunho (4 arquivos)
8. `roadmap.md` + `traceability.md` + `progress.md`
9. `evidence/` (EVD-000-001 re-executando a verificação SHA-256; EVD-000-002) + `reports/M0-report.md`
10. Validação final: checklist §21 item a item; re-verificar hashes do handoff (prova de não-modificação); commits pequenos; atualizar `AGENTS.md` (estado → M0 concluído) e memória do projeto

## 4. Verificação e limites

- **Verificação**: integridade SHA-256 re-executada e anexada como EVD; checagens de consistência (toda INV tem REQ; todo UC crítico tem cenário; todo milestone tem SPEC; toda ADR ≥2 alternativas); validação de sintaxe Mermaid se ferramenta disponível, senão revisão manual documentada.
- **Relatório final M0** no formato §16 (pt-BR, sem alegar o que não foi executado: `TESTS: NONE (M0 é modelagem)`; `PERFORMANCE: NOT_MEASURED`; próxima TASK `READY` = primeira task do M1).
- **Fora de escopo (respeitando o contrato M0)**: qualquer `dotnet new`, scaffold, Docker, gasto externo, criação dos 3 repos de produto ou código de implementação. ADRs permanecem `PROPOSED` até decisão/aceite.
