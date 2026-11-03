# Diagramas — Provenança e Diffs

Fontes editáveis (`.mmd`/`.puml`). **Originais intocados** em `../../../ATLAS_COMMERCE_DOTNET10_MAF_SDD_AGENT_HANDOFF/` (ver `sdd/source-manifest.md`). Este diretório contém as **cópias de trabalho da edição .NET**.

## Cópias de originais (15) e diffs aplicados

| Arquivo | Origem | Diff aplicado na edição .NET |
|---|---|---|
| `00-bounded-contexts.mmd` | discovery/diagrams | `Commerce BFF - FastAPI` → `Commerce BFF / Gateway - ASP.NET Core 10`; header de provenança |
| `01-usecases-storefront.puml` | discovery/diagrams | nenhum (stack-agnostic) |
| `02-usecases-admin.puml` | discovery/diagrams | nenhum (stack-agnostic) |
| `03-er-catalog.mmd` | discovery/diagrams | nenhum (ER conceitual válido) |
| `04-er-order-finance.mmd` | discovery/diagrams | nenhum |
| `05-er-admin.mmd` | discovery/diagrams | nenhum |
| `06-classes-order.mmd` | discovery/diagrams | nota `.NET edition` (VOs como `readonly record struct`) |
| `07-classes-inventory.mmd` | discovery/diagrams | nota `.NET edition` idem |
| `08-state-order.mmd` | discovery/diagrams | nenhum (máquina de estados de negócio) |
| `09-cqrs-projection.mmd` | discovery/diagrams | `FastAPI Command Handler` → `ASP.NET Core 10 Command Handler`; header |
| `10-sequence-checkout.mmd` | discovery/diagrams | nenhum (participantes já stack-agnostic) |
| `11-usecases-summary.mmd` | discovery/diagrams | nenhum |
| `00-dotnet-systems-and-owners.mmd` | references/dotnet/diagrams | nenhum (já edição .NET CONFIRMED) |
| `01-maf-shopping-workflow.mmd` | references/dotnet/diagrams | nenhum |
| `02-sdd-milestone-gates.mmd` | references/dotnet/diagrams | nenhum |

## Novos diagramas da edição .NET (12) — requisito §3.3 do prompt mestre

| Arquivo | Conteúdo |
|---|---|
| `12-state-payment-intent.mmd` | Estados PaymentIntent incl. UNKNOWN→reconciliação (INV-PAYMENT-001) |
| `13-state-reservation.mmd` | Estados Reservation; confirmada não expira por TTL (INV-STOCK-002/003) |
| `14-state-approval.mmd` | Fluxo de aprovação four-eyes e revalidação no owner (INV-ADMIN-001) |
| `15-sequence-admin-publish.mmd` | UC-005: Admin→Control→owner→outbox→CDC→Kafka→PG→Storefront |
| `16-sequence-concurrent-reservation.mmd` | UC003-T01: corrida pela última unidade com CAS |
| `17-sequence-refund.mmd` | UC-004: RMA distinto de refund; four-eyes; webhook |
| `18-sequence-projection-rebuild.mmd` | Replay/backfill idempotente de projeção |
| `19-er-ai-assistant.mmd` | ER do contexto Shopping Assistant (sessão ≠ negócio) |
| `C4-01-context.mmd` | C4 nível 1 (sistemas e externos) |
| `C4-02-container.mmd` | C4 nível 2 (containers por repo; serviços por milestone) |
| `20-deployment-compose.mmd` | Compose profiles core/data/messaging/observability/ai/simulated-external |
| `21-deployment-k8s.mmd` | K8s alvo (namespaces, stateful, KEDA, mTLS, políticas) |

## Regras

- Diagramas são **fonte editável**; toda mudança registra diff aqui ou em ADR quando muda semântica de ownership.
- Validar sintaxe Mermaid/PlantUML antes de commit quando ferramenta disponível (no ambiente atual não há renderizador — revisão manual documentada no M0-report).
