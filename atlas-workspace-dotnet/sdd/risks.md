# SDD — Matriz de Riscos (risks)

Probabilidade (P) e Impacto (I): Alta/Média/Baixa. Mitigações mapeadas a requisitos/ADRs.

| ID | Risco | P | I | Mitigação | Referência |
|---|---|---|---|---|---|
| R-01 | **APIs do MAF mudam entre versões** (framework jovem; snippets defasados) | Alta | Média | Pinar versão; smoke compile antes de codar; adapter `IChatClient` isola mudanças | ADR-0008; mestre §9.1 |
| R-02 | **Licenças desfavoráveis** (MediatR/FluentAssertions/MassTransit) | Média | Média | Baseline sem essas libs; verificação formal antes de adotar | ADR-0012 |
| R-03 | **Debezium/CDC exige replica set** e configuração SMT compatível | Média | Alta | Compose com replica set desde M1; bridge C# como fallback; semântica at-least-once declarada | ADR-0003 |
| R-04 | **Oversell em hot SKU** sob concorrência | Média | Alto | Reserva condicional atômica (CAS/version); testes parametrizados de contenda; hot-key mitigation com benchmark | INV-STOCK-001/003; mestre §18.3 |
| R-05 | **Pagamento duplicado/perdido** (timeout, webhook duplicado/fora de ordem) | Média | Alto | Idempotency keys, webhook assinado+dedup, estados UNKNOWN com reconciliador, PSP fake determinístico | INV-PAYMENT-001; ADR-0006 |
| R-06 | **Prompt injection** via descrições/reviews/seller content | Alta | Alto | Guardrails em código, allowlist de tools, outputs untrusted, consent artifact | INV-AI-001; ADR-0009/0015 |
| R-07 | **Exfiltração de PII** para modelo/logs/traces | Média | Alto | Minimização, redação, opt-in, traces sem prompts completos | INV-PII-001; SEC-006/007 |
| R-08 | **Promessa de escala não demonstrável** (1M/s) | Média | Alto (reputação) | Separar métricas; gates L0–L4 congelados antes; relatórios honestos com limitações | NFR-SCALE-001; mestre §10.2 |
| R-09 | **Dual-write acidental** (handler publicando direto) | Média | Alto | Outbox transacional obrigatório; testes de arquitetura; revisão de código focada | ADR-0003; REQ-PLT-001 |
| R-10 | **Ambiente sem Docker/Buf** trava M1/M2 | Alta (já constatado) | Médio | Instalação documentada como pré-task do M1; itens em decisions-open | audit §2 |
| R-11 | **Escopo crescer além do MVP** (3P, produtos digitais, assinaturas) | Média | Médio | Roadmap com M10 DEFERRED; specs atômicas; 1P no slice inaugural | mestre §2.11 |
| R-12 | **Divergência de documentação vs implementação** ao longo do tempo | Média | Médio | SDD evidence-first; traceability obrigatória por milestone; PASS só com EVD | mestre §11 |
| R-13 | **Cardinalidade de métricas explodir** (customer_id/order_id como label) | Média | Médio | Code review + lint de dashboards; IDs só em traces/logs redigidos | mestre §10.3 |
| R-14 | **Keycloak mal configurado** (MFA, escopos) | Média | Alto | Realms versionados em infra; testes de autorização por escopo (403 auditado) | ADR-0007; REQ-IAM |
| R-15 | **Consistência eventual confundir operadores** (admin lê projeção atrasada) | Média | Baixo | Estados PUBLISHING/SYNCING + `source_version` na UI | REQ-ADM-004 |

## Riscos aceitos

- Complexidade operacional de dois brokers + dois bancos + Keycloak: aceita como objetivo de portfólio (com mitigação de observabilidade e profiles Compose progressivos).
- Esforço de manter pt-BR + en-US + es-ES desde o MVP: aceito por requisito de produto (CAT-01, gates M5).
