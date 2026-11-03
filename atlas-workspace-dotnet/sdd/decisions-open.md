# SDD — Decisões Abertas (decisions-open)

Decisões `OPEN` com impacto e alternativa segura. ADRs `PROPOSED` aguardam aceite/evidência; nada aqui vira `ACCEPTED` sem registro.

## Abertas por ADR (todas as 15 estão PROPOSED)

| ADR | Ponto em aberto | Próximo passo | Impacto se adiada |
|---|---|---|---|
| ADR-0001 | Contrato exato do comando Admin→owner (proto) | Definir no M2 com SPEC-002 | Contratos instáveis atrasam M2 |
| ADR-0002 | Confirmar projections iniciais (quais das 10 entram no M2) | SPEC-002 design | Escopo de workers |
| ADR-0003 | Debezium connector vs bridge C# como primeiro mecanismo | Decidir no M2 com teste real (replica set obrigatório) | Gate M2 (evento→projeção) bloqueia |
| ADR-0004 | Topologia de tópicos/filas e naming | SPEC-002/003 design | Rework de consumers |
| ADR-0005 | Instalar Buf (ausente no ambiente) | M1 bootstrap | Contratos proto sem breaking check |
| ADR-0006 | Política de expiração de reserva (TTL) e ordem reserva↔pedido↔pagamento fina | SPEC-003 design com cenários T01–T05 | Risco de rework na saga |
| ADR-0007 | Modelagem exata de policies ABAC no Keycloak | M1/M2 | 403 vs UX |
| ADR-0008 | Modelo local validado para tool calling (Ollama?) | Smoke no M7 (ou antes, spike) | `BLOCKED/INCOMPATIBLE` possível |
| ADR-0009 | Formato do consent artifact (assinatura, TTL padrão) | SPEC-007 design | Segurança do fluxo AI |
| ADR-0010 | Adotar Aspire em algum serviço | Avaliar por caso no M1+ | Nenhum (opcional) |
| ADR-0011 | Quais rotas usam Dapper | Benchmark no M2/M3 | Perf subótima localizada |
| ADR-0012 | Licenças FluentAssertions/MediatR/MassTransit | Verificação no M1 | Escolha de libs de teste |
| ADR-0013 | Composição de serviços por milestone (validada em roadmap) | Gates M2/M3 | Nenhum — já decidido incremental |
| ADR-0014 | YARP vs Minimal API reverse proxy | M2 spike | Pequeno |
| ADR-0015 | Durabilidade de checkpoint na versão pinada do MAF | Smoke M7 | Sem resume de workflow |

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

| Item | Bloqueia | Remediação | Situação (2026-10-28) |
|---|---|---|---|
| Docker/Compose ausente | M1 (compose/Keycloak/Testcontainers/OTel-local) | Instalar Docker Engine + compose plugin (requer root — ação do proprietário) | **DIVERGÊNCIA**: proprietário informou presente; verificação real mostra ausente (EVD-000-003). Gates de container BLOCKED. |
| Buf | M1/M2 (contratos) | — | **RESOLVIDO**: buf 1.75.0 instalado e usado na validação dos drafts (EVD-000-003) |
| kubectl/Helm/Skaffold | M8 | Instalar quando chegar o milestone | pendente (ok) |
| MAF/NuGet no ambiente | M1 (restore), M7 (smoke) | `dotnet restore` + smoke na versão pinada | pendente (primeiro restore no M1) |

**Decisões do proprietário aplicadas (2026-10-28):** CI = GitHub Actions (SPEC-001 design); M7 = Ollama local zero-custo (cloud desativada); Estágio A aprovado e concluído.
