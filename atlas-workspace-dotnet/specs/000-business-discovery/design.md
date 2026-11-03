# SPEC-000 — design.md (desenho técnico alvo, edição .NET)

Consolida **como** o modelo de `spec.md` será realizado em .NET 10 / C# 14 / MAF. Decisões detalhadas estão nas ADRs (PROPOSED); aqui fica o mapa integrado.

## 1. Conversões LEGACY (edição Python) → edição .NET

Tabela autoritativa: `references/dotnet/00-ADAPTATION-AND-PRECEDENCE.md` (CONFIRMED). Síntese: FastAPI→ASP.NET Core 10; workers asyncio→`BackgroundService`/`CancellationToken`; uv/Copier→dotnet SDK/CPM/templates `dotnet new`; LangChain/LangGraph→**MAF C#**; pytest→xUnit+Testcontainers; Playwright mantido; Next/Angular/Mongo/PG/Kafka/Rabbit/Redis/gRPC/Keycloak/OTel mantidos.

## 2. Topologia (2 produtos + contratos)

- `atlas-storefront-dotnet` (repo A): Next.js + Gateway/BFF (YARP, ADR-0014) + microserviços `{Catalog, Offers, Inventory, Cart, Checkout, Orders, Payments}` com camadas `Api/Application/Domain/Infrastructure` + `AI/ShoppingAssistant` + `Workers/{Projections, Notifications, Reconciliation}` + `BuildingBlocks` (só primitives não-domínio). Serviços surgem por milestone (ADR-0013).
- `atlas-control-dotnet` (repo B): Angular + `Atlas.Control.Host` monólito modular; `Modules/{CatalogOperations, InventoryOperations, PricingOperations, Approvals, Audit, Reporting}` com grafo DAG e contratos in-process; testes de arquitetura.
- `atlas-platform-contracts-dotnet` (repo C): `contracts/{proto,openapi,asyncapi,jsonschema}`, `templates/{aspnet-microservice, aspnet-modular-monolith, dotnet-worker, maf-agent}`, `infrastructure/{compose,iam,telemetry,helm,monitoring}`.

Containers: ver `C4-02-container.mmd`; contexto: `C4-01-context.mmd`; sistemas/owners .NET: `00-dotnet-systems-and-owners.mmd`.

## 3. Dados

**Write (MongoDB, replica set, ADR-0002):** coleções por owner conforme blueprint #04 §3; concorrência otimista (`version`/CAS); outbox imutável na mesma transação (ADR-0003); índices únicos para idempotency keys; composto de estoque; TTL apenas como otimização de limpeza.

**Read (PostgreSQL):** 10 projeções por caso de uso; `processed_events` + mutation da view na mesma transação; `projection_offsets` para reconstrução; FK apenas dentro de uma projeção sem impedir upsert fora de ordem. Workers: `BackgroundService` + Confluent.Kafka; EF Core 10 para migrations/escrita, Dapper nas rotas quentes após benchmark (ADR-0011).

**Pipeline CQRS:** comando→validação/política→Mongo aggregate+outbox→CDC/Debezium→Kafka (key=aggregate_id)→workers idempotentes→PG→Redis cache-aside→Query API. Read-your-writes: resposta com version; UI `PUBLISHING/SYNCING` até `source_version`.

## 4. Comunicação

| Canal | Uso | Contrato |
|---|---|---|
| HTTP/JSON | browser→Gateway/BFF; Angular→Control | OpenAPI versionado, ProblemDetails, idempotency key, correlation |
| gRPC | Admin→owners; Checkout→Pricing/Inventory | proto + Buf lint/breaking; deadlines; mTLS/workload identity (ADR-0005) |
| Kafka | eventos de domínio (CDC/outbox), analytics | envelope INV-EVT-001; partições por aggregate; replay |
| RabbitMQ | tasks/workflows (notification, etiqueta, import) | quorum, manual ack, DLQ, retry (ADR-0004) |

Anti-padrão proibido: publicar o mesmo evento para Kafka e Rabbit no request handler; tasks nascem de bridge idempotente.

## 5. Processos críticos

- **Checkout (UC-003, ADR-0006):** ver `10-sequence-checkout.mmd` + `12-state-payment-intent.mmd` + `08-state-order.mmd`; saga persistida com inbox/dedup, compensation, reconciliador; PSP fake determinístico.
- **Reserva sob contenda:** `16-sequence-concurrent-reservation.mmd` (CAS; apenas Inventory decide).
- **Publicação admin:** `15-sequence-admin-publish.mmd` (UC-005 end-to-end).
- **Refund:** `17-sequence-refund.mmd` (RMA ≠ refund; four-eyes).
- **Reconstrução de projeção:** `18-sequence-projection-rebuild.mmd`.

## 6. Assistente IA (MAF)

Módulo `AI/ShoppingAssistant` (subpastas por responsabilidade); grafo `Intent → Search(read-only) → Compare → Clarify? → Recommend → ProposeCart(inerte) → HumanApproval(consent artifact) → ApplyApprovedCartChange(UI-driven, nunca tool) → Checkout normal com re-quote`. Provider `IChatClient` (Ollama local preferido; cloud opcional com privacy review/budget; fake só em teste). Grounding com IDs+timestamps; guardrails em código; métricas e fallback. Ver ADR-0008/0009/0015 e `19-er-ai-assistant.mmd` + `01-maf-shopping-workflow.mmd`.

## 7. IAM

Keycloak OIDC Auth Code+PKCE; perfis/ações do discovery 08; ABAC por market/warehouse/seller; MFA admin; four-eyes (ADR-0007). Keycloak com persistência própria (exceção CQRS documentada).

## 8. Observabilidade e deploy

OTel→Grafana (dashboard de negócio: conversão, quote p99, reservas, oversell guard, outbox age, projection lag, payments UNKNOWN, consumer lag, DLQ, cache, AI tools/custo); sem labels de alta cardinalidade. Compose profiles (ver `20-deployment-compose.mmd`); K8s/Helm/Skaffold (ver `21-deployment-k8s.mmd`).

## 9. Testes (estratégia por risco)

Unit (VOs, policies, state machines) → Integration (Testcontainers: Mongo replica set, PG, Kafka, Rabbit, Keycloak, PSP fake) → Contract (Buf breaking, OpenAPI diff, envelope N/N-1) → E2E Playwright (3 idiomas, admin→publicação→loja→checkout) → Resilience (kill worker, replay DLQ, webhooks duplicados, corrida de reserva) → Security (OWASP API, IDOR, injection) → Performance (L0–L4, gates congelados antes). Pirâmide e gates por milestone em `sdd/roadmap.md`.

## 10. Migração destes artefatos

Em M1, cada repo de produto recebe seu `sdd/` + `specs/` correspondentes (split por ownership); este workspace mantém a coordenação e o histórico M0. IDs preservados; nenhum renumbering.
