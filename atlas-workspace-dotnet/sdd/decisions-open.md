# SDD — Decisões Abertas (decisions-open)

Decisões `OPEN` com impacto e alternativa segura. ADRs `PROPOSED` aguardam aceite/evidência; nada aqui vira `ACCEPTED` sem registro.

> Sincronizado em 2026-11-16 (F1 higiene): ACCEPTED = 0008/0009/0010/0011/0012/0020 (6). Restam 14 PROPOSED.

## Abertas por ADR (14 PROPOSED restantes)

| ADR | Ponto em aberto | Próximo passo | Impacto se adiada |
|---|---|---|---|
| ADR-0001 | Contrato exato do comando Admin→owner (proto) | Aceitar (implementado e provado M2/M3/M4: gRPC + token revalidado no owner) ou refinar | Governança (código já conforme) |
| ADR-0002 | Confirmar projections iniciais (quais das 10 entram no M2) | Aceitar (projetores Catalog/Offers/Inventory operando M2/M3) | Governança |
| ADR-0003 | Debezium connector vs bridge C# como primeiro mecanismo | Aceitar bridge C# (dispatcher dos workers, provado M2/M3) ou decidir Debezium | Gate M9/L2 (throughput CDC) |
| ADR-0004 | Topologia de tópicos/filas e naming | Aceitar (atlas.*.v1 + quorum queues, provado M3/M6) | Rework de consumers |
| ADR-0005 | Instalar Buf (ausente no ambiente) | **RESOLVIDO**: buf 1.75.0 em uso desde M1 (EVD-000-003); aceitar e fechar | Nenhum |
| ADR-0006 | Política de expiração de reserva (TTL) e ordem reserva↔pedido↔pagamento fina | Aceitar (quote 10min/reserva 15min + reconciliador, provado M3) | Governança |
| ADR-0007 | Modelagem exata de policies ABAC no Keycloak | Aceitar (Keycloak realm + grants ABAC + four-eyes, provado M4) ou revalidar no deploy `.51` | 403 vs UX no deploy |
| ADR-0013 | Composição de serviços por milestone (validada em roadmap) | Manter incremental (decidido) | Nenhum — já decidido incremental |
| ADR-0014 | YARP vs Minimal API reverse proxy | **Reabrir**: M5 evidenciou "YARP nunca casou (latente M2)" → forwarder explícito adotado; aceitar forwarder ou novo spike | BFF no deploy |
| ADR-0015 | Durabilidade de checkpoint na versão pinada do MAF | SPEC-007+ (F2 grafo Workflows) | Sem resume de workflow |
| ADR-0016 | Quote 10min/reserva 15min + worker de domínio | Aceitar (provado M3: COM-08 409, reconciliador) | Governança |
| ADR-0017 | PSP fake SIMULATED-PSP + HMAC | Aceitar (provado M3: T05 dedup transacional :5398) | Governança |
| ADR-0018 | Taxonomia atlas.{ctx}.v1 | Aceitar (5 tópicos AsyncAPI em uso M3/M6) | Governança |
| ADR-0019 | NetArchTest vs ArchUnitNET vs reflection | Veredito licença net10.0 pendente (M1) | Gate de arquitetura no CI |

## Aceitas recentemente (não mais abertas)

| ADR | Evidência do aceite |
|---|---|
| ADR-0008 (MAF IChatClient) | Spike + GATE M7 PASS 2026-11-03 (MAF 1.26.0 real, Ollama qwen3.6:27b) |
| ADR-0009 (consent HMAC) | GATE M7 PASS 2026-11-03 (EVD-007-001; 403 p/ payload diverso; 7/7 testes) — aceita na F1 2026-11-16 |
| ADR-0010 (Aspire rejeitado) | GATE M4 PASS 2026-11-02 (Compose+Podman provados) |
| ADR-0011 (EF Core dono migrations) | L1 real EVD-009-002 (p95 4,76ms sem cache / 0,61ms com Redis) |
| ADR-0012 (sem MediatR/MassTransit/FluentAssertions) | GATE M4 PASS 2026-11-02 (licenças; dispatch manual + Kafka/Rabbit nativos) |
| ADR-0020 (SLOs congeladas) | L1 real EVD-009-002 (gates L0/L1a congelados) |

## Abertas de negócio (não cobertas por ADR específica)

| ID | Questão | Alternativa segura atual |
|---|---|---|
| OPEN-01 | Modelagem detalhada de coleções Mongo para Returns/Reviews/Communications/Analytics (blueprint não detalha) | Detalhar nas specs de M6+; não bloqueia M0–M5 |
| OPEN-02 | Política de backorder/pré-venda (fora do MVP) | Venda bloqueada sem saldo (INV-STOCK-001); pré-venda só com política explícita |
| OPEN-03 | Reconciliação de taxas/tributos por jurisdição (fase 2) | MVP: taxes no quote com breakdown; sem emissão fiscal |
| OPEN-04 | Marketplace 3P (M10) | DEFERRED por padrão |
| OPEN-05 | SLOs virarem compromisso (hoje PROPOSED) | Congelar por ADR apenas com hardware/medição |
| OPEN-06 | Estratégia multi-região (hipótese L4) | Fora do escopo local; nunca prometer |

## Pendências de ambiente (ver audit.md §2)

| Item | Bloqueia | Remediação | Situação (2026-11-16) |
|---|---|---|---|
| Docker/Compose ausente | M1 (compose/Keycloak/Testcontainers/OTel-local) | **RESOLVIDO**: Podman rootless adotado (EVD-000-004/006..008); Docker 29.5.3 disponível no WSL | Fechado |
| Buf | M1/M2 (contratos) | — | **RESOLVIDO**: buf 1.75.0 em uso (EVD-000-003) |
| kubectl/Helm/Skaffold | M8 | **RESOLVIDO**: kubectl v1.37.0 + Helm v3.16.4 no WSL; cluster `.51` (k3s v1.36.4) acessível | Fechado p/ F0 |
| MAF/NuGet no ambiente | M1 (restore), M7 (smoke) | **RESOLVIDO**: restore + smoke MAF 1.26.0 real (EVD-007-001) | Fechado |
| Postgres `.52` (pg_hba/SSL) | Fase 4 deploy | Liberar HBA p/ `.51` + range de pods no servidor `.52` | **BLOQUEADO** (ação no servidor) |
| Kafka no cluster | Fase 4/L2 | Instalar single-node (Strimzi/KRaft) no ns `kafka` | Pendente decisão |
| Keycloak no cluster | Fase 4 | Instalar Keycloak (casa com realm-atlas.json) ou adaptar p/ authentik | Pendente decisão |

**Decisões do proprietário aplicadas (2026-10-28):** CI = GitHub Actions (SPEC-001 design); M7 = Ollama local zero-custo (cloud desativada); Estágio A aprovado e concluído.
