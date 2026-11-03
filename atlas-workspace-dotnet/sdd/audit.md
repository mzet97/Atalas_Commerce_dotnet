# SDD — Auditoria (M0 / SPEC-000)

**Data:** 2026-10-27 (UTC) · **Escopo:** inventário real do workspace, baseline de ambiente, classificação de statements e divergências. Sem scaffolding, sem execução de build/testes de produto (M0 é modelagem).

## 1. Inventário do workspace

```text
/var/mnt/4TB/TI/git/Atalas_Commerce_dotnet/
├── ATLAS_COMMERCE_DOTNET10_MAF_SDD_AGENT_HANDOFF/   # pacote de handoff (32 arquivos, PROVENANCE — intocado)
│   ├── PROMPT_MASTER_*.md · README_HANDOFF_DOTNET.md · atlas-commerce-blueprint.md
│   ├── atlas-commerce-discovery/{README.md, docs/01..08, diagrams/ 12 fontes}
│   ├── references/dotnet/{00..04 *.md, diagrams/ 3 fontes}
│   └── SOURCE_MANIFEST_SHA256.json
├── AGENTS.md                                        # contexto do workspace (criado 2026-10-27)
└── atlas-workspace-dotnet/                          # ESTE repo auxiliar de coordenação SDD (git, branch main)
```

- **Repositórios de produto:** `NO_REPO` (os 3 repos-alvo não existem; criação é gate M1).
- **Git:** `atlas-workspace-dotnet` inicializado em 2026-10-27, branch `main`, primeiro commit a registrar no `progress.md`.
- **Integridade das fontes:** 32/32 arquivos conferem com `SOURCE_MANIFEST_SHA256.json` (ver `evidence/EVD-000-001`).

## 2. Baseline de ambiente (coletado em 2026-10-27 — ver `evidence/EVD-000-002`)

| Ferramenta | Versão/estado | Observação |
|---|---|---|
| .NET SDK | **10.0.403** (`dotnet --version`); SDK 8.0.427 também instalado | ✅ coincide com o baseline do contrato (`04-DEPENDENCIES`, snapshot 2026-10-27) |
| Node.js | v24.15.0 | ✅ presente |
| pnpm | 11.1.3 | ✅ presente |
| Docker/Compose | **AUSENTE** | ⛔ necessário no M1 (Compose é obrigatório no contrato) |
| kubectl | **AUSENTE** | ⛔ necessário a partir do M8 |
| Helm | **AUSENTE** | ⛔ M8 |
| Skaffold | **AUSENTE** | ⛔ M8 |
| Buf | **AUSENTE** | ⛔ M1/M2 (contratos proto com breaking checks) |
| .NET Aspire CLI | **AUSENTE** | opcional (ADR-0010); não bloqueia |
| git | 2.55.0 | ✅ |
| python3 | 3.14.7 | usado apenas para verificação de hashes (script de auditoria), não é stack do produto |

**Consequência para o roadmap:** M1 só pode ser `PASS` após instalar Docker/Compose (e Buf para contratos). Itens registrados em `decisions-open.md` e `risks.md`; não são bloqueios de M0.

## 3. Classificação de statements (amostra relevante; detalhe em `source-manifest.md`)

| Statement | Classe | Fonte |
|---|---|---|
| Bounded contexts, agregados, ownership, invariantes INV-001..012 | **CONFIRMED** (negócio) | blueprint #02, discovery 02 |
| UC-001..UC-011 com fluxos alternativos e cenários UC003-T01..T05 | **CONFIRMED** | blueprint #03, discovery 03 |
| ER conceitual, write model Mongo, read model PG, pipeline CQRS | **CONFIRMED** | blueprint #04, discovery 04 |
| NFRs/SLOs propostos e estágios L0–L4 | **PROPOSED** (metas a pactuar, nunca medidas) | discovery 05 |
| ADRs ADR-001..006 do blueprint | **PROPOSED** (repristinadas como ADR-0001..0015 .NET) | blueprint #06 |
| Microserviços FastAPI, uv/Copier, workers Python/asyncio | **LEGACY / NOT_APPLICABLE_TO_DOTNET** | blueprint #06 §2/§6, discovery README |
| LangChain/LangGraph como fluxo do assistente | **LEGACY** → substituído por MAF C# | blueprint #08; `references/dotnet/02` |
| .NET 10/C# 14, .slnx, CPM, MAF 1.26.0, Keycloak, Mongo/PG/Kafka/Rabbit/Redis/gRPC | **CONFIRMED** | prompt mestre §2/§6; `references/dotnet/00` |
| Versões de pacotes além do baseline (.NET, MAF) | **OPEN** — verificar no dia do bootstrap | `references/dotnet/04` |

## 4. Divergências e lacunas (resumo; detalhe em `source-manifest.md` §Citações divergentes)

1. **D-1 Duas taxonomias de invariantes** — reconciliadas em `requirements.md`: taxonomia do prompt mestre (INV-STOCK-001 etc.) é canônica; blueprint INV-001..012 viram alias. Extensões canônicas criadas: `INV-STOCK-003`, `INV-QUOTE-001`, `INV-EVT-001`, `INV-PII-001`, `INV-I18N-001`.
2. **D-2 Stack Python no blueprint** — tratada como LEGACY; conversões mapeadas em `specs/000-business-discovery/design.md`.
3. **D-3 Snapshot de versões** — baseline confere para .NET SDK; demais pacotes a revalidar no M1 (`docs/dependency-matrix.md` será criado lá).
4. **D-4 Rótulos de diagramas** — cópias de trabalho .NET em `architecture/diagrams/` com diffs registrados.

## 5. Verificação de consistência negócio ↔ ownership ↔ invariantes ↔ UC ↔ ER/UML ↔ eventos

| Checagem | Resultado |
|---|---|
| Todo bounded context tem owner único (write) | ✅ confirmado na tabela de contexts (discovery 02); Admin Control Plane não persiste estado canônico de Catalog/Offer/Inventory/Order/Payment |
| Toda invariante de negócio tem representação na taxonomia canônica | ✅ após reconciliação D-1 (15 invariantes canônicas) |
| UC críticos (P0) têm cenários Given/When/Then | ✅ UC-003 T01..T05 + catálogo de testes por domínio; expandidos em `specs/000/acceptance.feature` |
| Entidades do ER têm coleção Mongo (write) ou projeção PG (read) definida | ✅ blueprint #04 §§2–4; lacuna menor: entidades de Returns/Reviews/Communications/Analytics sem coleção detalhada → registrada como abertura menor em `decisions-open.md` (não bloqueia M0; detalhar nas specs dos respectivos milestones M6+) |
| Envelope de evento (event_id, aggregate_version, correlation…) especificado | ✅ INV-EVT-001 (alias blueprint INV-010) |
| Modelo Product/SKU/Offer/Seller/Market preparado para 1P→3P | ✅ blueprint #02 §5 |

## 6. ADRs existentes vs. necessárias

Blueprint traz 6 ADRs `proposto`; o prompt mestre §19 exige 15 temas de ADR antes de infra pesada. As 15 foram criadas em `architecture/decisions/ADR-0001..0015` com status `PROPOSED` (mapeamento blueprint↔nova numeração registrado em cada arquivo).
